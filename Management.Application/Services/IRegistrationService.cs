using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;

namespace Management.Application.Services
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
