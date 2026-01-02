using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Turnstiles.Commands.RegisterTurnstile
{
    public class RegisterTurnstileCommandHandler : IRequestHandler<RegisterTurnstileCommand, Result<Guid>>
    {
        private readonly ITurnstileRepository _turnstileRepository;

        public RegisterTurnstileCommandHandler(ITurnstileRepository turnstileRepository)
        {
            _turnstileRepository = turnstileRepository;
        }

        public async Task<Result<Guid>> Handle(RegisterTurnstileCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Turnstile;

            var result = Turnstile.Register(dto.Name, dto.Location, dto.HardwareId);
            if (result.IsFailure) return Result.Failure<Guid>(result.Error);

            var turnstile = result.Value;
            await _turnstileRepository.AddAsync(turnstile);

            return Result.Success(turnstile.Id);
        }
    }
}
