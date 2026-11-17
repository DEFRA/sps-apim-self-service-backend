using Sps.Apim.SelfServe.Domain.Constants;

namespace Sps.Apim.SelfServe.Domain.Entities;

public sealed class AccessRequest
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string RequestType { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Status { get; set; } = AccessRequestStatuses.AwaitingReview;

    public string? DecisionNote { get; set; }

    public string? AssignedRole { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? AccountUserId { get; set; }

    public Guid? DecidedByUserId { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    public DateTimeOffset? DecisionEmailSentAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
