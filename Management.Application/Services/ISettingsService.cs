using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;

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