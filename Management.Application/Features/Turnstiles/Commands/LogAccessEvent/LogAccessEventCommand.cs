using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Turnstiles.Commands.LogAccessEvent
{
    public record LogAccessEventCommand(Guid FacilityId, Guid TurnstileId, string CardId, string TransactionId, bool Granted, string Status, string Reason) : IRequest<Result<Guid>>;
}
