using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seedwork.Auditing.Abstractions;
using Seedwork.Auditing.Core;

namespace Seedwork.Auditing;

public static class AuditingServiceCollectionExtensions
{
    public static IServiceCollection AddSchemaAwareAuditing(this IServiceCollection services, Action<AuditPropertyIgnoreConfiguration>? configureIgnores = null)
    {
        services.AddSingleton<SchemaDetectionService>();
        services.AddScoped<IAuditStore, SchemaAwareAuditStore>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<AuditingInterceptor>();
        
        var ignoreConfig = new AuditPropertyIgnoreConfiguration();
        configureIgnores?.Invoke(ignoreConfig);
        services.AddSingleton(ignoreConfig);
        
        return services;
    }
    
    public static DbContextOptionsBuilder AddAuditing<TDbContext>(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider,
        string schema = "dbo")
        where TDbContext : DbContext
    {
        var interceptor = serviceProvider.GetRequiredService<AuditingInterceptor>();
        AuditDbContextRegistry.Register<TDbContext>(schema);
        return builder.AddInterceptors(interceptor);
    }
}