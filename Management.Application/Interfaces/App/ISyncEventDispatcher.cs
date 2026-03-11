using System.Threading.Tasks;

namespace Management.Application.Interfaces.App
{
    public interface ISyncEventDispatcher
    {
        Task DispatchAsync(string entityType, string action, string contentJson);
    }
}
