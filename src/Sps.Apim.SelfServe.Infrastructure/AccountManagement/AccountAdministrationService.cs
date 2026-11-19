using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sps.Apim.SelfServe.Application.AccountManagement;
using Sps.Apim.SelfServe.Domain.Constants;
using Sps.Apim.SelfServe.Domain.Entities;
using Sps.Apim.SelfServe.Domain.Enums;
using Sps.Apim.SelfServe.Infrastructure.Auditing;
using Sps.Apim.SelfServe.Infrastructure.Identity;
using Sps.Apim.SelfServe.Infrastructure.Persistence;
using Sps.Apim.SelfServe.Application.Notifications;

namespace Sps.Apim.SelfServe.Infrastructure.AccountManagement;

public sealed class AccountAdministrationService
    : IAccountAdministrationService
{
    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly ApplicationDbContext
        _dbContext;

    private readonly AuditWriter
        _auditWriter;

    private readonly AccountLifecycleOptions
        _options;
    private readonly INotificationService
    _notifications;

    public AccountAdministrationService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        AuditWriter auditWriter,
        IOptions<AccountLifecycleOptions> options,
        INotificationService notifications)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _options = options.Value;
        _notifications = notifications;
    }

    public async Task<CreateUserResponse>
        CreateUserAsync(
            ClaimsPrincipal administrator,
            CreateUserRequest request,
            string? correlationId,
            CancellationToken cancellationToken)
    {
        var email =
            request.Email.Trim();

        var existingUser =
            await _userManager
                .FindByEmailAsync(email);

        if (existingUser is not null)
        {
            throw new InvalidOperationException(
                "A portal account already exists " +
                "for this email address.");
        }

        ValidateRole(request.Role);

        await ValidateProjectsAsync(
            request.ProjectIds,
            cancellationToken);

        var administratorId =
            GetUserId(administrator);

        var user =
            new ApplicationUser
            {
                Id = Guid.NewGuid(),

                UserName = email,
                Email = email,

                DisplayName =
                    request.DisplayName.Trim(),

                Status =
                    AccountStatus.PendingActivation,

                EmailConfirmed = true,

                LockoutEnabled = true,

                CreatedAtUtc =
                    DateTimeOffset.UtcNow
            };

        // ICC creates the account WITHOUT a password.
        var createResult =
            await _userManager
                .CreateAsync(user);

        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                FormatIdentityErrors(
                    createResult));
        }

        var roleResult =
            await _userManager
                .AddToRoleAsync(
                    user,
                    request.Role);

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                FormatIdentityErrors(
                    roleResult));
        }

        foreach (
            var projectId
            in request.ProjectIds.Distinct())
        {
            _dbContext.UserProjects.Add(
                new UserProject
                {
                    UserId = user.Id,
                    ProjectId = projectId
                });
        }

        var rawToken =
            SecureTokenGenerator.Generate();

        var token =
            new AccountToken
            {
                Id = Guid.NewGuid(),

                UserId = user.Id,

                Type =
                    AccountTokenType.Activation,

                TokenHash =
                    SecureTokenGenerator.Hash(
                        rawToken),

                CreatedAtUtc =
                    DateTimeOffset.UtcNow,

                ExpiresAtUtc =
                    DateTimeOffset.UtcNow
                        .AddMinutes(
                            _options
                                .ActivationTokenLifetimeMinutes),

                IssuedByUserId =
                    administratorId
            };

        _dbContext.AccountTokens.Add(token);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        await _auditWriter.WriteAsync(
            "ACCOUNT_CREATED",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken,
            administratorId);

        await _auditWriter.WriteAsync(
            "ACTIVATION_ISSUED",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken,
            administratorId);

        var activationUrl =
            BuildActivationUrl(
                user.Id,
                rawToken);

        await _notifications
            .SendActivationEmailAsync(
                user.Email!,
                activationUrl,
                cancellationToken);

        return new CreateUserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            DisplayName = user.DisplayName,
            Status = user.Status.ToString()
        };
    }

    private void ValidateRole(
        string role)
    {
        if (
            role != RoleNames.User &&
            role != RoleNames.Administrator &&
            !RoleNames.ManageAccessRoles.Contains(role))
        {
            throw new InvalidOperationException(
                "Invalid portal role.");
        }
    }

    private async Task ValidateProjectsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? existingProjectIds = null)
    {
        if (projectIds.Count == 0)
        {
            return;
        }

        var distinctIds =
            projectIds.Distinct().ToArray();
        var existingIds = existingProjectIds?.ToArray() ?? [];

        var existingCount =
            await _dbContext.Projects
                .CountAsync(
                    project =>
                        distinctIds.Contains(project.Id) &&
                        (project.IsActive || existingIds.Contains(project.Id)),
                    cancellationToken);

        if (
            existingCount !=
            distinctIds.Length)
        {
            throw new InvalidOperationException(
                "One or more selected projects " +
                "do not exist.");
        }
    }

    public async Task<IReadOnlyCollection<ProjectResponse>> GetProjectsAsync(
            CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .Where(project => project.IsActive)
            .OrderBy(project => project.Name)
            .Select(project => new ProjectResponse
            {
                Id = project.Id,
                Name = project.Name,
                IsActive = project.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<UserAccessResponse> GetUserAccessAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new KeyNotFoundException("User account was not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault(RoleNames.ManageAccessRoles.Contains)
            ?? roles.FirstOrDefault(roleName => roleName == RoleNames.Administrator)
            ?? RoleNames.ApiDeveloper;

        var projectIds = await _dbContext.UserProjects
            .Where(userProject => userProject.UserId == userId)
            .Select(userProject => userProject.ProjectId)
            .ToListAsync(cancellationToken);

        var historyEvents = new[]
        {
                "ACCOUNT_CREATED",
                "USER_ACCESS_UPDATED",
                "ACCOUNT_ENABLED",
                "ACCOUNT_DISABLED",
                "ACCOUNT_UNLOCKED",
                "ACTIVATION_ISSUED",
                "ACTIVATION_REISSUED"
            };
        var audits = await _dbContext.AuditEvents
            .AsNoTracking()
            .Where(audit =>
                audit.UserId == userId &&
                historyEvents.Contains(audit.EventType))
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var actorIds = audits
            .Where(audit => audit.ActorUserId.HasValue)
            .Select(audit => audit.ActorUserId!.Value)
            .Distinct()
            .ToArray();
        var actorNames = await _userManager.Users
            .Where(actor => actorIds.Contains(actor.Id))
            .ToDictionaryAsync(
                actor => actor.Id,
                actor => actor.DisplayName,
                cancellationToken);

        return new UserAccessResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            Status = user.Status.ToString(),
            IsLocked = user.LockoutEnd.HasValue &&
                user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            Role = role == RoleNames.Administrator
                ? RoleNames.IccAdministrator
                : role == RoleNames.User
                    ? RoleNames.ApiDeveloper
                    : role,
            ProjectIds = projectIds,
            Projects = await _dbContext.Projects
                .AsNoTracking()
                .Where(project =>
                    project.IsActive || projectIds.Contains(project.Id))
                .OrderBy(project => project.Name)
                .Select(project => new ProjectResponse
                {
                    Id = project.Id,
                    Name = project.Name,
                    IsActive = project.IsActive
                })
                .ToListAsync(cancellationToken),
            History = audits.Select(audit => new AccountAccessHistoryResponse
            {
                Action = audit.EventType,
                Reason = audit.Outcome,
                ChangedBy = audit.ActorUserId is Guid actorId &&
                    actorNames.TryGetValue(actorId, out var actorName)
                        ? actorName
                        : "Not recorded",
                ChangedAtUtc = audit.OccurredAtUtc
            }).ToArray()
        };
    }

    public async Task UpdateUserAccessAsync(
       ClaimsPrincipal administrator,
       Guid userId,
       UpdateUserAccessRequest request,
       string? correlationId,
       CancellationToken cancellationToken)
    {
        ValidateRole(request.Role);

        if (!RoleNames.ManageAccessRoles.Contains(request.Role))
        {
            throw new InvalidOperationException("Select a valid access role.");
        }

        if (!Enum.TryParse<AccountStatus>(request.Status, out var requestedStatus) ||
            !Enum.IsDefined(requestedStatus))
        {
            throw new InvalidOperationException("Select a valid account status.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Enter a reason for the change.");
        }

        var actorId = GetUserId(administrator);

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());

                if (user is null)
                {
                    throw new KeyNotFoundException("User account was not found.");
                }

                if (requestedStatus == AccountStatus.Active &&
                    user.ActivatedAtUtc is null &&
                    user.Status != AccountStatus.Active)
                {
                    throw new InvalidOperationException(
                        "A pending account must be activated before it can be enabled.");
                }

                if (user.Status != AccountStatus.PendingActivation &&
                    requestedStatus == AccountStatus.PendingActivation)
                {
                    throw new InvalidOperationException(
                        "An account cannot be moved back to pending activation.");
                }

                var existingProjects = await _dbContext.UserProjects
                    .Where(userProject => userProject.UserId == userId)
                    .ToListAsync(cancellationToken);

                await ValidateProjectsAsync(
                    request.ProjectIds,
                    cancellationToken,
                    existingProjects.Select(project => project.ProjectId).ToArray());

                var existingRoles = await _userManager.GetRolesAsync(user);

                var managedRoles = existingRoles
                    .Where(existingRole =>
                        RoleNames.ManageAccessRoles.Contains(existingRole) ||
                        existingRole == RoleNames.User ||
                        existingRole == RoleNames.Administrator)
                    .ToArray();

                if (managedRoles.Length > 0)
                {
                    var removeResult =
                        await _userManager.RemoveFromRolesAsync(user, managedRoles);

                    if (!removeResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            FormatIdentityErrors(removeResult));
                    }
                }

                var addResult =
                    await _userManager.AddToRoleAsync(user, request.Role);

                if (!addResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        FormatIdentityErrors(addResult));
                }

                _dbContext.UserProjects.RemoveRange(existingProjects);

                foreach (var projectId in request.ProjectIds.Distinct())
                {
                    _dbContext.UserProjects.Add(new UserProject
                    {
                        UserId = userId,
                        ProjectId = projectId
                    });
                }

                user.Status = requestedStatus;

                user.DisabledAtUtc = requestedStatus == AccountStatus.Disabled
                    ? user.DisabledAtUtc ?? DateTimeOffset.UtcNow
                    : null;

                user.UpdatedAtUtc = DateTimeOffset.UtcNow;

                var updateResult = await _userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        FormatIdentityErrors(updateResult));
                }

                var stampResult =
                    await _userManager.UpdateSecurityStampAsync(user);

                if (!stampResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        FormatIdentityErrors(stampResult));
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                await _auditWriter.WriteAsync(
                    "USER_ACCESS_UPDATED",
                    user.Id,
                    request.Reason.Trim(),
                    correlationId,
                    cancellationToken,
                    actorId);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
    private Guid? GetUserId(
        ClaimsPrincipal administrator)
    {
        var value =
            _userManager.GetUserId(
                administrator);

        return Guid.TryParse(
            value,
            out var id)
                ? id
                : null;
    }

    private string BuildActivationUrl(
        Guid userId,
        string token)
    {
        var baseUrl =
            _options.PortalBaseUrl
                .TrimEnd('/');

        return
            $"{baseUrl}/activate" +
            $"?userId={Uri.EscapeDataString(userId.ToString())}" +
            $"&token={Uri.EscapeDataString(token)}";
    }

    private static string FormatIdentityErrors(
        IdentityResult result)
    {
        return string.Join(
            "; ",
            result.Errors.Select(
                error =>
                    $"{error.Code}: " +
                    error.Description));
    }

    public async Task EnableAsync(
    ClaimsPrincipal administrator,
    Guid userId,
    string reason,
    string? correlationId,
    CancellationToken cancellationToken)
    {
        var user =
            await _userManager
                .FindByIdAsync(
                    userId.ToString());

        if (user is null)
        {
            throw new KeyNotFoundException(
                "User account was not found.");
        }

        if (user.Status != AccountStatus.Disabled)
        {
            throw new InvalidOperationException(
                "Only disabled accounts can be enabled.");
        }

        user.Status =
            AccountStatus.Active;

        user.DisabledAtUtc = null;

        user.UpdatedAtUtc =
            DateTimeOffset.UtcNow;

        var updateResult =
            await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(
                FormatIdentityErrors(updateResult));
        }

        var stampResult =
            await _userManager.UpdateSecurityStampAsync(user);

        if (!stampResult.Succeeded)
        {
            throw new InvalidOperationException(
                FormatIdentityErrors(stampResult));
        }

        await _auditWriter.WriteAsync(
            "ACCOUNT_ENABLED",
            user.Id,
            reason,
            correlationId,
            cancellationToken,
            GetUserId(administrator));
    }

    public async Task DisableAsync(
    ClaimsPrincipal administrator,
    Guid userId,
    string reason,
    string? correlationId,
    CancellationToken cancellationToken)
    {
        var user =
            await _userManager
                .FindByIdAsync(
                    userId.ToString());

        if (user is null)
        {
            throw new KeyNotFoundException(
                "User account was not found.");
        }

        if (user.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException(
                "Only active accounts can be disabled.");
        }

        user.Status =
            AccountStatus.Disabled;

        user.DisabledAtUtc =
            DateTimeOffset.UtcNow;

        user.UpdatedAtUtc =
            DateTimeOffset.UtcNow;

        var updateResult =
            await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(
                FormatIdentityErrors(updateResult));
        }

        var stampResult =
            await _userManager.UpdateSecurityStampAsync(user);

        if (!stampResult.Succeeded)
        {
            throw new InvalidOperationException(
                FormatIdentityErrors(stampResult));
        }

        await _auditWriter.WriteAsync(
            "ACCOUNT_DISABLED",
            user.Id,
            reason,
            correlationId,
            cancellationToken,
            GetUserId(administrator));
    }

    public async Task<
    IReadOnlyCollection<UserSummaryResponse>>
    GetUsersAsync(
        CancellationToken cancellationToken)
    {
        var users =
            await _userManager.Users
                .OrderBy(x => x.Email)
                .ToListAsync(
                    cancellationToken);

        var result =
            new List<UserSummaryResponse>();

        foreach (var user in users)
        {
            var roles =
                await _userManager
                    .GetRolesAsync(user);

            result.Add(
                new UserSummaryResponse
                {
                    Id = user.Id,

                    Email = user.Email ?? "",

                    DisplayName =
                        user.DisplayName,

                    Status =
                        user.LockoutEnd.HasValue &&
                        user.LockoutEnd.Value > DateTimeOffset.UtcNow
                            ? "Locked"
                            : user.Status.ToString(),

                    Roles =
                        roles.Select(role => role switch
                        {
                            RoleNames.Administrator => RoleNames.IccAdministrator,
                            RoleNames.User => RoleNames.ApiDeveloper,
                            _ => role
                        }).ToList(),

                    Projects = await _dbContext.UserProjects
                        .Where(userProject => userProject.UserId == user.Id)
                        .Select(userProject => userProject.Project.Name)
                        .OrderBy(projectName => projectName)
                        .ToListAsync(cancellationToken)
                });
        }

        return result;
    }

    public Task<int> GetUserCountAsync(
        CancellationToken cancellationToken)
    {
        return _userManager.Users.CountAsync(cancellationToken);
    }

    public async Task UnlockAsync(
    ClaimsPrincipal administrator,
    Guid userId,
    string? correlationId,
    CancellationToken cancellationToken)
    {
        var user =
            await _userManager
                .FindByIdAsync(
                    userId.ToString());

        if (user is null)
        {
            return;
        }


        await _userManager
            .SetLockoutEndDateAsync(
                user,
                null);

        await _auditWriter.WriteAsync(
            "ACCOUNT_UNLOCKED",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken,
            GetUserId(administrator));
    }

    public async Task<ReissueActivationResponse>
    ReissueActivationAsync(
        ClaimsPrincipal administrator,
        Guid userId,
        ReissueActivationRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.FindByIdAsync(
                userId.ToString());

        if (user is null)
        {
            throw new InvalidOperationException(
                "User account was not found.");
        }

        if (
            user.Status !=
            AccountStatus.PendingActivation)
        {
            throw new InvalidOperationException(
                "Only accounts pending activation " +
                "can receive a new activation link.");
        }

        var now =
            DateTimeOffset.UtcNow;

        // Invalidate all previous unused activation tokens.
        var existingTokens =
            await _dbContext.AccountTokens
                .Where(token =>
                    token.UserId == user.Id &&
                    token.Type ==
                        AccountTokenType.Activation &&
                    token.UsedAtUtc == null)
                .ToListAsync(
                    cancellationToken);

        foreach (var existingToken in existingTokens)
        {
            existingToken.UsedAtUtc = now;
        }

        var rawToken =
            SecureTokenGenerator.Generate();

        var expiresAt =
            now.AddMinutes(
                _options
                    .ActivationTokenLifetimeMinutes);

        var administratorId =
            GetUserId(administrator);

        var accountToken =
            new AccountToken
            {
                Id = Guid.NewGuid(),

                UserId = user.Id,

                Type =
                    AccountTokenType.Activation,

                TokenHash =
                    SecureTokenGenerator.Hash(
                        rawToken),

                CreatedAtUtc = now,

                ExpiresAtUtc = expiresAt,

                IssuedByUserId =
                    administratorId
            };

        _dbContext.AccountTokens.Add(
            accountToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await _auditWriter.WriteAsync(
            "ACTIVATION_REISSUED",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken,
            administratorId);

        var activationUrl =
            BuildActivationUrl(
                user.Id,
                rawToken);

        await _notifications
            .SendActivationEmailAsync(
                user.Email!,
                activationUrl,
                cancellationToken);

        return new ReissueActivationResponse
        {
            UserId = user.Id,

            ExpiresAtUtc = expiresAt
        };
    }

}