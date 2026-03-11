using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Primitives;

namespace Management.Domain.Interfaces
{
    public interface ISettingsService
    {
        Task<Result<GeneralSettingsDto>> GetGeneralSettingsAsync(System.Guid facilityId);
        Task<Result> UpdateGeneralSettingsAsync(System.Guid facilityId, GeneralSettingsDto dto);

        Task<Result<FacilitySettingsDto>> GetFacilitySettingsAsync(System.Guid facilityId);
        Task<Result> UpdateFacilitySettingsAsync(System.Guid facilityId, FacilitySettingsDto dto);

        Task<Result<List<IntegrationDto>>> GetIntegrationsAsync(System.Guid facilityId);

        Task<Result<AppearanceSettingsDto>> GetAppearanceSettingsAsync(System.Guid facilityId);
        Task<Result> UpdateAppearanceSettingsAsync(System.Guid facilityId, AppearanceSettingsDto dto);
    }
}
