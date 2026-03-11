namespace Management.Presentation.ViewModels.Shared
{
    public class ActivityLogItem : IActivityItem
    {
        public string Title { get; set; }
        public string Status { get; set; }
        public string? StatusResourceKey { get; set; }
        public string Icon { get; set; }
        public string Initials { get; set; }
        public string Timestamp { get; set; }
        public string? ProfileImage { get; set; }
        
        // Helper date for sorting in unified streams
        public DateTime SortDate { get; set; } = DateTime.Now;

        public string Name => Title;
        public string AvatarInitials => Initials;

        public ActivityLogItem(string title, string status, string icon, string initials, string? statusResourceKey = null)
        {
            Title = title;
            Status = status;
            Icon = icon;
            Initials = initials;
            StatusResourceKey = statusResourceKey;
            Timestamp = System.DateTime.Now.ToString("HH:mm");
            SortDate = System.DateTime.Now;
        }
    }
}
