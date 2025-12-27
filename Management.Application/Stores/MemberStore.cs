using System;
using Management.Domain.DTOs;

namespace Management.Application.Stores
{
    /// <summary>
    /// Event Aggregator for Member-related actions.
    /// Allows disparate parts of the UI (Dashboard, Member List) to stay in sync
    /// without tight coupling or holding the entire database in memory.
    /// </summary>
    public class MemberStore
    {
        // Fired when a new member is successfully created in the database
        public event Action<MemberDto> MemberAdded;

        // Fired when an existing member's profile is modified
        public event Action<MemberDto> MemberUpdated;

        // Fired when a member is soft-deleted or removed
        public event Action<Guid> MemberDeleted;

        /// <summary>
        /// Broadcasts the addition of a new member.
        /// </summary>
        public void TriggerMemberAdded(MemberDto member)
        {
            MemberAdded?.Invoke(member);
        }

        /// <summary>
        /// Broadcasts an update to an existing member.
        /// </summary>
        public void TriggerMemberUpdated(MemberDto member)
        {
            MemberUpdated?.Invoke(member);
        }

        /// <summary>
        /// Broadcasts the deletion of a member by ID.
        /// </summary>
        public void TriggerMemberDeleted(Guid id)
        {
            MemberDeleted?.Invoke(id);
        }
    }
}