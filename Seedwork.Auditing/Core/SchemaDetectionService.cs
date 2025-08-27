using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace Seedwork.Auditing.Core;

public sealed class SchemaDetectionService
{
    private readonly ConcurrentDictionary<Type, SchemaInfo> _schemaCache = new();
    
    public SchemaInfo GetSchemaInfo(Type entityType)
    {
        return _schemaCache.GetOrAdd(entityType, type => 
        {
            var tempModelBuilder = new ModelBuilder();
            
            _ = tempModelBuilder.Entity(type);
            
            var model = tempModelBuilder.FinalizeModel();
            var entityTypeInfo = model.FindEntityType(type);
            
            if (entityTypeInfo == null)
            {
                throw new InvalidOperationException($"Entity type {type.Name} is not configured in the model");
            }
            
            var schema = entityTypeInfo.GetSchema() ?? "dbo";
            var tableName = entityTypeInfo.GetTableName() ?? type.Name;
            
            return new SchemaInfo(schema, tableName, type.Name);
        });
    }
    
    public SchemaInfo GetSchemaInfo(Type entityType, DbContext context)
    {
        return _schemaCache.GetOrAdd(entityType, type => 
        {
            var findEntityType = context.Model.FindEntityType(type);
            if (findEntityType == null)
            {
                throw new InvalidOperationException($"Entity type {type.Name} is not registered in DbContext");
            }
            
            var schema = findEntityType.GetSchema() ?? "dbo";
            var tableName = findEntityType.GetTableName() ?? type.Name;
            
            return new SchemaInfo(schema, tableName, type.Name);
        });
    }
}