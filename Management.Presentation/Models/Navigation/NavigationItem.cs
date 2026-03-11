using System;

namespace Management.Presentation.Models.Navigation
{
    public class NavigationItem
    {
        public string Title { get; set; }
        public string IconData { get; set; }
        public Type TargetViewModel { get; set; }

        public NavigationItem(string title, string iconData, Type targetViewModel)
        {
            Title = title;
            IconData = iconData;
            TargetViewModel = targetViewModel;
        }
    }
}
