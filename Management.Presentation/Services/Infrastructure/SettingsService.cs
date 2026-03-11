using Management.Presentation.Services.Application;
using Management.Domain.Enums;

namespace Management.Presentation.Services.Infrastructure
{
    public interface ISettingsService
    {
        FacilityType GetFacilityType();
        string GetFacilityName();
    }

    public class SettingsService : ISettingsService
    {
        public FacilityType GetFacilityType()
        {
            // Mocked to test the Salon (Plum) theme
            return FacilityType.Salon;
        }

        public string GetFacilityName()
        {
            return "Luxe Beauty Lounge";
        }
    }
}
