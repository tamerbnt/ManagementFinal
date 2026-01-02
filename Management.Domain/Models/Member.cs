using System;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using Management.Domain.Enums;

namespace Management.Domain.Models
{
    public class Member : AggregateRoot, ITenantEntity
    {

        // Core Identity
        public string FullName { get; private set; } = string.Empty;
        public Email Email { get; private set; } = null!;
        public PhoneNumber PhoneNumber { get; private set; } = null!;

        // Physical Access
        public string CardId { get; private set; } = string.Empty; // RFID/NFC Tag ID
        public string ProfileImageUrl { get; private set; } = string.Empty;

        // Membership Status
        public MemberStatus Status { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime ExpirationDate { get; private set; }

        // Foreign Key to Membership Plan
        public Guid? MembershipPlanId { get; private set; }

        // Emergency Info
        public string EmergencyContactName { get; private set; } = string.Empty;
        public PhoneNumber? EmergencyContactPhone { get; private set; }

        // Internal Notes
        public string Notes { get; private set; } = string.Empty;

        private Member(
            Guid id,
            string fullName,
            Email email,
            PhoneNumber phoneNumber,
            string cardId,
            Guid? membershipPlanId) : base(id)
        {
            FullName = fullName;
            Email = email;
            PhoneNumber = phoneNumber;
            CardId = cardId;
            MembershipPlanId = membershipPlanId;
            Status = MemberStatus.Pending;
            StartDate = DateTime.UtcNow;
            ExpirationDate = DateTime.UtcNow; // Expired by default until renewed
        }

        // EF Core & Supabase Constructor
        public Member() { }

        public static Result<Member> Register(
            string fullName,
            Email email,
            PhoneNumber phoneNumber,
            string cardId,
            Guid? membershipPlanId)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return Result.Failure<Member>(new Error("Member.EmptyName", "Name is required"));

            var member = new Member(Guid.NewGuid(), fullName, email, phoneNumber, cardId, membershipPlanId);
            
            // e.g. member.RaiseDomainEvent(new MemberRegistered(member.Id));
            
            return Result.Success(member);
        }

        public void UpdateDetails(
            string fullName, 
            Email? email, 
            PhoneNumber? phoneNumber, 
            string cardId, 
            string profileImageUrl,
            string notes)
        {
            if (!string.IsNullOrWhiteSpace(fullName)) FullName = fullName;
            if (email is not null) Email = email;
            if (phoneNumber is not null) PhoneNumber = phoneNumber;
            if (!string.IsNullOrWhiteSpace(cardId)) CardId = cardId;
            if (!string.IsNullOrWhiteSpace(profileImageUrl)) ProfileImageUrl = profileImageUrl;
            Notes = notes; // Notes can be cleared
            
            UpdateTimestamp();
        }

        public void UpdateEmergencyContact(string name, PhoneNumber? phone)
        {
            EmergencyContactName = name;
            if (phone is not null) EmergencyContactPhone = phone;
            UpdateTimestamp();
        }


        public void ActivateMembership(DateTime startDate, DateTime expirationDate)
        {
            StartDate = startDate;
            ExpirationDate = expirationDate;
            Status = MemberStatus.Active;
            UpdateTimestamp();
        }

        public void RenewReferencePlan(Guid planId, DateTime newExpiration)
        {
            MembershipPlanId = planId;
            ExpirationDate = newExpiration;
            Status = MemberStatus.Active;
            UpdateTimestamp();
        }

        public bool IsActive => Status == MemberStatus.Active && ExpirationDate > DateTime.UtcNow;

        public bool CanGrantAccess()
        {
            if (Status == MemberStatus.Expired || ExpirationDate <= DateTime.UtcNow)
            {
                return false;
            }

            return Status == MemberStatus.Active;
        }
    }

        // Replaced by UpdateEmergencyContact
}