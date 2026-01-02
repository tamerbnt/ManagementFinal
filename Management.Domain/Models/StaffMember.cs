using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class StaffMember : AggregateRoot
    {
        public string FullName { get; private set; }
        public Email Email { get; private set; }
        public PhoneNumber PhoneNumber { get; private set; }
        public StaffRole Role { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime HireDate { get; private set; }

        // Shift / Access
        public string? PinCode { get; private set; } // Hashed?

        private StaffMember(Guid id, string fullName, Email email, PhoneNumber phoneNumber, StaffRole role, DateTime hireDate) : base(id)
        {
            FullName = fullName;
            Email = email;
            PhoneNumber = phoneNumber;
            Role = role;
            HireDate = hireDate;
            IsActive = true;
        }

        private StaffMember() 
        { 
            FullName = string.Empty; 
            Email = null!; 
            PhoneNumber = null!; 
        }

        public static Result<StaffMember> Recruit(string fullName, Email email, PhoneNumber phone, StaffRole role)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return Result.Failure<StaffMember>(new Error("Staff.EmptyName", "Name is required"));

            return Result.Success(new StaffMember(Guid.NewGuid(), fullName, email, phone, role, DateTime.UtcNow));
        }

        public void UpdateDetails(string fullName, Email email, PhoneNumber phoneNumber, StaffRole role)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException("Name cannot be empty");
            
            FullName = fullName;
            Email = email;
            PhoneNumber = phoneNumber;
            Role = role;
            UpdateTimestamp();
        }

        public void Terminate()
        {
            IsActive = false;
            UpdateTimestamp();
        }
    }
}