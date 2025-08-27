using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seedwork.Auditing.Abstractions;

namespace Seedwork.Auditing.Core;

public sealed class SchemaAwareAuditStore(IServiceProvider serviceProvider, ILogger<SchemaAwareAuditStore> logger)
    : IAuditStore
{
    public async Task SaveAuditEntriesAsync(
        IEnumerable<AuditEntry> entries, 
        string schema, 
        CancellationToken cancellationToken = default)
    {
        var dbContext = GetDbContextForSchema(schema);
        
        try
        {
            foreach (var entry in entries)
            {
                var entityType = AuditTableRegistry.ResolveType(entry.EntityType);
                if (entityType == null) continue;
                
                var mapping = AuditTableRegistry.GetMapping(entityType);
                
                if (mapping == null) continue;
                
                await dbContext.Database.ExecuteSqlRawAsync(
                    $@"INSERT INTO [{mapping.Value.Schema}].[{mapping.Value.TableName}] 
       (EntityType, EntityId, Operation, UserId, UserEmail, CreatedAt, Changes)
       VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
                    entry.EntityType,
                    entry.EntityId,
                    entry.Operation.ToString(),
                    entry.UserId,
                    entry.UserEmail,
                    entry.CreatedAt,
                    entry.Changes
                );

            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save audit entries to schema {Schema}", schema);
            throw;
        }
    }
    
    public async Task<IEnumerable<AuditEntry>> GetAuditHistoryAsync(
    string entityType,
    string entityId,
    string schema,
    CancellationToken cancellationToken = default)
{
    var dbContext = GetDbContextForSchema(schema);
    
    var type = AuditTableRegistry.ResolveType(entityType);

    if (type is null)
        return [];
    
    var mapping = AuditTableRegistry.GetMapping(type!);

    if (mapping == null)
        return [];

    const string sqlTemplate = @"
        SELECT Id, EntityType, EntityId, Operation, UserId, UserEmail, CreatedAt, Changes
        FROM [{0}].[{1}]
        WHERE EntityId = @entityId
        ORDER BY CreatedAt DESC";

    var sql = string.Format(sqlTemplate, mapping.Value.Schema, mapping.Value.TableName);

    try
    {
        await using var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var entityIdParam = command.CreateParameter();
        entityIdParam.ParameterName = "@entityId";
        entityIdParam.Value = entityId;
        command.Parameters.Add(entityIdParam);

        var auditEntries = new List<AuditEntry>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var idIndex = reader.GetOrdinal("Id");
        var entityTypeIndex = reader.GetOrdinal("EntityType");
        var entityIdIndex = reader.GetOrdinal("EntityId");
        var operationIndex = reader.GetOrdinal("Operation");
        var userIdIndex = reader.GetOrdinal("UserId");
        var userEmailIndex = reader.GetOrdinal("UserEmail");
        var createdAtIndex = reader.GetOrdinal("CreatedAt");
        var changesIndex = reader.GetOrdinal("Changes");

        while (await reader.ReadAsync(cancellationToken))
        {
            var auditEntry = new AuditEntry
            {
                Id = reader.GetInt64(idIndex),
                EntityType = reader.GetString(entityTypeIndex),
                EntityId = reader.GetString(entityIdIndex),
                Operation = Enum.Parse<EntityState>(reader.GetString(operationIndex)),
                UserId = reader.GetString(userIdIndex),
                UserEmail = reader.IsDBNull(userEmailIndex) ? null : reader.GetString(userEmailIndex),
                CreatedAt = reader.GetDateTime(createdAtIndex),
                Changes = reader.GetString(changesIndex),
            };

            auditEntries.Add(auditEntry);
        }

        return auditEntries;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to retrieve audit history for {EntityType} {EntityId} in schema {Schema}",
            entityType, entityId, schema);
        throw;
    }
}

    
    private DbContext GetDbContextForSchema(string schema)
    {
        var dbContextType = AuditDbContextRegistry.GetDbContextTypeForSchema(schema);
        return (DbContext)serviceProvider.GetRequiredService(dbContextType);
    }
}