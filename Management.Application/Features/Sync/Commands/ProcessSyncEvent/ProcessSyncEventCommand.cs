using MediatR;
using Management.Domain.Primitives;

namespace Management.Application.Features.Sync.Commands.ProcessSyncEvent
{
    public record ProcessSyncEventCommand(string EntityType, string Action, string ContentJson) : IRequest<Result>;
}
