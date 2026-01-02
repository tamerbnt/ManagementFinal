using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Primitives;

namespace Management.Domain.Interfaces
{
    public interface ISettingsService
    {
        Task<Result<GeneralSettingsDto>> GetGeneralSettingsAsync();
        Task<Result> UpdateGeneralSettingsAsync(GeneralSettingsDto dto);

        Task<Result<FacilitySettingsDto>> GetFacilitySettingsAsync();
        Task<Result> UpdateFacilitySettingsAsync(FacilitySettingsDto dto);

        Task<Result<List<IntegrationDto>>> GetIntegrationsAsync();

        Task<Result<AppearanceSettingsDto>> GetAppearanceSettingsAsync();
        Task<Result> UpdateAppearanceSettingsAsync(AppearanceSettingsDto dto);
    }
}