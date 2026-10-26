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
        //RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext dbContext,
        AuditWriter auditWriter,
        IOptions<AccountLifecycleOptions> options,
        INotificationService notifications)
    {
        _userManager = userManager;
        //_roleManager = roleManager;
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
            cancellationToken);

        await _auditWriter.WriteAsync(
            "ACTIVATION_ISSUED",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken);

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
            role != RoleNames.Administrator)
        {
            throw new InvalidOperationException(
                "Invalid portal role.");
        }
    }

    private async Task ValidateProjectsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return;
        }

        var distinctIds =
            projectIds.Distinct().ToArray();

        var existingCount =
            await _dbContext.Projects
                .CountAsync(
                    project =>
                        distinctIds.Contains(
                            project.Id),
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
            return;
        }

        user.Status =
            AccountStatus.Active;

        user.UpdatedAtUtc =
            DateTimeOffset.UtcNow;

        await _userManager.UpdateAsync(
            user);

        await _auditWriter.WriteAsync(
            "ACCOUNT_ENABLED",
            user.Id,
            reason,
            correlationId,
            cancellationToken);
    }

    public async Task DisableAsync(
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
            return;
        }

        user.Status =
            AccountStatus.Disabled;

        user.UpdatedAtUtc =
            DateTimeOffset.UtcNow;

        await _userManager.UpdateAsync(
            user);

        await _userManager
            .UpdateSecurityStampAsync(
                user);

        await _auditWriter.WriteAsync(
            "ACCOUNT_DISABLED",
            user.Id,
            reason,
            correlationId,
            cancellationToken);
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
                        user.Status.ToString(),

                    Roles =
                        roles.ToList()
                });
        }

        return result;
    }

    public async Task UnlockAsync(
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
            cancellationToken);
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
            cancellationToken);

        return new ReissueActivationResponse
        {
            UserId = user.Id,

            ExpiresAtUtc = expiresAt,

            ActivationUrl =
                BuildActivationUrl(
                    user.Id,
                    rawToken)
        };
    }

    public Task ReissueActivationAsync(Guid userId, string reason, string? correlationId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}