namespace Seedwork.Auditing.Abstractions;

public interface IAuditStore
{
    Task SaveAuditEntriesAsync(IEnumerable<AuditEntry> entries, string schema, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditEntry>> GetAuditHistoryAsync(string entityType, string entityId, string schema, CancellationToken cancellationToken = default);
}