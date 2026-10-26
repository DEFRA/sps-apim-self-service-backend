using Microsoft.AspNetCore.Identity;
using Sps.Apim.SelfServe.Domain.Enums;

namespace Sps.Apim.SelfServe.Infrastructure.Identity;

public sealed class ApplicationUser
    : IdentityUser<Guid>
{
    public string DisplayName { get; set; }
        = string.Empty;
    public AccountStatus Status { get; set; }
        = AccountStatus.PendingActivation;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ActivatedAtUtc { get; set; }
    public DateTimeOffset? DisabledAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public bool IsActive =>
        Status == AccountStatus.Active;
}