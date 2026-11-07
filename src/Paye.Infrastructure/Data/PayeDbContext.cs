using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Entities;
using Paye.Core.Files;
using Paye.Infrastructure.Identity;

namespace Paye.Infrastructure.Data;

public sealed class PayeDbContext(DbContextOptions<PayeDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<PrivateFileRecord> PrivateFiles => Set<PrivateFileRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(u => u.IsActive)
                .HasDefaultValue(true)
                .IsRequired();

            entity.Property(u => u.CreatedAtUtc)
                .IsRequired();
        });

        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.ActorId)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(a => a.ActorEmail)
                .HasMaxLength(256);

            entity.Property(a => a.Action)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(a => a.EntityName)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(a => a.EntityId)
                .HasMaxLength(128);

            entity.Property(a => a.Summary)
                .HasMaxLength(500);

            entity.Property(a => a.IpAddress)
                .HasMaxLength(64);

            entity.HasIndex(a => a.OccurredAtUtc);
            entity.HasIndex(a => a.ActorId);
        });

        builder.Entity<PrivateFileRecord>(entity =>
        {
            entity.ToTable("private_files");
            entity.HasKey(f => f.Id);

            entity.Property(f => f.StoredFileName)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(f => f.OriginalDisplayName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(f => f.ContentType)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(f => f.Description)
                .HasMaxLength(300);

            entity.Property(f => f.CreatedBy)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(f => f.UpdatedBy)
                .HasMaxLength(128);

            entity.Property(f => f.DeletedBy)
                .HasMaxLength(128);

            entity.HasIndex(f => f.StoredFileName)
                .IsUnique();
        });

        builder.ApplyConfigurationsFromAssembly(typeof(PayeDbContext).Assembly);

        ApplyCrossCuttingConventions(builder);
    }

    private static void ApplyCrossCuttingConventions(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // 1. Concurrencia optimista con xmin en PostgreSQL para toda entidad IConcurrencyTracked / AuditableEntity
            if (typeof(IConcurrencyTracked).IsAssignableFrom(clrType))
            {
                builder.Entity(clrType)
                    .Property(nameof(IConcurrencyTracked.RowVersion))
                    .IsRowVersion();
            }

            // 2. Filtro global de borrado lógico (IsDeleted == false) para toda entidad ISoftDeletable
            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                var parameter = Expression.Parameter(clrType, "e");
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var notDeletedExpression = Expression.Not(isDeletedProperty);
                var lambda = Expression.Lambda(notDeletedExpression, parameter);

                builder.Entity(clrType).HasQueryFilter(lambda);
            }
        }
    }
}
