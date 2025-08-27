using Microsoft.EntityFrameworkCore;

namespace Seedwork.Auditing.Core;

public static class AuditDbContextRegistry
{
    private static Dictionary<string, Type> SchemaDbContextMap = new();

    public static void Register<TDbContext>(string? schema = null) where TDbContext : DbContext
    {
        var key = schema ?? typeof(TDbContext).Name;
        SchemaDbContextMap[key] = typeof(TDbContext);
    }

    public static Type GetDbContextTypeForSchema(string schema)
    {
        return SchemaDbContextMap.TryGetValue(schema, out var type) ? type : throw new InvalidOperationException($"No DbContext registered for schema '{schema}'");
    }
}