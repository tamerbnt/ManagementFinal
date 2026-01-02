using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Collections.ObjectModel;
using Management.Application.Services;
using System.Linq;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Domain.Models;
using Management.Application.Services;
using Management.Domain.Models.Salon;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;

namespace Management.Presentation.Services.Salon
{
    public interface ISalonService
    {
        ObservableCollection<Appointment> Appointments { get; }
        ObservableCollection<SalonService> Services { get; }
        Task<IEnumerable<StaffMember>> GetQualifiedStaffAsync(Guid serviceId);
        Task<StaffMember?> GetAutoAssignedStaffAsync(Guid serviceId, DateTime time);
        Task<bool> HasConflictAsync(Guid staffId, Guid clientId, DateTime startTime, DateTime endTime, Guid? excludingAppointmentId = null);
        Task RescheduleAppointmentAsync(Guid appointmentId, DateTime newStart);
        Task CompleteAppointmentAsync(Guid appointmentId, IEnumerable<ProductUsage> products);
    }

    public class SalonServiceImplementation : ISalonService
    {
        private readonly IProductService _productService;
        private readonly IStaffService _staffService;
        private readonly IFacilityContextService _facilityContext;

        public ObservableCollection<Appointment> Appointments { get; } = new();
        public ObservableCollection<SalonService> Services { get; } = new();

        public SalonServiceImplementation(
            IProductService productService, 
            IStaffService staffService,
            IFacilityContextService facilityContext)
        {
            _productService = productService;
            _staffService = staffService;
            _facilityContext = facilityContext;
            LoadMockData();
        }

        private void LoadMockData()
        {
            Services.Add(new SalonService { Name = "Haircut", Category = "Hair", BasePrice = 45.00m, DurationMinutes = 45 });
            Services.Add(new SalonService { Name = "Hair color", Category = "Hair", BasePrice = 120.00m, DurationMinutes = 120 });
            Services.Add(new SalonService { Name = "Manicure", Category = "Nails", BasePrice = 30.00m, DurationMinutes = 30 });
            Services.Add(new SalonService { Name = "Pedicure", Category = "Nails", BasePrice = 50.00m, DurationMinutes = 60 });

            // Mock Appointments
            Appointments.Add(new Appointment 
            { 
                ClientName = "Aria Montgomery", 
                StaffName = "Elena Gilbert", 
                ServiceName = "Haircut",
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(9).AddMinutes(45),
                Status = AppointmentStatus.Confirmed
            });
        }

        public async Task<IEnumerable<StaffMember>> GetQualifiedStaffAsync(Guid serviceId)
        {
            var service = Services.FirstOrDefault(s => s.Id == serviceId);
            if (service == null) return Enumerable.Empty<StaffMember>();

            var allStaff = await _staffService.GetAllAsync();
            
            // Logic: Filter by category (Mocked via name matching for demo)
            return allStaff.Where(s => 
                (service.Category == "Hair" && (s.FullName.Contains("Elena") || s.FullName.Contains("Damon"))) ||
                (service.Category == "Nails" && (s.FullName.Contains("Bonnie") || s.FullName.Contains("Caroline"))) ||
                s.Role.ToString() == "Manager");
        }

        public async Task<StaffMember?> GetAutoAssignedStaffAsync(Guid serviceId, DateTime time)
        {
            var qualified = await GetQualifiedStaffAsync(serviceId);
            var service = Services.First(s => s.Id == serviceId);
            var endTime = time.AddMinutes(service.DurationMinutes);

            foreach (var staff in qualified)
            {
                if (!await HasConflictAsync(staff.Id, Guid.Empty, time, endTime))
                {
                    return staff;
                }
            }
            return null;
        }

        public async Task<bool> HasConflictAsync(Guid staffId, Guid clientId, DateTime startTime, DateTime endTime, Guid? excludingAppointmentId = null)
        {
            // Real logic would query DB, here we check in-memory collection
            return Appointments.Any(a => 
                a.Id != excludingAppointmentId &&
                (a.StaffId == staffId || (clientId != Guid.Empty && a.ClientId == clientId)) &&
                (startTime < a.EndTime && a.StartTime < endTime));
        }

        public async Task RescheduleAppointmentAsync(Guid appointmentId, DateTime newStart)
        {
            var appt = Appointments.FirstOrDefault(a => a.Id == appointmentId);
            if (appt == null) return;

            var duration = appt.EndTime - appt.StartTime;
            appt.StartTime = newStart;
            appt.EndTime = newStart + duration;
            
            // In a real app, this would be a DB update
            // Notify UI
        }

        public async Task CompleteAppointmentAsync(Guid appointmentId, IEnumerable<ProductUsage> products)
        {
            var appt = Appointments.FirstOrDefault(a => a.Id == appointmentId);
            if (appt == null) return;

            appt.Status = AppointmentStatus.Completed;
            appt.UsedProducts.AddRange(products);

            // Inventory Link: Deduct stock
            foreach (var usage in products)
            {
                await _productService.UpdateStockAsync(_facilityContext.CurrentFacilityId, usage.ProductId, -usage.Quantity, $"Salon Service: {appt.Id}");
            }
        }
    }
}
