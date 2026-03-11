using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Application.Services;

namespace Management.Application.Features.Settings
{
    public class SettingsHandlers : 
        IRequestHandler<UpdateGeneralSettingsCommand, Result>,
        IRequestHandler<UpdateFacilitySettingsCommand, Result>,
        IRequestHandler<UpdateAppearanceSettingsCommand, Result>,
        IRequestHandler<GetGymSettingsQuery, Result<GymSettings>>
    {
        private readonly IGymSettingsRepository _settingsRepository;
        private readonly IAccessControlCache _cache;

        public SettingsHandlers(IGymSettingsRepository settingsRepository, IAccessControlCache cache)
        {
            _settingsRepository = settingsRepository;
            _cache = cache;
        }

        public async Task<Result> Handle(UpdateGeneralSettingsCommand request, CancellationToken cancellationToken)
        {
            var settings = await _settingsRepository.GetAsync(request.FacilityId);
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
            var settings = await _settingsRepository.GetAsync(request.FacilityId);
            var dto = request.Settings;

            settings.MaxOccupancy = dto.MaxOccupancy;
            settings.DailyRevenueTarget = dto.DailyRevenueTarget;
            settings.IsMaintenanceMode = dto.IsMaintenanceMode;
            settings.OperatingHoursJson = System.Text.Json.JsonSerializer.Serialize(dto.Schedule);

            await _settingsRepository.SaveAsync(settings);
            
            // Fix 10: Invalidate access control cache when operating hours change
            _cache.Clear();
            
            return Result.Success();
        }

        public async Task<Result> Handle(UpdateAppearanceSettingsCommand request, CancellationToken cancellationToken)
        {
            var settings = await _settingsRepository.GetAsync(request.FacilityId);
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
            var settings = await _settingsRepository.GetAsync(request.FacilityId);
            return Result.Success(settings);
        }
    }
}
