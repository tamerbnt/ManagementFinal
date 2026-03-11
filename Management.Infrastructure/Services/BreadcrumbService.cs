using System;
using System.Collections.ObjectModel;
using Management.Application.Services;
using Management.Application.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Management.Infrastructure.Services
{
    public class BreadcrumbService : ObservableObject, IBreadcrumbService
    {
        private readonly IDispatcherService _dispatcherService;

        public BreadcrumbService(IDispatcherService dispatcherService)
        {
            _dispatcherService = dispatcherService;
        }

        public ObservableCollection<BreadcrumbItem> Items { get; } = new();

        public void SetBreadcrumb(string section, string? page = null)
        {
            _dispatcherService.Invoke(() =>
            {
                Items.Clear();

                if (page == null)
                {
                    // We are at the section root
                    Items.Add(new BreadcrumbItem(section, true));
                }
                else
                {
                    // We are deep in a page
                    Items.Add(new BreadcrumbItem(section, false));
                    Items.Add(new BreadcrumbItem(page, true));
                }
            });
        }

        public void SetBreadcrumbs(params (string Title, Type? ViewModelType)[] breadcrumbs)
        {
            _dispatcherService.Invoke(() =>
            {
                Items.Clear();
                foreach (var (title, type) in breadcrumbs)
                {
                    bool isActive = (title == breadcrumbs[^1].Title);
                    Items.Add(new BreadcrumbItem(title, isActive, type));
                }
            });
        }
    }
}
