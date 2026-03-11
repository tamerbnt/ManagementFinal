using Management.Domain.Common;
using Management.Domain.Interfaces;

namespace Management.Domain.Common
{
    public abstract class BaseEntity : ITenantEntity, ISyncable, IEquatable<BaseEntity>
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastModifiedAt { get; private set; }
        public bool IsDeleted { get; private set; }
        public Guid FacilityId { get; set; }
        public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
        public bool IsSynced { get; set; }

        public DateTimeOffset UpdatedAt => LastModifiedAt ?? CreatedAt;

        Guid ITenantEntity.TenantId 
        { 
            get => FacilityId; 
            set => FacilityId = value; 
        }

        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        protected BaseEntity(Guid id)
        {
            Id = id;
            CreatedAt = DateTime.UtcNow;
        }

        public void Delete()
        {
            IsDeleted = true;
        }

        public void UpdateTimestamp()
        {
            LastModifiedAt = DateTime.UtcNow;
        }

        public override bool Equals(object? obj) => Equals(obj as BaseEntity);

        public bool Equals(BaseEntity? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Id == Guid.Empty || other.Id == Guid.Empty) return false;
            return Id == other.Id;
        }

        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(BaseEntity? left, BaseEntity? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(BaseEntity? left, BaseEntity? right) => !(left == right);
    }
}
