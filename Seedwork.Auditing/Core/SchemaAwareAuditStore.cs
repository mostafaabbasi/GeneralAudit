using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seedwork.Auditing.Abstractions;

namespace Seedwork.Auditing.Core;

public class SchemaAwareAuditStore(IServiceProvider serviceProvider, SchemaDetectionService schemaService) : IAuditStore
{
    public async Task SaveAsync<TEntity>(List<AuditEntry<TEntity>> auditEntries, CancellationToken cancellationToken)
        where TEntity : class, IAuditableEntity
    {
        if (!auditEntries.Any())
            return;
        
        foreach (var auditEntry in auditEntries)
        {
            var schema = AuditTableRegistry.GetSchema(auditEntry.EntityType);
            
            if(string.IsNullOrWhiteSpace(schema))
                continue;
            
            var dbContext = GetDbContextForSchema(schema);
            await dbContext.Set<AuditEntry<TEntity>>().AddAsync(auditEntry, cancellationToken);
        }
    }

    private DbContext GetDbContextForSchema(string schema)
    {
        var dbContextType = AuditDbContextRegistry.GetDbContextTypeForSchema(schema);
        return (DbContext)serviceProvider.GetRequiredService(dbContextType);
    }
}

// public sealed class SchemaAwareAuditStore(IServiceProvider serviceProvider, ILogger<SchemaAwareAuditStore> logger)
//     : IAuditStore
// {
//     public async Task SaveAuditEntriesAsync(
//         IEnumerable<AuditEntry> entries, 
//         string schema, 
//         CancellationToken cancellationToken = default)
//     {
//         var dbContext = GetDbContextForSchema(schema);
//         
//         try
//         {
//             foreach (var entry in entries)
//             {
//                 var entityType = AuditTableRegistry.ResolveType(entry.EntityType);
//                 if (entityType == null) continue;
//                 
//                 var mapping = AuditTableRegistry.GetMapping(entityType);
//                 
//                 if (mapping == null) continue;
//                 
//                 await dbContext.Database.ExecuteSqlRawAsync(
//                     $@"INSERT INTO [{mapping.Value.Schema}].[{mapping.Value.TableName}] 
//        (EntityType, EntityId, Operation, UserId, UserEmail, CreatedAt, Changes)
//        VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
//                     entry.EntityType,
//                     entry.EntityId,
//                     entry.Operation.ToString(),
//                     entry.UserId,
//                     entry.UserEmail,
//                     entry.CreatedAt,
//                     entry.Changes
//                 );
//
//             }
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Failed to save audit entries to schema {Schema}", schema);
//             throw;
//         }
//     }
//     
//     public async Task<IEnumerable<AuditEntry>> GetAuditHistoryAsync(
//     string entityType,
//     string entityId,
//     string schema,
//     CancellationToken cancellationToken = default)
// {
//     var dbContext = GetDbContextForSchema(schema);
//     
//     var type = AuditTableRegistry.ResolveType(entityType);
//
//     if (type is null)
//         return [];
//     
//     var mapping = AuditTableRegistry.GetMapping(type!);
//
//     if (mapping == null)
//         return [];
//
//     const string sqlTemplate = @"
//         SELECT Id, EntityType, EntityId, Operation, UserId, UserEmail, CreatedAt, Changes
//         FROM [{0}].[{1}]
//         WHERE EntityId = @entityId
//         ORDER BY CreatedAt DESC";
//
//     var sql = string.Format(sqlTemplate, mapping.Value.Schema, mapping.Value.TableName);
//
//     try
//     {
//         await using var connection = dbContext.Database.GetDbConnection();
//         await connection.OpenAsync(cancellationToken);
//
//         await using var command = connection.CreateCommand();
//         command.CommandText = sql;
//
//         var entityIdParam = command.CreateParameter();
//         entityIdParam.ParameterName = "@entityId";
//         entityIdParam.Value = entityId;
//         command.Parameters.Add(entityIdParam);
//
//         var auditEntries = new List<AuditEntry>();
//
//         await using var reader = await command.ExecuteReaderAsync(cancellationToken);
//
//         var idIndex = reader.GetOrdinal("Id");
//         var entityTypeIndex = reader.GetOrdinal("EntityType");
//         var entityIdIndex = reader.GetOrdinal("EntityId");
//         var operationIndex = reader.GetOrdinal("Operation");
//         var userIdIndex = reader.GetOrdinal("UserId");
//         var userEmailIndex = reader.GetOrdinal("UserEmail");
//         var createdAtIndex = reader.GetOrdinal("CreatedAt");
//         var changesIndex = reader.GetOrdinal("Changes");
//
//         while (await reader.ReadAsync(cancellationToken))
//         {
//             var auditEntry = new AuditEntry
//             {
//                 Id = reader.GetInt64(idIndex),
//                 EntityType = reader.GetString(entityTypeIndex),
//                 EntityId = reader.GetString(entityIdIndex),
//                 Operation = Enum.Parse<EntityState>(reader.GetString(operationIndex)),
//                 UserId = reader.GetString(userIdIndex),
//                 UserEmail = reader.IsDBNull(userEmailIndex) ? null : reader.GetString(userEmailIndex),
//                 CreatedAt = reader.GetDateTime(createdAtIndex),
//                 Changes = reader.GetString(changesIndex),
//             };
//
//             auditEntries.Add(auditEntry);
//         }
//
//         return auditEntries;
//     }
//     catch (Exception ex)
//     {
//         logger.LogError(ex, "Failed to retrieve audit history for {EntityType} {EntityId} in schema {Schema}",
//             entityType, entityId, schema);
//         throw;
//     }
// }
//
//     
//     private DbContext GetDbContextForSchema(string schema)
//     {
//         var dbContextType = AuditDbContextRegistry.GetDbContextTypeForSchema(schema);
//         return (DbContext)serviceProvider.GetRequiredService(dbContextType);
//     }
// }


