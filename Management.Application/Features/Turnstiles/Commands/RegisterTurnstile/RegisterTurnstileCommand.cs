using Management.Application.DTOs;
using Management.Domain.Primitives;
using MediatR;
using System;

namespace Management.Application.Features.Turnstiles.Commands.RegisterTurnstile
{
    public record RegisterTurnstileCommand(TurnstileDto Turnstile) : IRequest<Result<Guid>>;
}
