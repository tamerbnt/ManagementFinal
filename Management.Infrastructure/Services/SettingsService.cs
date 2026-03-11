using Management.Application.Features.Settings;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ISender _sender;
        private readonly IAccessControlCache _cache;

        public SettingsService(ISender sender, IAccessControlCache cache)
        {
            _sender = sender;
            _cache = cache;
        }

        public async Task<Result<GeneralSettingsDto>> GetGeneralSettingsAsync(Guid facilityId)
        {
            var result = await _sender.Send(new GetGymSettingsQuery(facilityId));
            if (result.IsFailure) return Result.Failure<GeneralSettingsDto>(result.Error);

            var s = result.Value;
            return Result.Success(new GeneralSettingsDto(
                s.GymName, s.Email, s.PhoneNumber, s.Website, s.TaxId, s.Address, s.LogoUrl));
        }

        public async Task<Result> UpdateGeneralSettingsAsync(Guid facilityId, GeneralSettingsDto dto)
        {
            return await _sender.Send(new UpdateGeneralSettingsCommand(facilityId, dto));
        }

        public async Task<Result<FacilitySettingsDto>> GetFacilitySettingsAsync(Guid facilityId)
        {
            var result = await _sender.Send(new GetGymSettingsQuery(facilityId));
            if (result.IsFailure) return Result.Failure<FacilitySettingsDto>(result.Error);

            var s = result.Value;
            var hours = string.IsNullOrWhiteSpace(s.OperatingHoursJson) || s.OperatingHoursJson == "{}"
                ? new List<DayScheduleDto>() 
                : System.Text.Json.JsonSerializer.Deserialize<List<DayScheduleDto>>(s.OperatingHoursJson) ?? new List<DayScheduleDto>();

            return Result.Success(new FacilitySettingsDto(
                s.MaxOccupancy,
                s.DailyRevenueTarget,
                s.IsMaintenanceMode,
                hours,
                new List<ZoneDto>())); // Zones might need their own logic if expanded
        }

        public async Task<Result> UpdateFacilitySettingsAsync(Guid facilityId, FacilitySettingsDto dto)
        {
            var result = await _sender.Send(new UpdateFacilitySettingsCommand(facilityId, dto));
            if (result.IsSuccess)
            {
                _cache.InvalidateFacilitySchedules();
            }
            return result;
        }

        public async Task<Result<List<IntegrationDto>>> GetIntegrationsAsync(Guid facilityId)
        {
            // This might still need a repository call if Integrations are separate
            return Result.Success(new List<IntegrationDto>());
        }

        public async Task<Result<AppearanceSettingsDto>> GetAppearanceSettingsAsync(Guid facilityId)
        {
            var result = await _sender.Send(new GetGymSettingsQuery(facilityId));
            if (result.IsFailure) return Result.Failure<AppearanceSettingsDto>(result.Error);

            var s = result.Value;
            return Result.Success(new AppearanceSettingsDto(
                s.IsLightMode, s.Language, s.DateFormat, s.HighContrast, s.ReducedMotion, s.TextScale));
        }

        public async Task<Result> UpdateAppearanceSettingsAsync(Guid facilityId, AppearanceSettingsDto dto)
        {
            return await _sender.Send(new UpdateAppearanceSettingsCommand(facilityId, dto));
        }
    }
}
