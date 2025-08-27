using System.Collections.Concurrent;
using System.Linq.Expressions;
using Seedwork.Auditing.Abstractions;

namespace Seedwork.Auditing.Core;

public sealed class AuditPropertyIgnoreConfiguration
{
    private readonly ConcurrentDictionary<Type, HashSet<string>> _ignoredProperties = new();
    private readonly ConcurrentDictionary<Type, Func<object, bool>> _conditionalIgnores = new();
    
    public AuditPropertyIgnoreConfiguration IgnoreProperties<T>(params Expression<Func<T, object>>[] propertyExpressions)
        where T : class, IAuditableEntity
    {
        var entityType = typeof(T);
        var propertyNames = propertyExpressions.Select(GetPropertyName).ToArray();
        
        _ignoredProperties.AddOrUpdate(entityType, 
            new HashSet<string>(propertyNames, StringComparer.OrdinalIgnoreCase),
            (_, existing) => 
            {
                foreach (var prop in propertyNames)
                    existing.Add(prop);
                return existing;
            });

        return this;
    }
    
    public AuditPropertyIgnoreConfiguration IgnoreWhen<T>(Func<T, bool> condition)
        where T : class, IAuditableEntity
    {
        _conditionalIgnores.TryAdd(typeof(T), entity => condition((T)entity));
        return this;
    }
    
    public AuditPropertyIgnoreConfiguration IgnorePropertiesWithAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return this;
    }

    public bool ShouldIgnoreProperty(Type entityType, string propertyName)
    {
        return _ignoredProperties.TryGetValue(entityType, out var ignored) && 
               ignored.Contains(propertyName);
    }

    public bool ShouldIgnoreEntity(object entity)
    {
        return _conditionalIgnores.TryGetValue(entity.GetType(), out var condition) && 
               condition(entity);
    }

    private static string GetPropertyName<T>(Expression<Func<T, object>> propertyExpression)
    {
        return propertyExpression.Body switch
        {
            MemberExpression member => member.Member.Name,
            UnaryExpression { Operand: MemberExpression memberExpr } => memberExpr.Member.Name,
            _ => throw new ArgumentException("Invalid property expression", nameof(propertyExpression))
        };
    }
}