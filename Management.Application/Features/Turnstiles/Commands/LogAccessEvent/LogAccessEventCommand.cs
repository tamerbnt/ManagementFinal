using Management.Domain.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Turnstiles.Commands.LogAccessEvent
{
    public record LogAccessEventCommand(Guid TurnstileId, string CardId, bool Granted, string Status, string Reason) : IRequest<Result<Guid>>;
}
