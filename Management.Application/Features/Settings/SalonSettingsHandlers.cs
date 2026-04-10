using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Application.DTOs;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Settings
{
    public class SalonSettingsHandlers : 
        IRequestHandler<GetSalonSettingsQuery, Result<SalonSettings>>,
        IRequestHandler<UpdateSalonSettingsCommand, Result>
    {
        private readonly ISalonSettingsRepository _settingsRepository;

        public SalonSettingsHandlers(ISalonSettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
        }

        public async Task<Result<SalonSettings>> Handle(GetSalonSettingsQuery request, CancellationToken cancellationToken)
        {
            var settings = await _settingsRepository.GetAsync(request.FacilityId);
            return Result.Success(settings);
        }

        public async Task<Result> Handle(UpdateSalonSettingsCommand request, CancellationToken cancellationToken)
        {
            var settings = await _settingsRepository.GetAsync(request.FacilityId);
            var dto = request.Settings;

            settings.TotalChairs = dto.TotalChairs;
            settings.DailyRevenueTarget = dto.DailyRevenueTarget;
            settings.OperatingHoursJson = dto.OperatingHoursJson;

            await _settingsRepository.SaveAsync(settings);
            return Result.Success();
        }
    }
}
