using System.ComponentModel.DataAnnotations;

namespace Sps.Apim.SelfServe.Application.AccountManagement;

public sealed class CreateUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; }
        = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; init; }
        = string.Empty;

    [Required]
    public string Role { get; init; }
        = string.Empty;

    public IReadOnlyCollection<Guid> ProjectIds
    {
        get;
        init;
    } = [];

    [Required]
    [MaxLength(500)]
    public string Reason { get; init; }
        = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ApprovalReference { get; init; }
        = string.Empty;
}

public sealed class CreateUserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; } =
        string.Empty;

    public string DisplayName { get; init; } =
        string.Empty;

    public string Status { get; init; } =
        string.Empty;
}
public sealed class ReissueActivationRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; }
        = string.Empty;
}

public sealed class UserSummaryResponse
{
    public Guid Id { get; init; }

    public string DisplayName { get; init; } =
        string.Empty;

    public string Email { get; init; } =
        string.Empty;

    public string Status { get; init; } =
        string.Empty;

    public IReadOnlyCollection<string> Roles
    {
        get;
        init;
    }
    =
    [];
}



public sealed class ReissueActivationResponse
{
    public Guid UserId { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    // POC only.
    // Production should send this through
    // the notification service.
    public string ActivationUrl { get; init; }
        = string.Empty;
}

public sealed class ActivateAccountRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public string Token { get; init; }
        = string.Empty;

    [Required]
    [MinLength(12)]
    public string Password { get; init; }
        = string.Empty;
}


public sealed class RequestPasswordResetRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; }
        = string.Empty;
}

public sealed class RequestPasswordResetResponse
{
    // POC only.
    // Production should deliver this using the
    // approved notification/email mechanism.
    public string? ResetUrl { get; init; }

    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed class CompletePasswordResetRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public string Token { get; init; }
        = string.Empty;

    [Required]
    [MinLength(12)]
    public string NewPassword { get; init; }
        = string.Empty;
}