using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seedwork.Auditing.Abstractions;

namespace Seedwork.Auditing.Core;

public sealed class AuditQueryService(
    IAuditStore auditStore,
    SchemaDetectionService schemaService)
    : IAuditQueryService
{
    public async Task<IEnumerable<AuditEntry>> GetEntityHistoryAsync<T>(
        object entityId, 
        CancellationToken cancellationToken = default) where T : class, IAuditableEntity
    {
        var entityType = typeof(T);
        var schemaInfo = schemaService.GetSchemaInfo(entityType);
        
        return await auditStore.GetAuditHistoryAsync(
            $"{schemaInfo.Schema}.{schemaInfo.EntityName}",
            entityId.ToString()!,
            schemaInfo.Schema,
            cancellationToken);
    }
}