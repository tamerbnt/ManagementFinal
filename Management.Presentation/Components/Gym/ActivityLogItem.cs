using System;

namespace Management.Presentation.Components.Gym
{
    public class ActivityLogItem
    {
        public string Name { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Granted, Denied, WalkIn
        public string Icon { get; set; } = string.Empty;
        public string AvatarInitials { get; set; } = string.Empty;

        public ActivityLogItem() { }

        public ActivityLogItem(string name, string status, string icon, string initials = "")
        {
            Name = name;
            Time = DateTime.Now.ToString("h:mm tt");
            Status = status;
            Icon = icon;
            AvatarInitials = string.IsNullOrEmpty(initials) ? (name.Length > 0 ? name[0].ToString() : "") : initials;
        }
    }
}
