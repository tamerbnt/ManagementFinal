using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Facility.Queries
{
    public class FacilityQueryHandlers : 
        IRequestHandler<GetZonesQuery, Result<List<ZoneDto>>>,
        IRequestHandler<GetIntegrationsQuery, Result<List<IntegrationDto>>>
    {
        private readonly IFacilityZoneRepository _zoneRepository;
        private readonly IIntegrationRepository _integrationRepository;

        public FacilityQueryHandlers(IFacilityZoneRepository zoneRepository, IIntegrationRepository integrationRepository)
        {
            _zoneRepository = zoneRepository;
            _integrationRepository = integrationRepository;
        }

        public async Task<Result<List<ZoneDto>>> Handle(GetZonesQuery request, CancellationToken cancellationToken)
        {
            var zones = await _zoneRepository.GetAllAsync();
            var dtos = zones.Select(z => new ZoneDto
            {
                Id = z.Id,
                Name = z.Name,
                Capacity = z.Capacity,
                Type = z.Type,
                IsOperational = z.IsOperational
            }).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result<List<IntegrationDto>>> Handle(GetIntegrationsQuery request, CancellationToken cancellationToken)
        {
            var configs = await _integrationRepository.GetAllAsync();
            var dtos = configs.Select(c => new IntegrationDto
            {
                Id = c.Id,
                ProviderName = c.ProviderName,
                ApiKey = c.ApiKey,
                ApiUrl = c.ApiUrl,
                IsEnabled = c.IsEnabled
            }).ToList();

            return Result.Success(dtos);
        }
    }
}
