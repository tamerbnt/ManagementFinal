using MediatR;
using Management.Domain.Common;
using System;

namespace Management.Application.Features.Products.Commands.ToggleProductPin
{
    public record ToggleProductPinCommand(Guid Id) : IRequest<Result>;
}
