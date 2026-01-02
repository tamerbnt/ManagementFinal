using System;
using System.Threading.Tasks;
using Management.Domain.Primitives;

namespace Management.Domain.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
