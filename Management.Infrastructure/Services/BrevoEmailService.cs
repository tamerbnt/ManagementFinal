using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Management.Infrastructure.Services
{
    public class BrevoEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly ISecureStorageService _secureStorage;
        private readonly ILogger<BrevoEmailService> _logger;

        public BrevoEmailService(
            HttpClient httpClient, 
            ISecureStorageService secureStorage,
            ILogger<BrevoEmailService> logger)
        {
            _httpClient = httpClient;
            _secureStorage = secureStorage;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var apiKey = _secureStorage.Get("ProfessionalEmailApiKey");
            var senderEmail = _secureStorage.Get("ProfessionalEmailAccount");

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogWarning("[BrevoEmail] Sending aborted: API Key or Sender Email not configured.");
                return;
            }

            var payload = new
            {
                sender = new { email = senderEmail },
                to = new[] { new { email = to } },
                subject = subject,
                htmlContent = body
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[BrevoEmail] Successfully sent email to {To}", to);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("[BrevoEmail] Failed to send email: {StatusCode} - {Error}", response.StatusCode, error);
                    throw new Exception($"Brevo API Error: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BrevoEmail] Exception during email sending to {To}", to);
                throw;
            }
        }
    }
}
