using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Staff.Queries.GetStaff
{
    public record GetStaffQuery(Guid Id) : IRequest<Result<StaffDto>>;
    public record GetAllStaffQuery() : IRequest<Result<List<StaffDto>>>;
}
