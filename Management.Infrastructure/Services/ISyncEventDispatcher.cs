using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public interface ISyncEventDispatcher
    {
        Task DispatchAsync(string entityType, string action, string contentJson);
    }
}
