using System;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Domain.Primitives;

namespace Management.Application.Services
{
    public interface IMemberAccessService
    {
        Task<Result<AccessEventDto>> ProcessAccessFlowAsync(string cardId, Guid facilityId, ScanDirection direction, string? transactionId = null);
    }
}
