using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    /// <summary>
    /// Represents a Lead or Pending Member waiting for approval.
    /// </summary>
    public class Registration : AggregateRoot
    {
        // Contact Info
        public string FullName { get; private set; } = string.Empty;
        public Email Email { get; private set; } = null!;
        public PhoneNumber PhoneNumber { get; private set; } = null!;

        // Marketing Info
        public string Source { get; private set; } = string.Empty; // "Walk-in", "Instagram", etc.

        // Workflow
        public RegistrationStatus Status { get; private set; }
        public string Notes { get; private set; } = string.Empty;

        // Interest
        public Guid? PreferredPlanId { get; private set; }
        public DateTime? PreferredStartDate { get; private set; }

        private Registration(
            Guid id, 
            string fullName, 
            Email email, 
            PhoneNumber phoneNumber, 
            string source,
            Guid? preferredPlanId,
            DateTime? preferredStartDate) : base(id)
        {
            FullName = fullName;
            Email = email;
            PhoneNumber = phoneNumber;
            Source = source;
            PreferredPlanId = preferredPlanId;
            PreferredStartDate = preferredStartDate;
            Status = RegistrationStatus.Pending;
        }

        private Registration() 
        {
            FullName = default!;
            Email = default!;
            PhoneNumber = default!;
            Source = default!;
            Notes = default!;
        }

        public static Result<Registration> Submit(
            string fullName, 
            Email email, 
            PhoneNumber phoneNumber, 
            string source,
            Guid? preferredPlanId,
            DateTime? preferredStartDate,
            string notes)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return Result.Failure<Registration>(new Error("Registration.EmptyName", "Name is required"));

            var registration = new Registration(Guid.NewGuid(), fullName, email, phoneNumber, source, preferredPlanId, preferredStartDate);
            registration.Notes = notes;
            return Result.Success(registration);
        }

        public void Approve()
        {
            if (Status != RegistrationStatus.Pending)
                throw new InvalidOperationException("Can only approve pending registrations");
                
            Status = RegistrationStatus.Approved;
            UpdateTimestamp();
        }
        
        public void Decline()
        {
             if (Status != RegistrationStatus.Pending)
                throw new InvalidOperationException("Can only decline pending registrations");
                
            Status = RegistrationStatus.Declined;
            UpdateTimestamp();
        }
    }
}