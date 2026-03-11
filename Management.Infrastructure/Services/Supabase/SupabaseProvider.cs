using System.Threading.Tasks;
using Supabase;

namespace Management.Infrastructure.Integrations.Supabase
{
    public interface ISupabaseProvider
    {
        Client Client { get; }
        Task InitializeAsync();
    }

    public class SupabaseProvider : ISupabaseProvider
    {
        private const string SupabaseUrl = "https://your-project.supabase.co"; // Replace with appsettings check if needed
        private const string SupabaseKey = "your-anon-key";

        private Client _client;
        public Client Client => _client;

        public async Task InitializeAsync()
        {
            if (_client != null) return;

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = true,
                AutoRefreshToken = true
            };

            _client = new Client(SupabaseUrl, SupabaseKey, options);
            await _client.InitializeAsync();
        }
    }
}
