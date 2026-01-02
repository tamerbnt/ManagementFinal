using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Facility.Commands.UpdateIntegration
{
    public class UpdateIntegrationCommandHandler : IRequestHandler<UpdateIntegrationCommand, Result<Guid>>
    {
        private readonly IIntegrationRepository _integrationRepository;

        public UpdateIntegrationCommandHandler(IIntegrationRepository integrationRepository)
        {
            _integrationRepository = integrationRepository;
        }

        public async Task<Result<Guid>> Handle(UpdateIntegrationCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Integration;
            var config = await _integrationRepository.GetByIdAsync(dto.Id);

            if (config == null)
            {
                return Result.Failure<Guid>(new Error("Integration.NotFound", "Integration config not found"));
            }

            config.UpdateDetails(dto.ApiKey, dto.ApiUrl);
            if (dto.IsEnabled) config.Enable(); else config.Disable();

            await _integrationRepository.UpdateAsync(config);

            return Result.Success(config.Id);
        }
    }
}
