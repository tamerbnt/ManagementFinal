using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Members.Commands.UndoRegistration
{
    public class UndoRegistrationCommand : IRequest<Result>
    {
        public Guid MemberId { get; }
        public Guid FacilityId { get; }

        public UndoRegistrationCommand(Guid memberId, Guid facilityId)
        {
            MemberId = memberId;
            FacilityId = facilityId;
        }
    }
}
