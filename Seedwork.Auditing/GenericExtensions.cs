namespace Seedwork.Auditing;

public static class GenericExtensions
{
    private const string AuditTableExtension = "_Audit";
    private const string AuditEntryExtension = "AuditEntry";
    public static string? ToAuditTableName<T>(this T tableName)
        => tableName is null ? null : $"{tableName}{AuditTableExtension}";
    
    public static string? ToAuditEntryName<T>(this T entityName)
        => entityName is null ? null : $"{entityName}{AuditEntryExtension}";
}