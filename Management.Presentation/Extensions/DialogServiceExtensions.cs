using System.Threading.Tasks;
using Management.Domain.Services;

namespace Management.Presentation.Extensions
{
    public static class DialogServiceExtensions
    {
        public static Task<string?> ShowImageUploadDialogAsync(this IDialogService service)
        {
            return service.ShowOpenFileDialogAsync(
                filter: "Images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp");
        }

        public static Task<string?> ShowCsvImportDialogAsync(this IDialogService service)
        {
            return service.ShowOpenFileDialogAsync(
                filter: "CSV Files (*.csv)|*.csv");
        }

        public static Task<string?> ShowReportExportDialogAsync(this IDialogService service, string reportName)
        {
            return service.ShowSaveFileDialogAsync(
                defaultName: $"{reportName}_{System.DateTime.Now:yyyyMMdd}",
                filter: "PDF Document (*.pdf)|*.pdf|Excel Workbook (*.xlsx)|*.xlsx");
        }
    }
}
