using Management.Application.Features.Settings;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Infrastructure.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ISender _sender;

        public SettingsService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<Result<GeneralSettingsDto>> GetGeneralSettingsAsync()
        {
            var result = await _sender.Send(new GetGymSettingsQuery());
            if (result.IsFailure) return Result.Failure<GeneralSettingsDto>(result.Error);

            var s = result.Value;
            return Result.Success(new GeneralSettingsDto(
                s.GymName, s.Email, s.PhoneNumber, s.Website, s.TaxId, s.Address, s.LogoUrl));
        }

        public async Task<Result> UpdateGeneralSettingsAsync(GeneralSettingsDto dto)
        {
            return await _sender.Send(new UpdateGeneralSettingsCommand(dto));
        }

        public async Task<Result<FacilitySettingsDto>> GetFacilitySettingsAsync()
        {
            var result = await _sender.Send(new GetGymSettingsQuery());
            if (result.IsFailure) return Result.Failure<FacilitySettingsDto>(result.Error);

            var s = result.Value;
            var hours = string.IsNullOrWhiteSpace(s.OperatingHoursJson) 
                ? new List<DayScheduleDto>() 
                : System.Text.Json.JsonSerializer.Deserialize<List<DayScheduleDto>>(s.OperatingHoursJson) ?? new List<DayScheduleDto>();

            return Result.Success(new FacilitySettingsDto(
                s.MaxOccupancy,
                s.IsMaintenanceMode,
                hours,
                new List<ZoneDto>())); // Zones might need their own logic if expanded
        }

        public async Task<Result> UpdateFacilitySettingsAsync(FacilitySettingsDto dto)
        {
            return await _sender.Send(new UpdateFacilitySettingsCommand(dto));
        }

        public async Task<Result<List<IntegrationDto>>> GetIntegrationsAsync()
        {
            // This might still need a repository call if Integrations are separate
            return Result.Success(new List<IntegrationDto>());
        }

        public async Task<Result<AppearanceSettingsDto>> GetAppearanceSettingsAsync()
        {
            var result = await _sender.Send(new GetGymSettingsQuery());
            if (result.IsFailure) return Result.Failure<AppearanceSettingsDto>(result.Error);

            var s = result.Value;
            return Result.Success(new AppearanceSettingsDto(
                s.IsLightMode, s.Language, s.DateFormat, s.HighContrast, s.ReducedMotion, s.TextScale));
        }

        public async Task<Result> UpdateAppearanceSettingsAsync(AppearanceSettingsDto dto)
        {
            return await _sender.Send(new UpdateAppearanceSettingsCommand(dto));
        }
    }
}
