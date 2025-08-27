using Microsoft.EntityFrameworkCore;

namespace Seedwork.Auditing.Abstractions;

public sealed record AuditEntry
{
    public long Id { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required EntityState Operation { get; init; }
    public required string UserId { get; init; }
    public string? UserEmail { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string Changes { get; init; }
}