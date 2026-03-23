using System;
using Management.Application.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Primitives;

namespace Management.Application.Services
{
    public interface IRegistrationService
    {
        Task<Result<PagedResult<RegistrationDto>>> SearchAsync(RegistrationSearchRequest request, Guid facilityId, int page = 1, int pageSize = 50);
        Task<Result<List<RegistrationDto>>> GetPendingRegistrationsAsync(Guid facilityId);
        Task<Result<RegistrationDto>> GetRegistrationAsync(Guid id, Guid facilityId);
        Task<Result<Guid>> ApproveRegistrationAsync(Guid id, Guid facilityId);
        Task<Result> UndoApproveRegistrationAsync(Guid registrationId, Guid facilityId);
        Task<Result> DeclineRegistrationAsync(Guid id, Guid facilityId);
        Task<Result> ApproveBatchAsync(List<Guid> ids, Guid facilityId);
        Task<Result> DeclineBatchAsync(List<Guid> ids, Guid facilityId);
    }
}
