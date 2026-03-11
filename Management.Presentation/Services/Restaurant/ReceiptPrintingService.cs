using System;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Models.Restaurant;
using Management.Domain.Models.Salon; // Assuming Gym models are here or in a separate namespace

namespace Management.Presentation.Services.Restaurant
{
    public interface IReceiptPrintingService
    {
        Task PrintRestaurantReceiptAsync(Guid facilityId, RestaurantOrder order);
        Task PrintSalonReceiptAsync(Guid facilityId, Appointment appointment, decimal total);
        Task PrintGymMembershipReceiptAsync(Guid facilityId, object member, string planName, decimal price);
    }

    public class ReceiptPrintingService : IReceiptPrintingService
    {
        private readonly INotificationService _notificationService;
        private const int ReceiptWidth = 40;

        public ReceiptPrintingService(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public Task PrintRestaurantReceiptAsync(Guid facilityId, RestaurantOrder order)
        {
            return Task.Run(() =>
            {
                try
                {
                    var sb = new StringBuilder();
                    // ... (Original logic)
                    sb.AppendLine("╔" + new string('═', ReceiptWidth - 2) + "╗");
                    sb.AppendLine("║" + CenterText("RESTAURANT RECEIPT", ReceiptWidth - 2) + "║");
                    sb.AppendLine("╚" + new string('═', ReceiptWidth - 2) + "╝");
                    sb.AppendLine();
                    sb.AppendLine($"Date: {DateTime.Now:g}");
                    sb.AppendLine($"Table No: {order.TableNumber}");
                    sb.AppendLine(new string('─', ReceiptWidth));
                    
                    foreach (var item in order.Items)
                    {
                        string line = $"{item.Quantity}x {item.Name}";
                        string price = (item.Price * item.Quantity).ToString("N2") + " DA";
                        sb.AppendLine(line.PadRight(ReceiptWidth - price.Length) + price);
                    }

                    sb.AppendLine(new string('─', ReceiptWidth));
                    sb.AppendLine("TOTAL:".PadRight(ReceiptWidth - (order.Total.ToString("N2") + " DA").Length) + order.Total.ToString("N2") + " DA");
                    sb.AppendLine();
                    sb.AppendLine(CenterText("THANK YOU!", ReceiptWidth));
                    
                    Console.WriteLine(sb.ToString());
                    // Simulate hardware success
                }
                catch (Exception)
                {
                    _notificationService.ShowError("Printer Disconnected", "Ensure hardware is connected and powered on.");
                }
            });
        }

        public Task PrintSalonReceiptAsync(Guid facilityId, Appointment appointment, decimal total)
        {
            return Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("╔" + new string('═', ReceiptWidth - 2) + "╗");
                sb.AppendLine("║" + CenterText("SALON RECEIPT", ReceiptWidth - 2) + "║");
                sb.AppendLine("╚" + new string('═', ReceiptWidth - 2) + "╝");
                sb.AppendLine();
                sb.AppendLine($"Date: {DateTime.Now:g}");
                sb.AppendLine($"Client: {appointment.ClientName}");
                sb.AppendLine($"Staff: {appointment.StaffName}");
                sb.AppendLine(new string('─', ReceiptWidth));
                sb.AppendLine($"Service: {appointment.ServiceName}");
                
                if (appointment.UsedProducts?.Any() == true)
                {
                    sb.AppendLine("Products:");
                    foreach (var p in appointment.UsedProducts)
                    {
                        string line = $" - {p.ProductName}";
                        string val = p.Total.ToString("N2") + " DA";
                        sb.AppendLine(line.PadRight(ReceiptWidth - val.Length) + val);
                    }
                }

                sb.AppendLine(new string('─', ReceiptWidth));
                sb.AppendLine("GRAND TOTAL:".PadRight(ReceiptWidth - (total.ToString("N2") + " DA").Length) + total.ToString("N2") + " DA");
                sb.AppendLine();
                sb.AppendLine(CenterText("STAY BEAUTIFUL", ReceiptWidth));
                
                Console.WriteLine(sb.ToString());
            });
        }

        public Task PrintGymMembershipReceiptAsync(Guid facilityId, object member, string planName, decimal price)
        {
             return Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("╔" + new string('═', ReceiptWidth - 2) + "╗");
                sb.AppendLine("║" + CenterText("GYM MEMBERSHIP", ReceiptWidth - 2) + "║");
                sb.AppendLine("╚" + new string('═', ReceiptWidth - 2) + "╝");
                sb.AppendLine();
                sb.AppendLine($"Date: {DateTime.Now:g}");
                sb.AppendLine($"Plan: {planName}");
                sb.AppendLine(new string('─', ReceiptWidth));
                sb.AppendLine("Amount:".PadRight(ReceiptWidth - (price.ToString("N2") + " DA").Length) + price.ToString("N2") + " DA");
                sb.AppendLine();
                sb.AppendLine(CenterText("STAY FIT!", ReceiptWidth));
                
                Console.WriteLine(sb.ToString());
            });
        }

        private string CenterText(string text, int width)
        {
            if (text.Length >= width) return text;
            int leftPadding = (width - text.Length) / 2;
            return text.PadLeft(leftPadding + text.Length).PadRight(width);
        }
    }
}
