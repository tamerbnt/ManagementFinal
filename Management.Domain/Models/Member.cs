using System;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using Management.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Management.Domain.Models
{
    public class Member : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        // Core Identity
        public string FullName { get; private set; } = string.Empty;
        public Email Email { get; private set; } = null!;
        public PhoneNumber PhoneNumber { get; private set; } = null!;

        // Physical Access
        public string ProfileImageUrl { get; private set; } = string.Empty;
        public string SegmentDataJson { get; private set; } = "{}";

        private IMemberMetadata? _metadata;

        [NotMapped]
        public IMemberMetadata Metadata 
        { 
            get => _metadata ??= DeserializeMetadata(); 
            private set 
            {
                _metadata = value;
                SegmentDataJson = System.Text.Json.JsonSerializer.Serialize(value, value.GetType());
            }
        }

        // Membership Status
        public MemberStatus Status { get; private set; }
        
        // BACKWARD COMPATIBILITY: Proxies to Metadata if it's a GymMemberMetadata
        public DateTime StartDate 
        { 
            get => _startDate;
            private set 
            { 
                _startDate = value;
                if (Metadata is GymMemberMetadata gm) UpdateMetadata(gm with { StartDate = value });
            }
        }
        private DateTime _startDate;

        public DateTime ExpirationDate 
        { 
            get => _expirationDate;
            private set 
            { 
                _expirationDate = value;
                if (Metadata is GymMemberMetadata gm) UpdateMetadata(gm with { ExpirationDate = value });
            }
        }
        private DateTime _expirationDate;

        public Guid? MembershipPlanId 
        { 
            get => _membershipPlanId;
            private set 
            { 
                _membershipPlanId = value;
                if (Metadata is GymMemberMetadata gm) UpdateMetadata(gm with { MembershipPlanId = value });
            }
        }
        private Guid? _membershipPlanId;

        public string CardId 
        { 
            get => _cardId;
            private set 
            { 
                _cardId = value ?? string.Empty;
                if (Metadata is GymMemberMetadata gm) UpdateMetadata(gm with { CardId = _cardId });
            }
        }
        private string _cardId = string.Empty;

        public int RemainingSessions 
        { 
            get => _remainingSessions;
            private set 
            { 
                _remainingSessions = value;
                if (Metadata is GymMemberMetadata gm) UpdateMetadata(gm with { RemainingSessions = value });
            }
        }
        private int _remainingSessions;

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

        // Infrastructure Reconstruction Constructor
        public Member(
            Guid id,
            string fullName,
            string email,
            string phoneNumber,
            string cardId,
            string profileImageUrl,
            MemberStatus status,
            DateTime startDate,
            DateTime expirationDate,
            Guid? membershipPlanId,
            Gender gender,
            int remainingSessions) : base(id)
        {
            FullName = fullName;
            Email = Email.Create(email).Value; 
            PhoneNumber = PhoneNumber.Create(phoneNumber).Value;
            CardId = cardId;
            ProfileImageUrl = profileImageUrl;
            Status = status;
            StartDate = startDate;
            ExpirationDate = expirationDate;
            MembershipPlanId = membershipPlanId;
            Gender = gender;
            RemainingSessions = remainingSessions;
        }

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

        public Gender Gender { get; private set; }

        public bool CanGrantAccess()
        {
            if (Status == MemberStatus.Expired || ExpirationDate <= DateTime.UtcNow)
            {
                return false;
            }

            return Status == MemberStatus.Active;
        }

        public void SetRemainingSessions(int newCount)
        {
            RemainingSessions = newCount;
            if (RemainingSessions < 0) RemainingSessions = 0;
            UpdateTimestamp();
        }

        // Metadata Management
        private void UpdateMetadata(IMemberMetadata metadata)
        {
            _metadata = metadata;
            SegmentDataJson = System.Text.Json.JsonSerializer.Serialize(metadata, metadata.GetType());
            UpdateTimestamp();
        }

        private IMemberMetadata DeserializeMetadata()
        {
            if (string.IsNullOrEmpty(SegmentDataJson) || SegmentDataJson == "{}")
            {
                // Fallback: Try to migrate legacy fields if they exist (from DB load)
                return new GymMemberMetadata
                {
                    MembershipPlanId = _membershipPlanId,
                    CardId = _cardId,
                    RemainingSessions = _remainingSessions,
                    StartDate = _startDate,
                    ExpirationDate = _expirationDate
                };
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(SegmentDataJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("SegmentType", out var typeProp))
                {
                    var type = typeProp.GetString();
                    return type switch
                    {
                        "Gym" => System.Text.Json.JsonSerializer.Deserialize<GymMemberMetadata>(SegmentDataJson)!,
                        "Salon" => System.Text.Json.JsonSerializer.Deserialize<SalonMemberMetadata>(SegmentDataJson)!,
                        "Restaurant" => System.Text.Json.JsonSerializer.Deserialize<RestaurantMemberMetadata>(SegmentDataJson)!,
                        _ => new GymMemberMetadata() // Default
                    };
                }
            } catch { }

            return new GymMemberMetadata();
        }

        public void SetMetadata(IMemberMetadata metadata) => UpdateMetadata(metadata);
    }

        // Replaced by UpdateEmergencyContact
}
