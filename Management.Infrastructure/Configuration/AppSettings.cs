namespace Management.Infrastructure.Configuration
{
    public class AppSettings
    {
        public required ConnectionStrings ConnectionStrings { get; set; }
        public required SupabaseSettings Supabase { get; set; }
    }

    public class ConnectionStrings
    {
        public required string DefaultConnection { get; set; }
    }

    public class SupabaseSettings
    {
        public required string Url { get; set; }
        public required string Key { get; set; }
    }
}