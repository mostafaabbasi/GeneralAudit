using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Seedwork.Auditing.Abstractions;

namespace Seedwork.Auditing.Core;

public static class AuditingModelBuilderExtensions
{
    public static ModelBuilder EnableAuditing(this ModelBuilder modelBuilder)
    {
        var auditableTypes = DiscoverAuditableTypes(modelBuilder);

        foreach (var entityType in auditableTypes)
        {
            ConfigureAuditTable(modelBuilder, entityType);
        }

        return modelBuilder;
    }

    private static List<Type> DiscoverAuditableTypes(ModelBuilder modelBuilder)
    {
        var model = modelBuilder.Model;

        return model.GetEntityTypes()
            .Where(et => typeof(IAuditableEntity).IsAssignableFrom(et.ClrType))
            .Select(et => et.ClrType)
            .ToList();
    }

    private static void ConfigureAuditTable(ModelBuilder modelBuilder, Type entityType)
    {
        var schemaService = new SchemaDetectionService();
        var schemaInfo = schemaService.GetSchemaInfo(entityType);

        var auditTableName = $"{schemaInfo.TableName}_Audit";

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable(auditTableName, schemaInfo.Schema);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd().IsRequired();
            entity.Property(e => e.EntityType).HasMaxLength(250).IsRequired();
            entity.Property(e => e.EntityId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Operation)
                .HasConversion(new EnumToStringConverter<EntityState>())
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(20).IsRequired();
            entity.Property(e => e.UserEmail).HasMaxLength(150);
            entity.Property(e => e.Changes).HasColumnType("nvarchar(max)").IsRequired();

            entity.HasIndex(e => new { e.EntityId, e.CreatedAt, e.UserId, e.UserEmail });
        });

        AuditTableRegistry.RegisterMapping(entityType, schemaInfo.Schema, auditTableName);
    }
}