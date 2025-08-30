using System.Collections;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Seedwork.Auditing.Abstractions;

namespace Seedwork.Auditing.Core;

public class AuditingInterceptor(
    IAuditStore auditStore,
    SchemaDetectionService schemaService,
    AuditPropertyIgnoreConfiguration ignoreConfiguration,
    IServiceProvider serviceProvider) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return result;

        var context = eventData.Context;
        var entries = context.ChangeTracker.Entries()
            .Where(e => e is
            {
                Entity: IAuditableEntity,
                State: EntityState.Added
                or
                EntityState.Modified
                or
                EntityState.Deleted
            })
            .ToList();

        foreach (var entry in entries)
        {
            await HandleEntryAsync(entry, cancellationToken);
        }

        return result;
    }
    
    private async Task HandleEntryAsync(EntityEntry entry, CancellationToken cancellationToken)
    {
        var entityType = entry.Entity.GetType();
    
        var method = GetType()
            .GetMethod(nameof(CreateAuditEntry), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        var audit = method.Invoke(this, [entry])!;

        var auditEntryType = typeof(AuditEntry<>).MakeGenericType(entityType);
        var listType = typeof(List<>).MakeGenericType(auditEntryType);
        var list = (IList)Activator.CreateInstance(listType)!;
        list.Add(audit);

        var saveMethod = typeof(IAuditStore)
            .GetMethod(nameof(IAuditStore.SaveAsync))!
            .MakeGenericMethod(entityType);

        await (Task)saveMethod.Invoke(auditStore, [list, cancellationToken])!;
    }

    private AuditEntry<TEntity> CreateAuditEntry<TEntity>(EntityEntry entry)
        where TEntity : class, IAuditableEntity
    {
        var userId = GetCurrentUserId();
        var userEmail = GetCurrentUserEmail();
        var entityId = GetEntityId(entry);
        var entityInfo = schemaService.GetSchemaInfo(entry.Entity.GetType());
        
        var audit = new AuditEntry<TEntity>
        {
            EntityType = $"{entityInfo.Schema}.{entityInfo.TableName}",
            EntityId = entityId,
            UserId = userId,
            UserEmail = userEmail,
            CreatedAt = DateTime.UtcNow,
            Operation = entry.State,
            Changes = SerializeChanges(entry)
        };

        return audit;
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

    private string SerializeChanges(EntityEntry entry)
    {
        var entityType = entry.Entity.GetType();
        
        var changes = entry.Properties
            .Where(p =>
            {
                var propertyName = p.Metadata.Name;

                if (ShouldIgnoreProperty(entityType, propertyName))
                    return false;
                
                return p.IsModified ||
                       entry.State == EntityState.Added ||
                       entry.State == EntityState.Deleted;
            })
            .ToDictionary(
                p => p.Metadata.Name,
                p =>
                {
                    var oldValue = entry.State == EntityState.Added ? null : p.OriginalValue;
                    var newValue = entry.State == EntityState.Deleted ? null : p.CurrentValue;

                    return new PropertyChange(oldValue, newValue);
                });

        return JsonConvert.SerializeObject(changes, Formatting.None);;
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
    
    private bool ShouldIgnoreProperty(Type entityType, string propertyName)
    {
        if (ignoreConfiguration.ShouldIgnoreProperty(entityType, propertyName))
            return true;

        var propertyInfo = entityType.GetProperty(propertyName);
        if (propertyInfo?.GetCustomAttribute<AuditIgnoreAttribute>() != null)
            return true;

        if (propertyInfo?.GetCustomAttributes<Attribute>()
                .Any(attr => attr.GetType().Name.Contains("NotMapped") || 
                             attr.GetType().Name.Contains("JsonIgnore")) == true)
            return true;

        return false;
    }
}