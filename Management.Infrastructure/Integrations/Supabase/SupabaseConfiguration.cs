using Supabase;

namespace Management.Infrastructure.Integrations.Supabase
{
    public static class SupabaseConfiguration
    {
        public static SupabaseOptions GetOptions()
        {
            return new SupabaseOptions
            {
                AutoRefreshToken = true,
                SessionHandler = new CustomFileSessionHandler()
            };
        }
    }
}
