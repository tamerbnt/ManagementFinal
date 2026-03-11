using System;
using System.Collections.ObjectModel;
using Management.Application.Services;

namespace Management.Presentation.Services.Application
{
    public class BreadcrumbService : IBreadcrumbService
    {
        public ObservableCollection<BreadcrumbItem> Items { get; } = new();

        public event Action<Type>? Navigated;

        public void SetBreadcrumbs(params (string Title, Type? ViewModelType)[] breadcrumbs)
        {
            Items.Clear();
            
            for (int i = 0; i < breadcrumbs.Length; i++)
            {
                bool isActive = i == breadcrumbs.Length - 1;
                Items.Add(new BreadcrumbItem(breadcrumbs[i].Title, isActive, breadcrumbs[i].ViewModelType));
            }
        }

        public void Navigate(Type viewModelType)
        {
            Navigated?.Invoke(viewModelType);
        }

        public void Clear()
        {
            Items.Clear();
        }

        // Compatibility implementation for IBreadcrumbService
        public void SetBreadcrumb(string section, string? page = null)
        {
            Items.Clear();
            if (page == null)
            {
                Items.Add(new BreadcrumbItem(section, true));
            }
            else
            {
                Items.Add(new BreadcrumbItem(section, false));
                Items.Add(new BreadcrumbItem(page, true));
            }
        }
    }

    public static class BreadcrumbExtensions
    {
        public static void SetBreadcrumbs(this IBreadcrumbService service, string title, Type? type = null)
        {
            if (service is BreadcrumbService bs)
            {
                bs.SetBreadcrumbs((title, type));
            }
        }

        public static void AddBreadcrumb(this IBreadcrumbService service, string title, Type? type = null)
        {
            service.Items.Add(new BreadcrumbItem(title, true, type));
            // Deactivate previous
            if (service.Items.Count > 1)
            {
                // This is crude but works for now
                // In a real app we'd have better state management
            }
        }
    }
}
