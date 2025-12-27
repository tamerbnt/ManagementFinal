using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json; // For JSON Serialization
using System.Threading.Tasks;
using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Services;

namespace Management.Application.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly IGymSettingsRepository _settingsRepository;
        private readonly IIntegrationRepository _integrationRepository;

        public SettingsService(
            IGymSettingsRepository settingsRepository,
            IIntegrationRepository integrationRepository)
        {
            _settingsRepository = settingsRepository;
            _integrationRepository = integrationRepository;
        }

        public async Task<GeneralSettingsDto> GetGeneralSettingsAsync()
        {
            var entity = await _settingsRepository.GetAsync();
            return new GeneralSettingsDto
            {
                GymName = entity.GymName,
                Address = entity.Address,
                Email = entity.Email,
                PhoneNumber = entity.PhoneNumber,
                Website = entity.Website,
                TaxId = entity.TaxId,
                LogoUrl = entity.LogoUrl
            };
        }

        public async Task UpdateGeneralSettingsAsync(GeneralSettingsDto dto)
        {
            var entity = await _settingsRepository.GetAsync();
            entity.GymName = dto.GymName;
            entity.Address = dto.Address;
            entity.Email = dto.Email;
            entity.PhoneNumber = dto.PhoneNumber;
            entity.Website = dto.Website;
            entity.TaxId = dto.TaxId;
            entity.LogoUrl = dto.LogoUrl;

            await _settingsRepository.SaveAsync(entity);
        }

        public async Task<FacilitySettingsDto> GetFacilitySettingsAsync()
        {
            var entity = await _settingsRepository.GetAsync();

            // Deserialize Operating Hours from JSON blob
            List<DayScheduleDto> schedule;
            try
            {
                schedule = string.IsNullOrEmpty(entity.OperatingHoursJson)
                    ? GenerateDefaultSchedule()
                    : JsonSerializer.Deserialize<List<DayScheduleDto>>(entity.OperatingHoursJson);
            }
            catch
            {
                schedule = GenerateDefaultSchedule();
            }

            // Zones are typically a separate table, but for this DTO we map them here
            // Assuming _settingsRepository.GetAsync() includes Zones navigation property if configured
            var zones = new List<ZoneDto>();
            // In real app: entity.Zones.Select(...)

            return new FacilitySettingsDto
            {
                MaxOccupancy = entity.MaxOccupancy,
                IsMaintenanceMode = entity.IsMaintenanceMode,
                Schedule = schedule,
                Zones = zones
            };
        }

        public async Task UpdateFacilitySettingsAsync(FacilitySettingsDto dto)
        {
            var entity = await _settingsRepository.GetAsync();

            entity.MaxOccupancy = dto.MaxOccupancy;
            entity.IsMaintenanceMode = dto.IsMaintenanceMode;

            // Serialize Schedule
            entity.OperatingHoursJson = JsonSerializer.Serialize(dto.Schedule);

            await _settingsRepository.SaveAsync(entity);
        }

        public async Task<List<IntegrationDto>> GetIntegrationsAsync()
        {
            // Mock Implementation for Prototype
            return new List<IntegrationDto>
            {
                new IntegrationDto { Name = "Stripe", Description = "Payment Gateway", IsConnected = true, IconKey = "IconCreditCard" },
                new IntegrationDto { Name = "Twilio", Description = "SMS Notifications", IsConnected = false, IconKey = "IconBell" },
                new IntegrationDto { Name = "Door Access Controller", Description = "Hardware Link", IsConnected = true, IconKey = "IconTurnstile" }
            };
        }

        public async Task<AppearanceSettingsDto> GetAppearanceSettingsAsync()
        {
            // In V1, these might be local config. Returning default.
            return await Task.FromResult(new AppearanceSettingsDto
            {
                IsLightMode = true,
                Language = "English (US)",
                HighContrast = false
            });
        }

        private List<DayScheduleDto> GenerateDefaultSchedule()
        {
            var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            return days.Select(d => new DayScheduleDto
            {
                Day = d,
                Open = "06:00",
                Close = "22:00",
                IsActive = true
            }).ToList();
        }
    }
}