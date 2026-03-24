using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Sales.Commands.CancelSalesByMember
{
    public class CancelSalesByMemberCommand : IRequest<Result>
    {
        public Guid MemberId { get; }
        public Guid FacilityId { get; }

        public CancelSalesByMemberCommand(Guid memberId, Guid facilityId)
        {
            MemberId = memberId;
            FacilityId = facilityId;
        }
    }
}
