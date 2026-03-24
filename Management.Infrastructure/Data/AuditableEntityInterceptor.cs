using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Management.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Management.Infrastructure.Data
{
    public class AuditableEntityInterceptor : SaveChangesInterceptor
    {
        public AuditableEntityInterceptor()
        {
            Serilog.Log.Information("[AuditableInterceptor] Constructor called.");
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            UpdateEntities(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            UpdateEntities(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void UpdateEntities(DbContext? context)
        {
            if (context == null) return;

            // STEP 1 — Rescue primary entities first (Sale, SaleItem, Member etc)
            // ToList() is critical — captures snapshot before we modify states
            var deletedEntries = context.ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in deletedEntries)
            {
                // Both Entity and BaseEntity implement soft-delete via Delete()
                if (entry.Entity is Management.Domain.Primitives.Entity primitiveEntity)
                {
                    entry.State = EntityState.Modified;
                    primitiveEntity.Delete();
                    Serilog.Log.Information("[AuditableInterceptor] Soft-deleted Entity: {Type} {Id}", entry.Entity.GetType().Name, primitiveEntity.Id);
                }
                else if (entry.Entity is BaseEntity baseEntity)
                {
                    entry.State = EntityState.Modified;
                    baseEntity.Delete();
                    Serilog.Log.Information("[AuditableInterceptor] Soft-deleted BaseEntity: {Type} {Id}", entry.Entity.GetType().Name, baseEntity.Id);
                }
                else if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    // Audit timestamps for non-deleted entities
                    if (entry.Entity is Management.Domain.Primitives.Entity p) p.UpdateTimestamp();
                    else if (entry.Entity is BaseEntity b) b.UpdateTimestamp();
                }
            }

            // STEP 2 — Rescue orphaned owned types AFTER the primary loop
            // EF Core cascades EntityState.Deleted to owned types automatically
            // These owned types are not BaseEntity/Entity so they were missed in Step 1
            var orphanedOwnedTypes = context.ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Deleted && 
                           !(e.Entity is Management.Domain.Primitives.Entity) && 
                           !(e.Entity is BaseEntity))
                .ToList();

            foreach (var entry in orphanedOwnedTypes)
            {
                // Setting to Unchanged means EF Core will not try to UPDATE them to NULL
                // or DELETE them, which is correct for table-split owned types on a rescued parent.
                entry.State = EntityState.Unchanged;
                Serilog.Log.Information("[AuditableInterceptor] Rescued orphaned owned type: {Type}", entry.Entity.GetType().Name);
            }
        }
    }
}
