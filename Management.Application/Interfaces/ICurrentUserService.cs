using System;

namespace Management.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? CurrentFacilityId { get; }
        Guid? UserId { get; }
    }
}
