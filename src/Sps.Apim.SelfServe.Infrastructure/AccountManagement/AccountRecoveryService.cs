using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Sps.Apim.SelfServe.Application.AccountManagement;
using Sps.Apim.SelfServe.Application.Notifications;

using Sps.Apim.SelfServe.Domain.Entities;
using Sps.Apim.SelfServe.Domain.Enums;

using Sps.Apim.SelfServe.Infrastructure.Auditing;
using Sps.Apim.SelfServe.Infrastructure.Identity;
using Sps.Apim.SelfServe.Infrastructure.Persistence;

namespace Sps.Apim.SelfServe.Infrastructure.AccountManagement;

public sealed class AccountRecoveryService
    : IAccountRecoveryService
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

    public AccountRecoveryService(
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

    // ---------------------------------------------------------
    // Account activation
    // ---------------------------------------------------------

    public async Task<bool> ActivateAsync(
        ActivateAccountRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.FindByIdAsync(
                request.UserId.ToString());

        if (user is null)
        {
            return false;
        }

        if (
            user.Status !=
            AccountStatus.PendingActivation)
        {
            return false;
        }

        var tokenHash =
            SecureTokenGenerator.Hash(
                request.Token);

        var token =
            await _dbContext.AccountTokens
                .SingleOrDefaultAsync(
                    x =>
                        x.UserId == user.Id &&
                        x.Type ==
                            AccountTokenType.Activation &&
                        x.TokenHash == tokenHash,
                    cancellationToken);

        if (token is null)
        {
            return false;
        }

        if (token.UsedAtUtc.HasValue)
        {
            return false;
        }

        if (
            token.ExpiresAtUtc <=
            DateTimeOffset.UtcNow)
        {
            return false;
        }

        var passwordResult =
            await _userManager.AddPasswordAsync(
                user,
                request.Password);

        if (!passwordResult.Succeeded)
        {
            return false;
        }

        var now =
            DateTimeOffset.UtcNow;

        user.Status =
            AccountStatus.Active;

        user.ActivatedAtUtc = now;

        user.UpdatedAtUtc = now;

        token.UsedAtUtc = now;

        var otherTokens =
            await _dbContext.AccountTokens
                .Where(x =>
                    x.UserId == user.Id &&
                    x.Type ==
                        AccountTokenType.Activation &&
                    x.Id != token.Id &&
                    x.UsedAtUtc == null)
                .ToListAsync(
                    cancellationToken);

        foreach (var otherToken in otherTokens)
        {
            otherToken.UsedAtUtc = now;
        }

        var updateResult =
            await _userManager.UpdateAsync(
                user);

        if (!updateResult.Succeeded)
        {
            return false;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await _auditWriter.WriteAsync(
            "ACCOUNT_ACTIVATED",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken);

        return true;
    }

    // ---------------------------------------------------------
    // Request password reset
    // ---------------------------------------------------------

    public async Task<RequestPasswordResetResponse>
        RequestPasswordResetAsync(
            RequestPasswordResetRequest request,
            string? correlationId,
            CancellationToken cancellationToken)
    {
        var email =
            request.Email.Trim();

        var user =
            await _userManager.FindByEmailAsync(
                email);

        // IMPORTANT:
        // Never reveal whether the account exists.

        if (
            user is null ||
            user.Status != AccountStatus.Active)
        {
            return new RequestPasswordResetResponse();
        }

        var now =
            DateTimeOffset.UtcNow;

        var previousTokens =
            await _dbContext.AccountTokens
                .Where(x =>
                    x.UserId == user.Id &&
                    x.Type ==
                        AccountTokenType.PasswordReset &&
                    x.UsedAtUtc == null)
                .ToListAsync(
                    cancellationToken);

        foreach (var previousToken in previousTokens)
        {
            previousToken.UsedAtUtc = now;
        }

        var rawToken =
            await _userManager
                .GeneratePasswordResetTokenAsync(
                    user);

        var tokenHash =
            SecureTokenGenerator.Hash(
                rawToken);

        var expiresAt =
            now.AddMinutes(
                _options
                    .PasswordResetTokenLifetimeMinutes);

        var accountToken =
            new AccountToken
            {
                Id = Guid.NewGuid(),

                UserId = user.Id,

                Type =
                    AccountTokenType.PasswordReset,

                TokenHash =
                    tokenHash,

                CreatedAtUtc = now,

                ExpiresAtUtc = expiresAt
            };

        _dbContext.AccountTokens.Add(
            accountToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await _auditWriter.WriteAsync(
            "PASSWORD_RESET_REQUESTED",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken);

        var resetUrl =
            BuildPasswordResetUrl(
                user.Id,
                rawToken);

        await _notifications
            .SendPasswordResetEmailAsync(
                user.Email!,
                resetUrl,
                cancellationToken);

        return new RequestPasswordResetResponse();
    }

    // ---------------------------------------------------------
    // Complete password reset
    // ---------------------------------------------------------

    public async Task<bool> ResetPasswordAsync(
        CompletePasswordResetRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.FindByIdAsync(
                request.UserId.ToString());

        if (user is null)
        {
            return false;
        }

        if (
            user.Status !=
            AccountStatus.Active)
        {
            return false;
        }

        var tokenHash =
            SecureTokenGenerator.Hash(
                request.Token);

        var accountToken =
            await _dbContext.AccountTokens
                .SingleOrDefaultAsync(
                    x =>
                        x.UserId == user.Id &&
                        x.Type ==
                            AccountTokenType.PasswordReset &&
                        x.TokenHash == tokenHash,
                    cancellationToken);

        if (accountToken is null)
        {
            return false;
        }

        if (accountToken.UsedAtUtc.HasValue)
        {
            return false;
        }

        if (
            accountToken.ExpiresAtUtc <=
            DateTimeOffset.UtcNow)
        {
            return false;
        }

        var resetResult =
            await _userManager.ResetPasswordAsync(
                user,
                request.Token,
                request.NewPassword);

        if (!resetResult.Succeeded)
        {
            await _auditWriter.WriteAsync(
                "PASSWORD_RESET_FAILED",
                user.Id,
                "Failed",
                correlationId,
                cancellationToken);

            return false;
        }

        var now =
            DateTimeOffset.UtcNow;

        accountToken.UsedAtUtc = now;

        user.UpdatedAtUtc = now;

        var otherTokens =
            await _dbContext.AccountTokens
                .Where(x =>
                    x.UserId == user.Id &&
                    x.Type ==
                        AccountTokenType.PasswordReset &&
                    x.Id != accountToken.Id &&
                    x.UsedAtUtc == null)
                .ToListAsync(
                    cancellationToken);

        foreach (var otherToken in otherTokens)
        {
            otherToken.UsedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await _userManager.UpdateSecurityStampAsync(
            user);

        await _auditWriter.WriteAsync(
            "PASSWORD_RESET_COMPLETED",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken);

        await _auditWriter.WriteAsync(
            "SESSIONS_REVOKED",
            user.Id,
            "Succeeded",
            correlationId,
            cancellationToken);

        return true;
    }

    // ---------------------------------------------------------
    // URL helpers
    // ---------------------------------------------------------

    private string BuildPasswordResetUrl(
        Guid userId,
        string token)
    {
        var baseUrl =
            _options.PortalBaseUrl
                .TrimEnd('/');

        return
            $"{baseUrl}/reset-password" +
            $"?userId={Uri.EscapeDataString(userId.ToString())}" +
            $"&token={Uri.EscapeDataString(token)}";
    }
}