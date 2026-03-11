using System;
using System.Collections.ObjectModel;

namespace Management.Application.Services
{
    public interface IBreadcrumbService
    {
        ObservableCollection<BreadcrumbItem> Items { get; }
        
        void SetBreadcrumb(string section, string? page = null);
        void SetBreadcrumbs(params (string Title, Type? ViewModelType)[] breadcrumbs);
    }

    public record BreadcrumbItem(string Title, bool IsActive, Type? ViewModelType = null);
}
