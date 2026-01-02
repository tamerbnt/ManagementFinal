using System;
using System.Threading.Tasks;
using Management.Domain.Primitives;
using Management.Domain.Services;
using Supabase;

namespace Management.Infrastructure.Repositories
{
    /// <summary>
    /// Base repository for Supabase operations.
    /// NOTE: This class is now decoupled from Supabase's BaseModel.
    /// Concrete repositories should handle mapping between domain entities and Supabase models.
    /// </summary>
    public abstract class SupabaseRepositoryBase<TEntity> where TEntity : ITenantEntity
    {
        protected readonly Supabase.Client _supabase;
        protected readonly ITenantService _tenantService;

        protected SupabaseRepositoryBase(Supabase.Client supabase, ITenantService tenantService)
        {
            _supabase = supabase;
            _tenantService = tenantService;
        }

        /// <summary>
        /// Gets the current tenant ID, throwing if not set.
        /// </summary>
        protected Guid GetTenantIdOrThrow()
        {
            var tenantId = _tenantService.GetTenantId();
            if (tenantId == null || tenantId == Guid.Empty)
            {
                throw new InvalidOperationException("Cannot perform operation: No active tenant context found.");
            }
            return tenantId.Value;
        }

        /// <summary>
        /// Ensures the entity has the current tenant ID stamped.
        /// </summary>
        public void StampTenantId(TEntity entity)
        {
            entity.TenantId = GetTenantIdOrThrow();
        }
    }
}
