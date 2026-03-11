using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Supabase;
using Management.Application.Interfaces;
using Management.Application.DTOs;
using Management.Domain.Exceptions;
using System.Text.Json;

namespace Management.Infrastructure.Services
{
    public class LicenseService : ILicenseService
    {
        private readonly Client _supabase;
        private readonly ILogger<LicenseService> _logger;

        public LicenseService(Client supabase, ILogger<LicenseService> logger)
        {
            _supabase = supabase;
            _logger = logger;
        }

        public async Task<LicenseCheckResult> ValidateLicenseAsync(string licenseKey, string hardwareId)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return LicenseCheckResult.Failure("License key cannot be empty.");
            }

            // 1. Sanitization
            var cleanKey = licenseKey.Trim().ToUpper();
            
            _logger.LogInformation("[LICENSE_AUDIT] Attempting to verify license key: {Key} for Hardware: {HardwareId}", cleanKey, hardwareId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    // The keys MUST match the SQL function parameter names exactly
                    { "p_lookup_key", cleanKey },
                    { "p_hardware_id", hardwareId },
                    { "p_label", Environment.MachineName } // Added to disambiguate RPC
                };

                // Log the exact parameters being sent
                _logger.LogInformation("[LICENSE_AUDIT] RPC Parameters: p_lookup_key={LicenseKey}, p_hardware_id={HardwareId}", cleanKey, hardwareId);

                // 2. Call the Postgres Function defined in SQL
                var response = await _supabase.Rpc("verify_license_key", parameters);
                
                // 3. Log raw response for debugging
                _logger.LogInformation("[LICENSE_AUDIT] Supabase RPC Raw Response: {Content}", response.Content ?? "<NULL>");
                _logger.LogInformation("[LICENSE_AUDIT] Response Status Code: {StatusCode}", response.ResponseMessage?.StatusCode);



                // 4. Parse Response
                // Supabase RPC returns a single JSON object (not an array)
                // Expected structure: { "valid": true/false, "message": "...", "license_id": "...", "tenant_id": null }
                using var doc = JsonDocument.Parse(response.Content ?? "{}");
                JsonElement root = doc.RootElement;
                
                _logger.LogInformation("[LICENSE_AUDIT] Root ValueKind: {ValueKind}", root.ValueKind);
                
                bool isValid = false;
                if (root.TryGetProperty("valid", out var validProp))
                {
                    isValid = validProp.GetBoolean();
                    _logger.LogInformation("[LICENSE_AUDIT] Parsed 'valid' property: {IsValid}", isValid);
                }
                else
                {
                    _logger.LogWarning("[LICENSE_AUDIT] 'valid' property NOT FOUND in response. Root ValueKind: {ValueKind}", root.ValueKind);
                }

                string message = string.Empty;
                if (root.TryGetProperty("message", out var msgProp))
                {
                    message = msgProp.GetString() ?? string.Empty;
                    _logger.LogInformation("[LICENSE_AUDIT] Parsed 'message' property: {Message}", message);
                }
                else
                {
                    _logger.LogWarning("[LICENSE_AUDIT] 'message' property NOT FOUND in response");
                }

                if (isValid)
                {
                    _logger.LogInformation("[LICENSE_AUDIT] ✓ License validation SUCCESSFUL for key: {Key}", cleanKey);
                    return LicenseCheckResult.Success();
                }
                else
                {
                    _logger.LogWarning("[LICENSE_AUDIT] ✗ License validation FAILED. Reason: {Message}", message);
                    _logger.LogWarning("[LICENSE_AUDIT] Throwing LicenseException with message: {ExceptionMessage}", message ?? "Invalid license key.");
                    throw new LicenseException(message ?? "Invalid license key.");
                }
            }
            catch (LicenseException)
            {
                // Re-throw license exceptions to be handled by the UI
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during license verification RPC.");
                throw new LicenseException("System error during license validation. Please try again later.", ex);
            }
        }
    }
}
