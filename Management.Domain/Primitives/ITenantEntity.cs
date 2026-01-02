using System;

namespace Management.Domain.Primitives
{
    public interface ITenantEntity
    {
        Guid TenantId { get; set; }
    }
}
