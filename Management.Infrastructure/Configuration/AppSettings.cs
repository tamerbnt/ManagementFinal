namespace Management.Infrastructure.Configuration
{
    public class AppSettings
    {
        public ConnectionStrings ConnectionStrings { get; set; }
        public SupabaseSettings Supabase { get; set; }
    }

    public class ConnectionStrings
    {
        public string DefaultConnection { get; set; }
    }

    public class SupabaseSettings
    {
        public string Url { get; set; }
        public string Key { get; set; }
    }
}