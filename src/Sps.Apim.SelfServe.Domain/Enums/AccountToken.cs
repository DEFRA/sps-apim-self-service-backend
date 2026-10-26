using Sps.Apim.SelfServe.Domain.Enums;

namespace Sps.Apim.SelfServe.Domain.Entities;

public sealed class AccountToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AccountTokenType Type { get; set; }
    public string TokenHash { get; set; }
        = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public Guid? IssuedByUserId { get; set; }
    public bool IsUsed =>
        UsedAtUtc.HasValue;
}