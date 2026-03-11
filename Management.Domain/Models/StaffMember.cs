using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class StaffMember : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public string FullName { get; private set; }
        public Email Email { get; private set; }
        public PhoneNumber PhoneNumber { get; private set; }
        public StaffRole Role { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime HireDate { get; private set; }
        public decimal Salary { get; private set; }
        public int PaymentDay { get; private set; }
        public System.Collections.Generic.Dictionary<string, bool> Permissions { get; private set; } = new();
        public string? SupabaseUserId { get; private set; }
        public System.Collections.Generic.List<string> AllowedModules { get; private set; } = new();

        // Shift / Access
        public string? PinCode { get; private set; } 
        public string? CardId { get; private set; }

        public void SetCardId(string cardId)
        {
            CardId = cardId;
            UpdateTimestamp();
        }

        public void SetPinCode(string pinCode)
        {
            PinCode = pinCode;
            UpdateTimestamp();
        }
        
        // Deferred Auth (Local-First)
        public string? PendingAuthEmail { get; private set; }
        public string AuthSyncStatus { get; private set; } = "none"; // none, pending, completed, failed

        private StaffMember(Guid id, Guid tenantId, Guid facilityId, string fullName, Email email, PhoneNumber phoneNumber, StaffRole role, DateTime hireDate, decimal salary, int paymentDay) : base(id)
        {
            TenantId = tenantId;
            FacilityId = facilityId;
            FullName = fullName;
            Email = email;
            PhoneNumber = phoneNumber;
            Role = role;
            HireDate = hireDate;
            Salary = salary;
            PaymentDay = paymentDay;
            Permissions = new System.Collections.Generic.Dictionary<string, bool>();
            IsActive = true;
        }

        public StaffMember() 
        { 
            FullName = string.Empty; 
            Email = null!; 
            PhoneNumber = null!; 
        }

        public static Result<StaffMember> Recruit(Guid tenantId, Guid facilityId, string fullName, Email email, PhoneNumber phone, StaffRole role, decimal salary, int paymentDay)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return Result.Failure<StaffMember>(new Error("Staff.EmptyName", "Name is required"));

            return Result.Success(new StaffMember(Guid.NewGuid(), tenantId, facilityId, fullName, email, phone, role, DateTime.UtcNow, salary, paymentDay));
        }

        public void SetPermission(string name, bool isGranted)
        {
            Permissions[name] = isGranted;
            UpdateTimestamp();
        }

        public void SetSupabaseUserId(string userId)
        {
            SupabaseUserId = userId;
            UpdateTimestamp();
        }

        public void SetAllowedModules(System.Collections.Generic.List<string> modules)
        {
            AllowedModules = modules ?? new();
            UpdateTimestamp();
        }

        public void UpdateDetails(string fullName, Email email, PhoneNumber phoneNumber, StaffRole role, decimal salary, int paymentDay, System.Collections.Generic.List<string>? allowedModules = null)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException("Name cannot be empty");
            
            FullName = fullName;
            Email = email;
            PhoneNumber = phoneNumber;
            Role = role;
            Salary = salary;
            PaymentDay = paymentDay;
            if (allowedModules != null) AllowedModules = allowedModules;
            UpdateTimestamp();
        }

        public static StaffMember ForLocalSync(Guid id, Guid tenantId, Guid facilityId, string fullName, Email email, StaffRole role, bool isActive, decimal salary, int paymentDay)
        {
            return new StaffMember(id, tenantId, facilityId, fullName, email, PhoneNumber.None, role, DateTime.UtcNow, salary, paymentDay)
            {
                IsActive = isActive
            };
        }

        public void Terminate()
        {
            IsActive = false;
            UpdateTimestamp();
        }
        
        public void MarkAuthPending(string email)
        {
            PendingAuthEmail = email;
            AuthSyncStatus = "pending";
            UpdateTimestamp();
            IsSynced = false;
        }

        public void MarkAuthCompleted(string supabaseUserId)
        {
            SupabaseUserId = supabaseUserId;
            AuthSyncStatus = "completed";
            PendingAuthEmail = null;
            UpdateTimestamp();
            IsSynced = false;
        }

        public void MarkAuthFailed(string error)
        {
            AuthSyncStatus = "failed";
            UpdateTimestamp();
            IsSynced = false;
        }
    }
}
