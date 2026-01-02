using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Facility.Commands.RegisterZone
{
    public class RegisterZoneCommandHandler : IRequestHandler<RegisterZoneCommand, Result<Guid>>
    {
        private readonly IFacilityZoneRepository _zoneRepository;

        public RegisterZoneCommandHandler(IFacilityZoneRepository zoneRepository)
        {
            _zoneRepository = zoneRepository;
        }

        public async Task<Result<Guid>> Handle(RegisterZoneCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Zone;

            var result = FacilityZone.Create(
                dto.Name,
                dto.Capacity,
                dto.Type);

            if (result.IsFailure) return Result.Failure<Guid>(result.Error);

            var zone = result.Value;
            await _zoneRepository.AddAsync(zone);

            return Result.Success(zone.Id);
        }
    }
}
