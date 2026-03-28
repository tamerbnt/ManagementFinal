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

            // STEP 1 — Normal lifecycle (Added, Modified)
            var entries = context.ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .ToList();

            foreach (var entry in entries)
            {
                if (entry.Entity is Management.Domain.Primitives.Entity p) p.UpdateTimestamp();
                else if (entry.Entity is BaseEntity b) b.UpdateTimestamp();
                
                Serilog.Log.Debug("[AuditableInterceptor] Updated Progress Timestamp: {Type} {Id} ({State})", 
                    entry.Entity.GetType().Name, 
                    (entry.Entity as Management.Domain.Primitives.Entity)?.Id ?? (entry.Entity as BaseEntity)?.Id ?? Guid.Empty,
                    entry.State);
            }

            // STEP 2 — Soft-Delete Rescue
            var deletedEntries = context.ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in deletedEntries)
            {
                // 1. Soft-delete the main entity
                if (entry.Entity is Management.Domain.Primitives.Entity primitiveEntity)
                {
                    entry.State = EntityState.Modified;
                    primitiveEntity.Delete();
                    Serilog.Log.Information("[AuditableInterceptor] Soft-deleted Entity: {Type} {Id}", entry.Entity.GetType().Name, primitiveEntity.Id);
                    
                    // 2. Rescue owned types of THIS specific soft-deleted entity
                    RescueOwnedTypes(context, entry);
                }
                else if (entry.Entity is BaseEntity baseEntity)
                {
                    entry.State = EntityState.Modified;
                    baseEntity.Delete();
                    Serilog.Log.Information("[AuditableInterceptor] Soft-deleted BaseEntity: {Type} {Id}", entry.Entity.GetType().Name, baseEntity.Id);
                    
                    // 2. Rescue owned types of THIS specific soft-deleted entity
                    RescueOwnedTypes(context, entry);
                }
            }
        }

        private void RescueOwnedTypes(DbContext context, Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry parentEntry)
        {
            // In EF Core, if a parent is marked for deletion or modification, its owned types 
            // might also be marked for deletion if they are being replaced or removed.
            // When soft-deleting, we want to KEEP the owned types (Price, Cost, etc.) 
            // in their current state so the record remains readable in history.
            
            var ownedEntries = context.ChangeTracker.Entries()
                .Where(e => e.Metadata.IsOwned() && 
                           e.State == EntityState.Deleted);

            foreach (var owned in ownedEntries)
            {
                // We only rescue if the owned type belongs to the entity we just soft-deleted.
                // This prevents us from 'rescuing' a sub-object that is being legitimately 
                // replaced during a normal Update (where the parent is Modified).
                if (parentEntry.State == EntityState.Modified)
                {
                    owned.State = EntityState.Unchanged;
                    Serilog.Log.Debug("[AuditableInterceptor] Rescued owned type '{Type}' from soft-delete parent.", owned.Entity.GetType().Name);
                }
            }
        }
    }
}
