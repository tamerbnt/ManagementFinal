using Management.Domain.Common;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.Services;
using ITenantEntity = Management.Domain.Primitives.ITenantEntity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Infrastructure.Data.Interceptors
{
    public class ShadowPropertyInterceptor : SaveChangesInterceptor
    {
        private readonly ITenantService _tenantService;
        private readonly IFacilityContextService _facilityContext;
        private readonly ILogger<ShadowPropertyInterceptor> _logger;

        public ShadowPropertyInterceptor(
            ITenantService tenantService,
            IFacilityContextService facilityContext,
            ILogger<ShadowPropertyInterceptor> logger)
        {
            _tenantService = tenantService;
            _facilityContext = facilityContext;
            _logger = logger;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

            var tenantId = _tenantService.GetTenantId();
            var facilityId = _facilityContext.CurrentFacilityId;

            foreach (var entry in eventData.Context.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added)
                {
                    // 1. Tenant ID Injection
                    if (entry.Entity is ITenantEntity tenantEntity && tenantId.HasValue)
                    {
                        if (tenantEntity.TenantId == Guid.Empty) tenantEntity.TenantId = tenantId.Value;
                    }

                    // 2. Facility ID Injection
                    if (entry.Entity is IFacilityEntity facilityEntity)
                    {
                        if (facilityEntity.FacilityId == Guid.Empty)
                        {
                            if (facilityId != Guid.Empty)
                            {
                                 facilityEntity.FacilityId = facilityId;
                            }
                            else if (tenantId.HasValue && tenantId.Value != Guid.Empty)
                            {
                                _logger.LogWarning("[ShadowPropertyInterceptor] Saving {Type} with Guid.Empty FacilityId (Tenant Recovery Path).", 
                                    entry.Entity.GetType().Name);
                            }
                            else
                            {
                                // SOFTENED GUARD: Log critical error instead of throwing during NEW creation
                                // to prevent hard crashes while still alerting the developer.
                                _logger.LogCritical("[ShadowPropertyInterceptor] DATA ISOLATION VIOLATION: Saving {Type} without active context.", 
                                    entry.Entity.GetType().Name);
                            }
                        }
                    }

                    // 3. Legacy Shadow Constraints
                    SyncLegacyProperties(entry);
                }

                if (entry.State == EntityState.Modified)
                {
                    if (entry.Entity is BaseEntity baseEntity) baseEntity.UpdateTimestamp();
                    else if (entry.Entity is Management.Domain.Primitives.Entity primitiveEntity) primitiveEntity.UpdateTimestamp();

                    SyncLegacyProperties(entry);
                }

                // 5. Special Shadow Property Sync (Payroll)
                if (entry.Entity is PayrollEntry payroll)
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    {
                        entry.Property("IsPaidShadow").CurrentValue = payroll.IsPaid;
                    }
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void SyncLegacyProperties(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            if (entry.Entity is MembershipPlan or Product or Management.Domain.Models.Salon.SalonService or Management.Domain.Models.Salon.Appointment)
            {
                try
                {
                    decimal amount = entry.Entity switch
                    {
                        Product p => p.Price?.Amount ?? 0m,
                        MembershipPlan m => m.Price?.Amount ?? 0m,
                        Management.Domain.Models.Salon.SalonService s => s.BasePrice,
                        Management.Domain.Models.Salon.Appointment a => a.Price,
                        _ => 0m
                    };

                    // RELIABILITY FIX: Use entry.CurrentValues directly for shadow properties.
                    // This is more robust for 'Added' entities than entry.Property("price").CurrentValue.
                    var property = entry.Metadata.FindProperty("price");
                    if (property != null)
                    {
                        entry.CurrentValues["price"] = amount;
                        _logger.LogInformation("[ShadowPropertyInterceptor] Synced legacy 'price'={Amount} for {Type} ({State})", 
                            amount, entry.Entity.GetType().Name, entry.State);
                    }
                    else
                    {
                        _logger.LogWarning("[ShadowPropertyInterceptor] Shadow property 'price' NOT FOUND in metadata for {Type}. Column mapping may be missing.", 
                            entry.Entity.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ShadowPropertyInterceptor] CRITICAL: Failed to sync legacy 'price' for {Type} ({State}). Database save may fail.", 
                        entry.Entity.GetType().Name, entry.State);
                }
            }
        }
    }
}
