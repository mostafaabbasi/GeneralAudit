namespace Seedwork.Auditing.Abstractions;

public interface IAuditQueryService
{
    Task<IEnumerable<AuditEntry>> GetEntityHistoryAsync<T>(object entityId, CancellationToken cancellationToken = default) where T : class, IAuditableEntity;
}