using System;

namespace Management.Domain.Common
{
    public interface ITenantEntity
    {
        Guid TenantId { get; set; }
    }
}

