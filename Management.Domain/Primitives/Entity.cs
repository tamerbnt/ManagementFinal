using System;
using Management.Domain.Primitives;
using Management.Domain.Interfaces;

namespace Management.Domain.Primitives
{
    public abstract class Entity : ITenantEntity, Management.Domain.Interfaces.ISyncable, IEquatable<Entity>
    {
        public Guid Id { get; set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime? UpdatedAt { get; private set; }
        
        DateTimeOffset ISyncable.UpdatedAt => UpdatedAt ?? CreatedAt;

        public bool IsDeleted { get; private set; }
        
        public bool IsSynced { get; set; }

        public Guid TenantId { get; set; }
        public byte[] RowVersion { get; private set; } = Array.Empty<byte>(); // For SQLite/Postgres concurrency

        protected Entity(Guid id)
        {
            Id = id;
            CreatedAt = DateTime.UtcNow;
        }

        protected Entity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        public void UpdateTimestamp()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        public void Delete()
        {
            IsDeleted = true;
            UpdateTimestamp();
        }

        public bool Equals(Entity? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Entity)obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(Entity? a, Entity? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }

        public static bool operator !=(Entity? a, Entity? b)
        {
            return !(a == b);
        }
    }
}
