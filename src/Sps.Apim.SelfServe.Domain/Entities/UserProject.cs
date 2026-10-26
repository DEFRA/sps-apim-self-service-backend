namespace Sps.Apim.SelfServe.Domain.Entities;
public sealed class UserProject { public Guid UserId { get; set; } public Guid ProjectId { get; set; } public Project Project { get; set; } = null!; }
