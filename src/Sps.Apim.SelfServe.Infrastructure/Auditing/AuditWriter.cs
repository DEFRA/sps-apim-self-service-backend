using Sps.Apim.SelfServe.Domain.Entities; using Sps.Apim.SelfServe.Infrastructure.Persistence;
namespace Sps.Apim.SelfServe.Infrastructure.Auditing;
public sealed class AuditWriter(ApplicationDbContext db){public async Task WriteAsync(string type,Guid? userId,string outcome,string? correlationId,CancellationToken ct){db.AuditEvents.Add(new AuditEvent{EventType=type,UserId=userId,Outcome=outcome,CorrelationId=correlationId});await db.SaveChangesAsync(ct);}}
