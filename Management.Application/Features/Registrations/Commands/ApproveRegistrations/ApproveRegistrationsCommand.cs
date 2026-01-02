using MediatR;
using System;
using System.Collections.Generic;

namespace Management.Application.Features.Registrations.Commands.ApproveRegistrations
{
    public record ApproveRegistrationsCommand(List<Guid> RegistrationIds) : IRequest<bool>;
}
