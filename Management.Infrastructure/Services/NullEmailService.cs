using System.Threading.Tasks;
using Management.Domain.Services;

namespace Management.Infrastructure.Services
{
    public class NullEmailService : IEmailService
    {
        public Task SendEmailAsync(string to, string subject, string body)
        {
            // Do nothing
            return Task.CompletedTask;
        }
    }
}
