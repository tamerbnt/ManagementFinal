using Management.Application.Features.Registrations.Queries.GetRegistration;
using Management.Application.Features.Registrations.Queries.SearchRegistrations;
using Management.Application.Services;
using Management.Application.Features.Registrations.Queries.GetPendingRegistrations;
using Management.Application.Features.Registrations.Commands.ApproveRegistration;
using Management.Application.Features.Registrations.Commands.DeclineRegistration;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using Management.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly ISender _sender;

        public RegistrationService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<Result<RegistrationDto>> GetRegistrationAsync(Guid id, Guid facilityId)
        {
            return await _sender.Send(new GetRegistrationQuery(id, facilityId));
        }

        public async Task<Result<List<RegistrationDto>>> GetPendingRegistrationsAsync(Guid facilityId)
        {
            return await _sender.Send(new GetPendingRegistrationsQuery(facilityId));
        }

        public async Task<Result<(Guid MemberId, Guid? SaleId)>> ApproveRegistrationAsync(Guid id, Guid facilityId)
        {
            return await _sender.Send(new ApproveRegistrationCommand(id, facilityId));
        }

        public async Task<Result> UndoApproveRegistrationAsync(Guid registrationId, Guid facilityId)
        {
            return await _sender.Send(new Management.Application.Features.Registrations.Commands.UndoApproveRegistration.UndoApproveRegistrationCommand(registrationId, facilityId));
        }

        public async Task<Result> DeclineRegistrationAsync(Guid id, Guid facilityId)
        {
            return await _sender.Send(new DeclineRegistrationCommand(id, facilityId));
        }

        public async Task<Result> ApproveBatchAsync(List<Guid> ids, Guid facilityId)
        {
            foreach (var id in ids)
            {
                var result = await _sender.Send(new ApproveRegistrationCommand(id, facilityId));
                if (result.IsFailure) return Result.Failure(result.Error);
            }
            return Result.Success();
        }

        public async Task<Result> DeclineBatchAsync(List<Guid> ids, Guid facilityId)
        {
            foreach (var id in ids)
            {
                var result = await _sender.Send(new DeclineRegistrationCommand(id, facilityId));
                if (result.IsFailure) return result;
            }
            return Result.Success();
        }

        public async Task<Result<PagedResult<RegistrationDto>>> SearchAsync(RegistrationSearchRequest request, Guid facilityId, int page = 1, int pageSize = 50)
        {
            return await _sender.Send(new SearchRegistrationsQuery(
                request.SearchTerm, 
                request.FilterType, 
                request.Status, 
                page, 
                pageSize));
        }
    }
}
