using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Management.Infrastructure.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GymDbContext>
    {
        public GymDbContext CreateDbContext(string[] args)
        {
            // Build config to read connection string
            // In a real app, this reads appsettings.json
            // For the prototype, we can use a direct builder or hardcoded string placeholder

            var optionsBuilder = new DbContextOptionsBuilder<GymDbContext>();

            // REPLACE WITH YOUR SUPABASE CONNECTION STRING
            // Format: "Host=db.supabase.co;Port=5432;Database=postgres;User Id=postgres;Password=YOUR_PASSWORD;"
            string connectionString = "Host=localhost;Database=GymDb;Username=postgres;Password=password";

            optionsBuilder.UseNpgsql(connectionString);

            return new GymDbContext(optionsBuilder.Options);
        }
    }
}