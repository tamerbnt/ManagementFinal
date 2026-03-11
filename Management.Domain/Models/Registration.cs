using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations.Schema;

namespace Management.Domain.Models
{
    /// <summary>
    /// Represents a Lead or Pending Member waiting for approval.
    /// </summary>
    public class Registration : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        // Contact Info
        public string FullName { get; private set; } = string.Empty;
        public Email Email { get; private set; } = null!;
        public PhoneNumber PhoneNumber { get; private set; } = null!;

        // Marketing Info
        public string Source { get; private set; } = string.Empty; // "Walk-in", "Instagram", etc.

        // Workflow
        public RegistrationStatus Status { get; private set; }
        public string Notes { get; private set; } = string.Empty;

        // Interest (Generic JSON Payload)
        public string InterestPayloadJson { get; private set; } = "{}";

        private IRegistrationMetadata? _metadata;

        [NotMapped]
        public IRegistrationMetadata Metadata 
        { 
            get => _metadata ??= DeserializeMetadata(); 
            private set 
            {
                _metadata = value;
                InterestPayloadJson = System.Text.Json.JsonSerializer.Serialize(value, value.GetType());
            }
        }

        // BACKWARD COMPATIBILITY: Proxies to Metadata
        public Guid? PreferredPlanId 
        { 
            get => Metadata is GymRegistrationMetadata gm ? gm.PreferredPlanId : GetInterestValue<Guid?>("PreferredPlanId");
            private set 
            { 
                if (Metadata is GymRegistrationMetadata gm) UpdateMetadata(gm with { PreferredPlanId = value }); 
                else SetInterestValue("PreferredPlanId", value); 
            }
        }

        public DateTime? PreferredStartDate 
        { 
            get => Metadata is GymRegistrationMetadata gm ? gm.PreferredStartDate : GetInterestValue<DateTime?>("PreferredStartDate");
            private set 
            { 
                if (Metadata is GymRegistrationMetadata gm) UpdateMetadata(gm with { PreferredStartDate = value }); 
                else SetInterestValue("PreferredStartDate", value); 
            }
        }

        private T? GetInterestValue<T>(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(InterestPayloadJson) || InterestPayloadJson == "{}") return default;
                using var doc = System.Text.Json.JsonDocument.Parse(InterestPayloadJson);
                if (doc.RootElement.TryGetProperty(key, out var prop))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<T>(prop.GetRawText());
                }
            } catch { }
            return default;
        }

        private void SetInterestValue<T>(string key, T value)
        {
            try
            {
                var dict = string.IsNullOrEmpty(InterestPayloadJson) || InterestPayloadJson == "{}"
                    ? new System.Collections.Generic.Dictionary<string, object?>()
                    : System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object?>>(InterestPayloadJson) 
                      ?? new System.Collections.Generic.Dictionary<string, object?>();
                
                dict[key] = value;
                InterestPayloadJson = System.Text.Json.JsonSerializer.Serialize(dict);
                UpdateTimestamp();
            } catch { }
        }

        private void UpdateMetadata(IRegistrationMetadata metadata)
        {
            _metadata = metadata;
            InterestPayloadJson = System.Text.Json.JsonSerializer.Serialize(metadata, metadata.GetType());
            UpdateTimestamp();
        }

        private IRegistrationMetadata DeserializeMetadata()
        {
            if (string.IsNullOrEmpty(InterestPayloadJson) || InterestPayloadJson == "{}") return null!;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(InterestPayloadJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("SegmentType", out var typeProp))
                {
                    var type = typeProp.GetString();
                    return type switch
                    {
                        "Gym" => System.Text.Json.JsonSerializer.Deserialize<GymRegistrationMetadata>(InterestPayloadJson)!,
                        "Salon" => System.Text.Json.JsonSerializer.Deserialize<SalonRegistrationMetadata>(InterestPayloadJson)!,
                        "Restaurant" => System.Text.Json.JsonSerializer.Deserialize<RestaurantRegistrationMetadata>(InterestPayloadJson)!,
                        _ => null!
                    };
                }
            } catch { }

            return null!;
        }

        private Registration(
            Guid id, 
            string fullName, 
            Email email, 
            PhoneNumber phoneNumber, 
            string source,
            Guid? preferredPlanId,
            DateTime? preferredStartDate,
            IRegistrationMetadata? metadata = null) : base(id)
        {
            FullName = fullName;
            Email = email;
            PhoneNumber = phoneNumber;
            Source = source;
            if (metadata != null)
            {
                Metadata = metadata;
            }
            else
            {
                PreferredPlanId = preferredPlanId;
                PreferredStartDate = preferredStartDate;
            }
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
            string notes,
            IRegistrationMetadata? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return Result.Failure<Registration>(new Error("Registration.EmptyName", "Name is required"));

            var registration = new Registration(Guid.NewGuid(), fullName, email, phoneNumber, source, preferredPlanId, preferredStartDate, metadata);
            registration.Notes = notes;
            return Result.Success(registration);
        }

        public void SetMetadata(IRegistrationMetadata metadata) => UpdateMetadata(metadata);

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
