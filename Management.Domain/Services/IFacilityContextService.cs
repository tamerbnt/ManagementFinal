using System;
using Management.Domain.Enums;

namespace Management.Domain.Services
{
    public interface IFacilityContextService
    {
        FacilityType CurrentFacility { get; }
        Guid CurrentFacilityId { get; }
        event Action<FacilityType> FacilityChanged;
        void SwitchFacility(FacilityType type);
        void SetFacility(FacilityType type);
        void Initialize();
    }
}
