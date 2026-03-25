using System;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Models;
using Management.Domain.Models.Salon;
using Management.Domain.Interfaces;
using Management.Domain.Services;
using Management.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Management.Presentation.Services.Salon
{
    public interface ISalonService
    {
        ObservableCollection<Appointment> Appointments { get; }
        ObservableCollection<SalonService> Services { get; }
        Task LoadServicesAsync();
        Task LoadAppointmentsAsync(Guid facilityId, DateTime date);
        Task<IEnumerable<StaffMember>> GetQualifiedStaffAsync(Guid serviceId);
        Task<StaffMember?> GetAutoAssignedStaffAsync(Guid serviceId, DateTime time);
        Task<bool> HasConflictAsync(Guid staffId, Guid clientId, DateTime startTime, DateTime endTime, Guid? excludingAppointmentId = null);
        Task RescheduleAppointmentAsync(Guid appointmentId, DateTime newStart);
        Task CompleteAppointmentAsync(Guid appointmentId, IEnumerable<ProductUsage> products);
        Task BookAppointmentAsync(Appointment appointment);
        Task UpdateAppointmentStatusAsync(Guid appointmentId, AppointmentStatus newStatus);
        Task CancelAppointmentAsync(Guid appointmentId);
        event EventHandler<(Guid AppointmentId, AppointmentStatus NewStatus)>? AppointmentStatusChanged;
        event EventHandler<Appointment>? AppointmentAdded;
    }

    public class SalonServiceImplementation : ISalonService
    {
        private readonly IProductService _productService;
        private readonly IStaffService _staffService;
        private readonly IFacilityContextService _facilityContext;
        private readonly IGymOperationService _gymOperationService;
        private readonly IServiceScopeFactory _scopeFactory;

        public ObservableCollection<Appointment> Appointments { get; } = new();
        public ObservableCollection<SalonService> Services { get; } = new();
        public event EventHandler<(Guid AppointmentId, AppointmentStatus NewStatus)>? AppointmentStatusChanged;
        public event EventHandler<Appointment>? AppointmentAdded;

        public SalonServiceImplementation(
            IProductService productService, 
            IStaffService staffService,
            IFacilityContextService facilityContext,
            IGymOperationService gymOperationService,
            IServiceScopeFactory scopeFactory)
        {
            _productService = productService;
            _staffService = staffService;
            _facilityContext = facilityContext;
            _gymOperationService = gymOperationService;
            _scopeFactory = scopeFactory;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await LoadServicesAsync();
            }
            catch (Exception ex)
            {
                // Basic error handling for background initialization
                System.Diagnostics.Debug.WriteLine($"Failed to initialize SalonService: {ex.Message}");
            }
        }

        public async Task LoadServicesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Management.Infrastructure.Data.AppDbContext>();
            var facilityId = _facilityContext.CurrentFacilityId;
            var tenantId = context.Facilities.IgnoreQueryFilters()
                .Where(f => f.Id == facilityId)
                .Select(f => f.TenantId)
                .FirstOrDefault();

            var dbServices = await context.SalonServices.ToListAsync();

            // Update UI collection atomically on UI Thread if available
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    Services.Clear();
                    foreach (var s in dbServices) 
                    {
                        if (s != null) Services.Add(s);
                    }
                });
            }
            else
            {
                Services.Clear();
                foreach (var s in dbServices) 
                {
                    if (s != null) Services.Add(s);
                }
            }
        }

        public async Task LoadAppointmentsAsync(Guid facilityId, DateTime date)
        {
            if (!Services.Any())
            {
                await LoadServicesAsync();
            }

            using var scope = _scopeFactory.CreateScope();
            var appointmentRepository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();

            Appointments.Clear();
            var start = date.Date;
            var end = date.Date.AddDays(1).AddTicks(-1);
            var items = await appointmentRepository.GetByDateRangeAsync(start, end, facilityId);
            var loadedItems = items?.ToList() ?? new List<Appointment>();
            
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    Appointments.Clear();
                    foreach (var item in loadedItems)
                    {
                        if (item != null) Appointments.Add(item);
                    }
                });
            }
            else
            {
                Appointments.Clear();
                foreach (var item in loadedItems)
                {
                    if (item != null) Appointments.Add(item);
                }
            }
        }

        public async Task<IEnumerable<StaffMember>> GetQualifiedStaffAsync(Guid serviceId)
        {
            var allStaff = await _staffService.GetAllAsync();

            if (serviceId == Guid.Empty)
            {
                // In service-agnostic mode, return all available staff
                return allStaff;
            }

            var service = Services.FirstOrDefault(s => s.Id == serviceId);
            if (service == null) return Enumerable.Empty<StaffMember>();

            // For now, return all staff as "qualified" to ensure the dropdown works.
            // In a real system, we might filter by Skillsets matched to the service.
            return allStaff;
        }

        public async Task<StaffMember?> GetAutoAssignedStaffAsync(Guid serviceId, DateTime time)
        {
            if (serviceId == Guid.Empty) return null;

            var qualified = await GetQualifiedStaffAsync(serviceId);
            var service = Services.FirstOrDefault(s => s.Id == serviceId);
            if (service == null) return null;

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
            using var scope = _scopeFactory.CreateScope();
            var appointmentRepository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();
            return await appointmentRepository.HasConflictAsync(staffId, startTime, endTime, excludingAppointmentId);
        }

        public async Task BookAppointmentAsync(Appointment appointment)
        {
            using var scope = _scopeFactory.CreateScope();
            var appointmentRepository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();
            await appointmentRepository.AddAsync(appointment);

            // Marshal to UI thread: ObservableCollection and event must fire on the dispatcher
            // to avoid cross-thread InvalidOperationException in WPF bindings.
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() =>
                {
                    Appointments.Add(appointment);
                    AppointmentAdded?.Invoke(this, appointment);
                });
            }
            else
            {
                Appointments.Add(appointment);
                AppointmentAdded?.Invoke(this, appointment);
            }
        }

        public async Task RescheduleAppointmentAsync(Guid appointmentId, DateTime newStart)
        {
            using var scope = _scopeFactory.CreateScope();
            var appointmentRepository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();

            var appt = await appointmentRepository.GetByIdAsync(appointmentId);
            if (appt == null) return;

            var duration = appt.EndTime - appt.StartTime;
            appt.StartTime = newStart;
            appt.EndTime = newStart + duration;
            
            await appointmentRepository.UpdateAsync(appt);
            
            // Refresh local collection if present
            var local = Appointments.FirstOrDefault(a => a.Id == appointmentId);
            if (local != null)
            {
                local.StartTime = appt.StartTime;
                local.EndTime = appt.EndTime;
            }
        }

        public async Task CompleteAppointmentAsync(Guid appointmentId, IEnumerable<ProductUsage> products)
        {
            using var scope = _scopeFactory.CreateScope();
            var appointmentRepository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();

            var appt = await appointmentRepository.GetByIdAsync(appointmentId);
            if (appt == null) return;

            // Use the status update logic to trigger sale if needed
            await UpdateAppointmentStatusAsync(appointmentId, AppointmentStatus.Completed);

            appt.UsedProducts.AddRange(products);
            await appointmentRepository.UpdateAsync(appt);

            // Inventory Link: Deduct stock
            foreach (var usage in products)
            {
                await _productService.UpdateStockAsync(_facilityContext.CurrentFacilityId, usage.ProductId, -usage.Quantity, $"Salon Service: {appt.Id}");
            }
        }

        public async Task UpdateAppointmentStatusAsync(Guid appointmentId, AppointmentStatus newStatus)
        {
            try 
            {
                using var scope = _scopeFactory.CreateScope();
                var appointmentRepository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();

                var appt = await appointmentRepository.GetByIdAsync(appointmentId);
                if (appt == null) return;

            // 1. Update status in DB first (Primary Action)
            appt.Status = newStatus;
            await appointmentRepository.UpdateAsync(appt);

            // 2. Update the local collection item and fire event (UI-Related Actions)
            // MUST be marshaled to UI thread to avoid cross-thread violations in WPF bindings.
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() => 
                {
                    var localItem = Appointments.FirstOrDefault(a => a.Id == appointmentId);
                    if (localItem != null)
                    {
                        localItem.Status = newStatus;
                    }

                    AppointmentStatusChanged?.Invoke(this, (appointmentId, newStatus));
                });
            }

            // 3. Trigger automated sale if transitioning to Completed (Secondary Action, Non-Blocking)
            if (newStatus == AppointmentStatus.Completed && appt.Status == AppointmentStatus.Completed)
            {
                _ = Task.Run(async () => 
                {
                    try 
                    {
                        // Use a specific price from the appointment if set, otherwise fallback to catalog
                        decimal price = appt.Price;
                        
                        // If price is 0, try to find a service in the catalog
                        if (price == 0)
                        {
                            var service = Services.FirstOrDefault(s => s.Id == appt.ServiceId) 
                                       ?? Services.FirstOrDefault(s => s.Name == appt.ServiceName);
                            
                            if (service != null)
                            {
                                price = service.BasePrice;
                            }
                        }

                        var label = !string.IsNullOrWhiteSpace(appt.ServiceName) ? appt.ServiceName : "Salon Service";

                        // Create a sale record using GymOperationService
                        await _gymOperationService.SellItemAsync(
                            appt.ClientId == Guid.Empty ? null : appt.ClientId.ToString(),
                            price,
                            label,
                            appt.FacilityId,
                            $"SalonAppt-{appt.Id}", // Linked ID for Undo behavior
                            Management.Domain.Enums.SaleCategory.Service, // <- Changed from Membership
                            label
                        );
                    }
                    catch (Exception ex)
                    {
                        // Log but don't block the UI/Status update
                        System.Diagnostics.Debug.WriteLine($"Background Sale Recording Failed: {ex.Message}");
                    }
                });
            }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateAppointmentStatusAsync failed: {ex.Message}");
            }
        }

        public async Task CancelAppointmentAsync(Guid appointmentId)
        {
            try 
            {
                using var scope = _scopeFactory.CreateScope();
                var appointmentRepository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();

                // 1. Soft Delete in DB
                await appointmentRepository.DeleteAsync(appointmentId);

                // 2. Update local collection and notify
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => 
                    {
                        var localItem = Appointments.FirstOrDefault(a => a.Id == appointmentId);
                        if (localItem != null)
                        {
                            Appointments.Remove(localItem);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CancelAppointmentAsync failed: {ex.Message}");
            }
        }
    }
}
