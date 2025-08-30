using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Seedwork.Auditing.Abstractions;

namespace Seedwork.Auditing.Core;

public static class AuditingModelBuilderExtensions
{
    public static ModelBuilder EnableAuditing(this ModelBuilder modelBuilder)
    {
        var auditableTypes = modelBuilder.Model
            .GetEntityTypes()
            .Select(et => et.ClrType)
            .Where(t => typeof(IAuditableEntity).IsAssignableFrom(t))
            .ToList();

        foreach (var entityType in auditableTypes)
        {
            ConfigureAuditTable(modelBuilder, entityType);
        }

        return modelBuilder;
    }

    private static void ConfigureAuditTable(ModelBuilder modelBuilder, Type entityType)
    {
        var schemaService = new SchemaDetectionService();
        var schemaInfo = schemaService.GetSchemaInfo(entityType);

        var auditTableName = schemaInfo.TableName.ToAuditTableName();

        var auditClr = typeof(AuditEntry<>).MakeGenericType(entityType);

        modelBuilder.Entity(auditClr, builder =>
        {
            builder.ToTable(auditTableName, schemaInfo.Schema);

            builder.HasKey("Id");

            builder.Property("Id").ValueGeneratedOnAdd();
            builder.Property("EntityId").HasMaxLength(128).IsRequired();
            builder.Property("UserId").HasMaxLength(20);
            builder.Property("UserEmail").HasMaxLength(150);
            builder.Property("Changes").HasColumnType("nvarchar(max)").IsRequired();
            builder.Property("CreatedAt").IsRequired();

            builder.Property("Operation")
                .HasConversion(new EnumToStringConverter<EntityState>())
                .HasMaxLength(50)
                .IsRequired();

            builder.HasIndex("EntityId", "CreatedAt", "UserId", "UserEmail")
                .HasDatabaseName($"IX_{auditTableName}_EntityId_CreatedAt_UserId_UserEmail");
        });
        
        AuditTableRegistry.RegisterMapping(entityType, schemaInfo.Schema, auditTableName!);
    }
}