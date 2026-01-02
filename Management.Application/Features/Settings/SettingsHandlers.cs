using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Settings
{
    public class SettingsHandlers : 
        IRequestHandler<UpdateGeneralSettingsCommand, Result>,
        IRequestHandler<UpdateFacilitySettingsCommand, Result>,
        IRequestHandler<UpdateAppearanceSettingsCommand, Result>,
        IRequestHandler<GetGymSettingsQuery, Result<GymSettings>>
    {
        private readonly IGymSettingsRepository _settingsRepository;

        public SettingsHandlers(IGymSettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
        }

        public async Task<Result> Handle(UpdateGeneralSettingsCommand request, CancellationToken cancellationToken)
        {
            var settings = await _settingsRepository.GetAsync();
            var dto = request.Settings;

            settings.GymName = dto.GymName;
            settings.Email = dto.Email;
            settings.PhoneNumber = dto.PhoneNumber;
            settings.Website = dto.Website;
            settings.TaxId = dto.TaxId;
            settings.Address = dto.Address;
            settings.LogoUrl = dto.LogoUrl;

            await _settingsRepository.SaveAsync(settings);
            return Result.Success();
        }

        public async Task<Result> Handle(UpdateFacilitySettingsCommand request, CancellationToken cancellationToken)
        {
            var settings = await _settingsRepository.GetAsync();
            var dto = request.Settings;

            settings.MaxOccupancy = dto.MaxOccupancy;
            settings.IsMaintenanceMode = dto.IsMaintenanceMode;
            settings.OperatingHoursJson = System.Text.Json.JsonSerializer.Serialize(dto.Schedule);

            await _settingsRepository.SaveAsync(settings);
            return Result.Success();
        }

        public async Task<Result> Handle(UpdateAppearanceSettingsCommand request, CancellationToken cancellationToken)
        {
            var settings = await _settingsRepository.GetAsync();
            var dto = request.Settings;

            settings.IsLightMode = dto.IsLightMode;
            settings.Language = dto.Language;
            settings.DateFormat = dto.DateFormat;
            settings.HighContrast = dto.HighContrast;
            settings.ReducedMotion = dto.ReducedMotion;
            settings.TextScale = dto.TextScale;

            await _settingsRepository.SaveAsync(settings);
            return Result.Success();
        }

        public async Task<Result<GymSettings>> Handle(GetGymSettingsQuery request, CancellationToken cancellationToken)
        {
            var settings = await _settingsRepository.GetAsync();
            return Result.Success(settings);
        }
    }
}
