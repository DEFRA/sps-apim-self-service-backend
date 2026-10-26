namespace Sps.Apim.SelfServe.Domain.Entities;
public sealed class AuditEvent { public long Id { get; set; } public string EventType { get; set; } = string.Empty; public Guid? UserId { get; set; } public string Outcome { get; set; } = string.Empty; public string? CorrelationId { get; set; } public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow; }
