using Management.Application.Abstractions.Messaging;
using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System.Collections.Generic;

namespace Management.Application.Features.Sales.Commands.ProcessCheckout
{
    public record ProcessCheckoutCommand(Guid FacilityId, CheckoutRequestDto Request, bool PublishNotification = true) : IRequest<Result<Guid>>, IAuthorizeableRequest
    {
        public IEnumerable<string> RequiredPermissions => new[] { "Check-In" };
    }
}
