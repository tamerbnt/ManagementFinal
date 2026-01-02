using MediatR;
using System.Threading.Tasks;
using Management.Application.Features.Sync.Commands.ProcessSyncEvent;

namespace Management.Infrastructure.Services
{
    public class SyncEventDispatcher : ISyncEventDispatcher
    {
        private readonly IMediator _mediator;

        public SyncEventDispatcher(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task DispatchAsync(string entityType, string action, string contentJson)
        {
            await _mediator.Send(new ProcessSyncEventCommand(entityType, action, contentJson));
        }
    }
}
