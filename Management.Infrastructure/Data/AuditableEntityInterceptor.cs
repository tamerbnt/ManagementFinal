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

            var entries = context.ChangeTracker.Entries().ToList();
            Serilog.Log.Information("[AuditableInterceptor] Total tracked entries: {Count}", entries.Count);

            foreach (var entry in entries)
            {
                var type = entry.Entity.GetType().Name;
                Serilog.Log.Information("[AuditableInterceptor] Found {Type} (State: {State})", type, entry.State);

                if (entry.Entity is Management.Domain.Primitives.Entity primitiveEntity)
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    {
                        primitiveEntity.UpdateTimestamp();
                    }

                    if (entry.State == EntityState.Deleted)
                    {
                        Serilog.Log.Information("[AuditableInterceptor] Intercepted Delete for {Type} {Id}. Converting to Soft-Delete.", type, primitiveEntity.Id);
                        
                        if (entry.Entity is Management.Domain.Models.SaleItem si)
                        {
                            bool isPriceNull = si.UnitPriceSnapshot == null;
                            Serilog.Log.Information("[AuditableInterceptor] SaleItem {Id} PRE-SOFT-DELETE - UnitPriceSnapshot IsNull: {IsNull}", si.Id, isPriceNull);
                        }

                        entry.State = EntityState.Modified;
                        primitiveEntity.Delete();

                        if (entry.Entity is Management.Domain.Models.SaleItem si2)
                        {
                            bool isPriceNull = si2.UnitPriceSnapshot == null;
                            Serilog.Log.Information("[AuditableInterceptor] SaleItem {Id} POST-SOFT-DELETE - UnitPriceSnapshot IsNull: {IsNull}", si2.Id, isPriceNull);
                        }
                    }
                }
                else if (entry.Entity is BaseEntity baseEntity)
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    {
                        baseEntity.UpdateTimestamp();
                    }

                    if (entry.State == EntityState.Deleted)
                    {
                        entry.State = EntityState.Modified;
                        baseEntity.Delete();
                    }
                }
            }
        }
    }
}
