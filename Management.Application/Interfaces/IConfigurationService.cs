using System.Threading.Tasks;

namespace Management.Application.Interfaces
{
    public interface IConfigurationService
    {
        Task SaveConfigAsync<T>(T config, string filename);
        Task<T?> LoadConfigAsync<T>(string filename);
    }
}
