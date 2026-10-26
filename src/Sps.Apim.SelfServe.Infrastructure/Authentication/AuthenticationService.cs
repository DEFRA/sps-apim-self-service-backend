using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sps.Apim.SelfServe.Application.Authentication;
using Sps.Apim.SelfServe.Infrastructure.Auditing;
using Sps.Apim.SelfServe.Infrastructure.Identity;
using Sps.Apim.SelfServe.Infrastructure.Persistence;

namespace Sps.Apim.SelfServe.Infrastructure.Authentication;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly AuditWriter _auditWriter;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext,
        AuditWriter auditWriter)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _auditWriter = auditWriter;
    }

    public async Task<bool> SignInAsync(
        SignInRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            await _auditWriter.WriteAsync(
                "SignInFailed",
                null,
                "Failed",
                correlationId,
                cancellationToken);

            return false;
        }

        if (!user.IsActive)
        {
            await _auditWriter.WriteAsync(
                "SignInFailed",
                user.Id,
                "Disabled",
                correlationId,
                cancellationToken);

            return false;
        }

        var result =
            await _signInManager.PasswordSignInAsync(
                user,
                request.Password,
                isPersistent: false,
                lockoutOnFailure: true);

        var eventType = result.Succeeded
            ? "SignInSucceeded"
            : result.IsLockedOut
                ? "AccountLocked"
                : "SignInFailed";

        var outcome = result.Succeeded
            ? "Succeeded"
            : result.IsLockedOut
                ? "LockedOut"
                : "Failed";

        await _auditWriter.WriteAsync(
            eventType,
            user.Id,
            outcome,
            correlationId,
            cancellationToken);

        return result.Succeeded;
    }

    public async Task SignOutAsync(
        Guid? userId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        await _signInManager.SignOutAsync();

        await _auditWriter.WriteAsync(
            "SignOutSucceeded",
            userId,
            "Succeeded",
            correlationId,
            cancellationToken);
    }

    public async Task<SessionResponse?> GetSessionAsync(
     ClaimsPrincipal principal,
     CancellationToken cancellationToken)
    {
        var user =
            await _userManager.GetUserAsync(principal);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var projects =
            await _dbContext.UserProjects
                .AsNoTracking()
                .Where(userProject =>
                    userProject.UserId == user.Id &&
                    userProject.Project.IsActive)
                .OrderBy(userProject =>
                    userProject.Project.Name)
                .Select(userProject =>
                    new ProjectItem(
                        userProject.ProjectId,
                        userProject.Project.Name))
                .ToListAsync(cancellationToken);

        return new SessionResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles.ToArray(),
            projects);
    }

    public async Task<bool> ChangePasswordAsync(
        ClaimsPrincipal principal,
        ChangePasswordRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.GetUserAsync(principal);

        if (user is null || !user.IsActive)
        {
            return false;
        }

        var result =
            await _userManager.ChangePasswordAsync(
                user,
                request.CurrentPassword,
                request.NewPassword);

        if (!result.Succeeded)
        {
            await _auditWriter.WriteAsync(
                "PasswordChangeFailed",
                user.Id,
                "Failed",
                correlationId,
                cancellationToken);

            return false;
        }

        await _userManager.UpdateSecurityStampAsync(user);

        await _signInManager.RefreshSignInAsync(user);

        await _auditWriter.WriteAsync(
            "PasswordChanged",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken);

        return true;
    }
}