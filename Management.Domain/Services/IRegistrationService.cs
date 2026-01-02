using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Primitives;

namespace Management.Domain.Services
{
    public interface IRegistrationService
    {
        Task<Result<PagedResult<RegistrationDto>>> SearchAsync(RegistrationSearchRequest request, int page = 1, int pageSize = 50);
        Task<Result<List<RegistrationDto>>> GetPendingRegistrationsAsync();
        Task<Result<RegistrationDto>> GetRegistrationAsync(Guid id);
        Task<Result<Guid>> ApproveRegistrationAsync(Guid id);
        Task<Result> DeclineRegistrationAsync(Guid id);
        Task<Result> ApproveBatchAsync(List<Guid> ids);
        Task<Result> DeclineBatchAsync(List<Guid> ids);
    }
}
