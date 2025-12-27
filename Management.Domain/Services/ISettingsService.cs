using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;

namespace Management.Domain.Interfaces
{
    public interface ISettingsService
    {
        Task<GeneralSettingsDto> GetGeneralSettingsAsync();
        Task UpdateGeneralSettingsAsync(GeneralSettingsDto dto);

        Task<FacilitySettingsDto> GetFacilitySettingsAsync();
        Task UpdateFacilitySettingsAsync(FacilitySettingsDto dto);

        Task<List<IntegrationDto>> GetIntegrationsAsync();

        Task<AppearanceSettingsDto> GetAppearanceSettingsAsync();
    }
}