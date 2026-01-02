using Management.Application.Features.Registrations.Queries.GetRegistration;
using Management.Application.Services;
using Management.Application.Features.Registrations.Queries.GetPendingRegistrations;
using Management.Application.Services;
using Management.Application.Features.Registrations.Commands.ApproveRegistration;
using Management.Application.Services;
using Management.Application.Features.Registrations.Commands.DeclineRegistration;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using MediatR;
using Management.Application.Services;
using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Infrastructure.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly ISender _sender;

        public RegistrationService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<Result<RegistrationDto>> GetRegistrationAsync(Guid id)
        {
            return await _sender.Send(new GetRegistrationQuery(id));
        }

        public async Task<Result<List<RegistrationDto>>> GetPendingRegistrationsAsync()
        {
            return await _sender.Send(new GetPendingRegistrationsQuery());
        }

        public async Task<Result<Guid>> ApproveRegistrationAsync(Guid id)
        {
            return await _sender.Send(new ApproveRegistrationCommand(id));
        }

        public async Task<Result> DeclineRegistrationAsync(Guid id)
        {
            return await _sender.Send(new DeclineRegistrationCommand(id));
        }

        public async Task<Result> ApproveBatchAsync(List<Guid> ids)
        {
            foreach (var id in ids)
            {
                var result = await _sender.Send(new ApproveRegistrationCommand(id));
                if (result.IsFailure) return Result.Failure(result.Error);
            }
            return Result.Success();
        }

        public async Task<Result> DeclineBatchAsync(List<Guid> ids)
        {
            foreach (var id in ids)
            {
                var result = await _sender.Send(new DeclineRegistrationCommand(id));
                if (result.IsFailure) return result;
            }
            return Result.Success();
        }

        public async Task<Result<PagedResult<RegistrationDto>>> SearchAsync(RegistrationSearchRequest request, int page = 1, int pageSize = 50)
        {
            // Placeholder: Not implemented yet
            return Result.Success(new PagedResult<RegistrationDto> { Items = new List<RegistrationDto>(), TotalCount = 0 });
        }
    }
}
