using System;

namespace Management.Domain.Interfaces
{
    public interface ISyncable
    {
        Guid Id { get; }
        DateTimeOffset UpdatedAt { get; }
        bool IsSynced { get; set; }
        bool IsDeleted { get; }
    }
}
