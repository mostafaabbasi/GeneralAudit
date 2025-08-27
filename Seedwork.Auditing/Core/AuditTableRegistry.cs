using System.Collections.Concurrent;

namespace Seedwork.Auditing.Core;

public static class AuditTableRegistry
{
    private static readonly ConcurrentDictionary<Type, (string Schema, string TableName)> Mappings = new();
    
    public static void RegisterMapping(Type entityType, string schema, string tableName)
    {
        Mappings.TryAdd(entityType, (schema, tableName));
    }
    
    public static (string Schema, string TableName)? GetMapping(Type entityType)
    {
        return Mappings.TryGetValue(entityType, out var mapping) ? mapping : null;
    }

    public static (string Schema, string TableName)? GetMainInfo(Type entityType)
    {
        var isExist = Mappings.TryGetValue(entityType, out var mapping);
        
        if(!isExist) return null;

        return (mapping.Schema, entityType.Name);
    }
    
    public static string? GetSchema(Type entityType) =>  Mappings.TryGetValue(entityType, out var mapping) ? mapping.Schema : null;

    public static string? GetSchema(string schemaAndName)
    {
        foreach (var kv in Mappings)
        {
            var expected = $"{kv.Value.Schema}.{kv.Key.Name}";
            if (expected.Equals(schemaAndName, StringComparison.OrdinalIgnoreCase))
                return kv.Value.Schema;
        }
        return null;
    }
    
    public static Type? ResolveType(string schemaAndName)
    {
        foreach (var kv in Mappings)
        {
            var expected = $"{kv.Value.Schema}.{kv.Key.Name}";
            if (expected.Equals(schemaAndName, StringComparison.OrdinalIgnoreCase))
                return kv.Key;
        }
        return null;
    }
}