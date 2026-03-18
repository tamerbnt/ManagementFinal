using System;
using System.Collections.Generic;
using System.Linq;
using Management.Application.DTOs;

using Management.Domain.Interfaces;

namespace Management.Application.Stores
{
    /// <summary>
    /// Stores the history of notifications and the state of the Unread Badge.
    /// Acts as the persistent memory for the Notification System.
    /// </summary>
    public class NotificationStore : IStateResettable
    {
        // Lock to protect _notifications from concurrent access by the UI thread
        // (ResetState, UnreadCount) and background Task.Run threads (ToastService.AddToast).
        private readonly object _lock = new();

        public void ResetState()
        {
            lock (_lock)
            {
                _notifications.Clear();
            }
            UnreadCountChanged?.Invoke();
        }

        // Event raised when the UnreadCount changes (e.g. new alert or mark as read)
        public event Action? UnreadCountChanged;

        // Event raised when a new notification is added to history
        public event Action<NotificationItem>? NotificationAdded;

        private readonly List<NotificationItem> _notifications = new List<NotificationItem>();

        // Null guard keeps us safe even if a corrupt entry slips through during a race.
        public int UnreadCount
        {
            get
            {
                lock (_lock)
                {
                    return _notifications.Count(n => n != null && !n.IsRead);
                }
            }
        }

        public IEnumerable<NotificationItem> Notifications
        {
            get
            {
                lock (_lock)
                {
                    return _notifications.ToList(); // snapshot to avoid out-of-sync enumeration
                }
            }
        }

        /// <summary>
        /// Adds a new notification to the store and increments the badge count.
        /// </summary>
        public void Add(string message, string title, NotificationType type)
        {
            var item = new NotificationItem
            {
                Id = Guid.NewGuid(),
                Message = message,
                Title = title,
                Type = type,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            };

            lock (_lock)
            {
                _notifications.Insert(0, item); // Add to top

                // Limit history to last 50 items to prevent memory bloat
                if (_notifications.Count > 50)
                {
                    _notifications.RemoveAt(_notifications.Count - 1);
                }
            }

            NotificationAdded?.Invoke(item);
            UnreadCountChanged?.Invoke();
        }

        /// <summary>
        /// Marks all notifications as read and clears the badge count.
        /// </summary>
        public void MarkAllAsRead()
        {
            lock (_lock)
            {
                foreach (var item in _notifications)
                {
                    if (item != null) item.IsRead = true;
                }
            }
            UnreadCountChanged?.Invoke();
        }

        /// <summary>
        /// Marks a single notification as read.
        /// </summary>
        public void MarkAsRead(Guid id)
        {
            NotificationItem? item;
            lock (_lock)
            {
                item = _notifications.FirstOrDefault(n => n != null && n.Id == id);
            }
            if (item != null && !item.IsRead)
            {
                item.IsRead = true;
                UnreadCountChanged?.Invoke();
            }
        }
    }

    // --- Helper DTOs for the Store ---

    public class NotificationItem
    {
        public Guid Id { get; set; }
        public required string Message { get; set; }
        public required string Title { get; set; }
        public NotificationType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    public enum NotificationType
    {
        Success,
        Error,
        Warning,
        Info
    }
}
