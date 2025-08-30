namespace Seedwork.Auditing.Abstractions;

public interface IAuditStore
{
    Task SaveAsync<TEntity>(List<AuditEntry<TEntity>> auditEntries, CancellationToken cancellationToken)
        where TEntity : class, IAuditableEntity;
}