using System;
using Management.Domain.Enums;

namespace Management.Domain.Services
{
    public interface IFacilityContextService
    {
        string LanguageCode { get; }
        FacilityType CurrentFacility { get; }
        Guid CurrentFacilityId { get; }
        event Action<FacilityType> FacilityChanged;
        void SwitchFacility(FacilityType type);
        void SetFacility(FacilityType type);
        void SaveLanguage(string languageCode);
        void UpdateFacilities(System.Collections.Generic.Dictionary<FacilityType, Guid> facilityMappings);
        void UpdateFacilityId(FacilityType type, Guid actualId);
        void Initialize();
        void CommitFacility();
    }
}
