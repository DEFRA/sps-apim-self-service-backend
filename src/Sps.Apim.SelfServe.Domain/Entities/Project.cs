namespace Sps.Apim.SelfServe.Domain.Entities;
public sealed class Project { public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = string.Empty; public bool IsActive { get; set; } = true; public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow; }
