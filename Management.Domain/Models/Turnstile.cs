using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class Turnstile : AggregateRoot
    {
        public string Name { get; private set; }
        public string Location { get; private set; }
        public string HardwareId { get; private set; }
        public TurnstileStatus Status { get; private set; }
        public bool IsLocked { get; private set; }

        private Turnstile(Guid id, string name, string location, string hardwareId) : base(id)
        {
            Name = name;
            Location = location;
            HardwareId = hardwareId;
            Status = TurnstileStatus.Operational;
            IsLocked = true;
        }

        private Turnstile() 
        { 
            Name = string.Empty; 
            Location = string.Empty; 
            HardwareId = string.Empty; 
        }

        public static Result<Turnstile> Register(string name, string location, string hardwareId)
        {
             if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<Turnstile>(new Error("Turnstile.EmptyName", "Name is required"));
             
             if (string.IsNullOrWhiteSpace(hardwareId))
                return Result.Failure<Turnstile>(new Error("Turnstile.EmptyHardwareId", "HardwareId is required"));

            return Result.Success(new Turnstile(Guid.NewGuid(), name, location, hardwareId));
        }

        public void Unlock()
        {
            IsLocked = false;
            UpdateTimestamp();
        }

        public void Lock()
        {
            IsLocked = true;
            UpdateTimestamp();
        }

        public void UpdateStatus(TurnstileStatus status)
        {
            Status = status;
            UpdateTimestamp();
        }
    }
}