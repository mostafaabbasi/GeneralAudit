namespace Seedwork.Auditing.Abstractions;

[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditIgnoreAttribute : Attribute
{
    public string? Reason { get; init; }
}