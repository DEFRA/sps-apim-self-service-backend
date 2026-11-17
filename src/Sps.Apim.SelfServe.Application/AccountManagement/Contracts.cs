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

public sealed class AccountStatusChangeRequest
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

    public IReadOnlyCollection<string> Projects { get; init; } = [];
}

public sealed class ProjectResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class AccountAccessHistoryResponse
{
    public string Action { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string ChangedBy { get; init; } = string.Empty;

    public DateTimeOffset ChangedAtUtc { get; init; }
}

public sealed class UserAccessResponse
{
    public Guid UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public bool IsLocked { get; init; }

    public string Role { get; init; } = string.Empty;

    public IReadOnlyCollection<Guid> ProjectIds { get; init; } = [];

    public IReadOnlyCollection<ProjectResponse> Projects { get; init; } = [];

    public IReadOnlyCollection<AccountAccessHistoryResponse> History { get; init; } = [];
}

public sealed class UpdateUserAccessRequest
{
    [Required]
    public string Role { get; init; } = string.Empty;

    public IReadOnlyCollection<Guid> ProjectIds { get; init; } = [];

    [Required]
    public string Status { get; init; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}


public sealed class ReissueActivationResponse
{
    public Guid UserId { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }
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

public sealed class AccessRequestRequest
{
    [Required]
    [MaxLength(100)]
    [RegularExpression(@".*\S.*")]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [RegularExpression(@".*\S.*")]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [RegularExpression(
        "^(Onboard a new API|Manage an existing API|Manage onboarding requests as an ICC administrator)$")]
    public string RequestType { get; init; } = string.Empty;

    [Required]
    [MaxLength(3000)]
    [RegularExpression(@".*\S.*")]
    public string Reason { get; init; } = string.Empty;
}

public sealed class AccessRequestResponse
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RequestType { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset SubmittedAtUtc { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? DecisionNote { get; init; }

    public string? AssignedRole { get; init; }

    public string? ProjectName { get; init; }

    public Guid? AccountUserId { get; init; }

    public DateTimeOffset? DecidedAtUtc { get; init; }

    public DateTimeOffset? DecisionEmailSentAtUtc { get; init; }
}

public sealed class AccessRequestDecisionRequest
{
    [Required]
    [RegularExpression("^(Approved|Rejected|MoreInformationRequested)$")]
    public string Outcome { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? Role { get; init; }

    public Guid? ProjectId { get; init; }

    [Required]
    [MaxLength(1000)]
    [RegularExpression(@".*\S.*")]
    public string DecisionNote { get; init; } = string.Empty;
}

public sealed class AdminDashboardSummaryResponse
{
    public int PortalUserCount { get; init; }

    public int AccessRequestsAwaitingReviewCount { get; init; }

    public int? ActiveOnboardingRequestsCount { get; init; }
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