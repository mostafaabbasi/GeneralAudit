using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Seedwork.Auditing.Abstractions;

namespace Seedwork.Auditing.Core;

public sealed class AuditingInterceptor(
    SchemaDetectionService schemaService,
    IAuditStore auditStore,
    IServiceProvider serviceProvider)
    : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return result;
        
        var auditEntries = await CreateAuditEntries(eventData.Context);
        
        var groupedEntries = auditEntries.GroupBy(entry => 
            AuditTableRegistry.GetSchema(entry.EntityType) ?? "dbo");
        
        foreach (var schemaGroup in groupedEntries)
        {
            await auditStore.SaveAuditEntriesAsync(
                schemaGroup.AsEnumerable(), 
                schemaGroup.Key,    
                cancellationToken);
        }
        
        return result;
    }
    
    private async Task<List<AuditEntry>> CreateAuditEntries(DbContext context)
    {
        var auditEntries = new List<AuditEntry>();
        var userId = GetCurrentUserId();
        var userEmail = GetCurrentUserEmail();
        
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IAuditableEntity ||
                entry.State == EntityState.Unchanged)
                continue;
            
            var auditEntry = CreateAuditEntryFromEntityEntry(entry, userId, userEmail);
            if (auditEntry != null)
            {
                auditEntries.Add(auditEntry);
            }
        }
        
        return auditEntries;
    }
    
    private AuditEntry? CreateAuditEntryFromEntityEntry(EntityEntry entry, string userId, string userEmail)
    {
        var entityType = entry.Entity.GetType();
        var entityId = GetEntityId(entry);
        var operation = entry.State;
        
        var changes = new Dictionary<string, PropertyChange>();
        
        foreach (var property in entry.Properties)
        {
            var propertyName = property.Metadata.Name;

            switch (operation)
            {
                case EntityState.Added:
                {
                    if (property.CurrentValue != null)
                    {
                        changes[propertyName] = new PropertyChange(OldValue: null, NewValue: property.CurrentValue);
                    }

                    break;
                }
                case EntityState.Deleted:
                {
                    if (property.OriginalValue != null)
                    {
                        changes[propertyName] = new PropertyChange(property.OriginalValue, null);
                    }

                    break;
                }
                case EntityState.Modified when property.IsModified:
                    changes[propertyName] = new PropertyChange(property.OriginalValue, property.CurrentValue);
                    break;
            }
        }
        
        if (!changes.Any())
            return null;

        var serializedChanges = JsonConvert.SerializeObject(changes, Formatting.None);

        var entityInfo = schemaService.GetSchemaInfo(entityType);
        
        var auditEntry = new AuditEntry
        {
            EntityType = $"{entityInfo.Schema}.{entityInfo.TableName}",
            EntityId = entityId,
            Operation = operation,
            UserId = userId,
            UserEmail = userEmail,
            CreatedAt = DateTime.UtcNow,
            Changes = serializedChanges.Trim(),
        };
        
        return auditEntry; 
    }
    
    private string GetEntityId(EntityEntry entry)
    {
        var keyProperties = entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).ToList();
        
        if (keyProperties.Count == 1)
        {
            return keyProperties[0].CurrentValue?.ToString() ?? string.Empty;
        }
        
        var keyValues = keyProperties.Select(p => p.CurrentValue?.ToString() ?? "null");
        return string.Join("|", keyValues);
    }
    
    private string GetCurrentUserId()
    {
        try
        {
            var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
            var userId = httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userId ?? "system";
        }
        catch
        {
            return "system";
        }
    }
    
    private string GetCurrentUserEmail()
    {
        try
        {
            var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
            var userEmail = httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
            return userEmail ?? "system";
        }
        catch
        {
            return "system";
        }
    }
}