// public sealed class SchemaAwareAuditStore(IServiceProvider serviceProvider, ILogger<SchemaAwareAuditStore> logger)
//     : IAuditStore
// {
//     public async Task SaveAuditEntriesAsync(
//         IEnumerable<AuditEntry> entries, 
//         string schema, 
//         CancellationToken cancellationToken = default)
//     {
//         var dbContext = GetDbContextForSchema(schema);
//         
//         try
//         {
//             foreach (var entry in entries)
//             {
//                 var entityType = AuditTableRegistry.ResolveType(entry.EntityType);
//                 if (entityType == null) continue;
//                 
//                 var mapping = AuditTableRegistry.GetMapping(entityType);
//                 if (mapping == null) continue;
//
//                 // For multiple tables approach, use the shared type entity
//                 var auditEntityName = entityType.Name.ToAuditEntryName();
//                 
//                 // Try to get the shared type entity configuration
//                 var entityTypeConfig = dbContext.Model.FindEntityType(auditEntityName);
//                 
//                 if (entityTypeConfig != null)
//                 {
//                     // Use the shared type entity
//                     var auditEntry = new AuditEntry
//                     {
//                         EntityType = entry.EntityType,
//                         EntityId = entry.EntityId,
//                         Operation = entry.Operation,
//                         UserId = entry.UserId,
//                         UserEmail = entry.UserEmail,
//                         CreatedAt = entry.CreatedAt,
//                         Changes = entry.Changes
//                     };
//
//                     dbContext.Set<AuditEntry>(auditEntityName).Add(auditEntry);
//                 }
//                 else
//                 {
//                     // Fallback to raw SQL if shared type entity is not found
//                     await dbContext.Database.ExecuteSqlRawAsync(
//                         $@"INSERT INTO [{mapping.Value.Schema}].[{mapping.Value.TableName}] 
//            (EntityType, EntityId, Operation, UserId, UserEmail, CreatedAt, Changes)
//            VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
//                         entry.EntityType,
//                         entry.EntityId,
//                         entry.Operation.ToString(),
//                         entry.UserId,
//                         entry.UserEmail,
//                         entry.CreatedAt,
//                         entry.Changes
//                     );
//                 }
//             }
//
//             await dbContext.SaveChangesAsync(cancellationToken);
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Failed to save audit entries to schema {Schema}", schema);
//             throw;
//         }
//     }
//     
//     public async Task<IEnumerable<AuditEntry>> GetAuditHistoryAsync(
//         string entityType,
//         string entityId,
//         string schema,
//         CancellationToken cancellationToken = default)
//     {
//         var dbContext = GetDbContextForSchema(schema);
//         
//         var type = AuditTableRegistry.ResolveType(entityType);
//         if (type is null) return [];
//         
//         var mapping = AuditTableRegistry.GetMapping(type);
//         if (mapping == null) return [];
//
//         try
//         {
//             // Try to use shared type entity first
//             var auditEntityName = $"{type.Name}AuditEntry";
//             var entityTypeConfig = dbContext.Model.FindEntityType(auditEntityName);
//             
//             if (entityTypeConfig != null)
//             {
//                 return await dbContext.Set<AuditEntry>(auditEntityName)
//                     .Where(a => a.EntityId == entityId)
//                     .OrderByDescending(a => a.CreatedAt)
//                     .ToListAsync(cancellationToken);
//             }
//             
//             // Fallback to raw SQL
//             return await GetAuditHistoryWithRawSql(dbContext, mapping.Value, entityId, cancellationToken);
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Failed to retrieve audit history for {EntityType} {EntityId} in schema {Schema}",
//                 entityType, entityId, schema);
//             throw;
//         }
//     }
//
//     private async Task<IEnumerable<AuditEntry>> GetAuditHistoryWithRawSql(
//         DbContext dbContext, 
//         (string Schema, string TableName) mapping, 
//         string entityId, 
//         CancellationToken cancellationToken)
//     {
//         const string sqlTemplate = @"
//             SELECT Id, EntityType, EntityId, Operation, UserId, UserEmail, CreatedAt, Changes
//             FROM [{0}].[{1}]
//             WHERE EntityId = @entityId
//             ORDER BY CreatedAt DESC";
//
//         var sql = string.Format(sqlTemplate, mapping.Schema, mapping.TableName);
//
//         await using var connection = dbContext.Database.GetDbConnection();
//         await connection.OpenAsync(cancellationToken);
//
//         await using var command = connection.CreateCommand();
//         command.CommandText = sql;
//
//         var entityIdParam = command.CreateParameter();
//         entityIdParam.ParameterName = "@entityId";
//         entityIdParam.Value = entityId;
//         command.Parameters.Add(entityIdParam);
//
//         var auditEntries = new List<AuditEntry>();
//
//         await using var reader = await command.ExecuteReaderAsync(cancellationToken);
//
//         var idIndex = reader.GetOrdinal("Id");
//         var entityTypeIndex = reader.GetOrdinal("EntityType");
//         var entityIdIndex = reader.GetOrdinal("EntityId");
//         var operationIndex = reader.GetOrdinal("Operation");
//         var userIdIndex = reader.GetOrdinal("UserId");
//         var userEmailIndex = reader.GetOrdinal("UserEmail");
//         var createdAtIndex = reader.GetOrdinal("CreatedAt");
//         var changesIndex = reader.GetOrdinal("Changes");
//
//         while (await reader.ReadAsync(cancellationToken))
//         {
//             var auditEntry = new AuditEntry
//             {
//                 Id = reader.GetInt64(idIndex),
//                 EntityType = reader.GetString(entityTypeIndex),
//                 EntityId = reader.GetString(entityIdIndex),
//                 Operation = Enum.Parse<EntityState>(reader.GetString(operationIndex)),
//                 UserId = reader.GetString(userIdIndex),
//                 UserEmail = reader.IsDBNull(userEmailIndex) ? null : reader.GetString(userEmailIndex),
//                 CreatedAt = reader.GetDateTime(createdAtIndex),
//                 Changes = reader.GetString(changesIndex),
//             };
//
//             auditEntries.Add(auditEntry);
//         }
//
//         return auditEntries;
//     }
//     
//     private DbContext GetDbContextForSchema(string schema)
//     {
//         var dbContextType = AuditDbContextRegistry.GetDbContextTypeForSchema(schema);
//         return (DbContext)serviceProvider.GetRequiredService(dbContextType);
//     }
// }