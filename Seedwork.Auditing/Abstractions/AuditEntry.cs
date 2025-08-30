namespace Seedwork.Auditing.Abstractions;

public class AuditEntry<TEntity> : AuditEntryBase
    where TEntity : class, IAuditableEntity;