using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Management.Presentation.ViewModels.Shared
{
    public class ActivityLogItem : ObservableObject, IActivityItem
    {
        public string Title { get; set; }
        
        private string _status = string.Empty;
        public string Status 
        { 
            get => _status; 
            set => SetProperty(ref _status, value); 
        }
        
        public string? StatusResourceKey { get; set; }
        public string Icon { get; set; }
        public string Initials { get; set; }
        public string Timestamp { get; set; }
        public string? ProfileImage { get; set; }
        
        // Helper date for sorting in unified streams
        public DateTime SortDate { get; set; } = DateTime.Now;

        public string RelativeTime
        {
            get
            {
                var diff = System.DateTime.Now - SortDate;
                if (diff.TotalMinutes < 1) 
                    return System.Windows.Application.Current.TryFindResource("Terminology.Global.Time.Now") as string ?? "now";
                
                if (diff.TotalHours < 1)
                {
                    var format = System.Windows.Application.Current.TryFindResource("Terminology.Global.Time.MinAgo") as string ?? "{0}min ago";
                    return string.Format(format, (int)diff.TotalMinutes);
                }
                
                if (diff.TotalDays < 1)
                {
                    var format = System.Windows.Application.Current.TryFindResource("Terminology.Global.Time.HoursAgo") as string ?? "{0}h ago";
                    return string.Format(format, (int)diff.TotalHours);
                }
                
                var daysFormat = System.Windows.Application.Current.TryFindResource("Terminology.Global.Time.DaysAgo") as string ?? "{0}d ago";
                return string.Format(daysFormat, (int)diff.TotalDays);
            }
        }

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
