using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels; // For async queue
using Management.Application.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;

namespace Management.Infrastructure.Services
{
    /// <summary>
    /// ESC/POS thermal printer service using raw byte commands.
    /// Supports receipt printing and cash drawer control via serial port.
    /// </summary>
    public class EscPosPrinterService : IPrinterService, IDisposable
    {
        private readonly SerialPort _port;
        private readonly string _portName;
        private readonly IFacilityContextService? _facilityContext;
        private readonly Channel<Func<Task>> _printQueue;
        private readonly Task _processorTask;
        private bool _isDisposed;

        // ESC/POS Command Sequences
        private static readonly byte[] ESC_INIT = { 0x1B, 0x40 };                    // Initialize printer
        private static readonly byte[] ESC_ALIGN_CENTER = { 0x1B, 0x61, 0x01 };      // Center alignment
        private static readonly byte[] ESC_ALIGN_LEFT = { 0x1B, 0x61, 0x00 };        // Left alignment
        private static readonly byte[] ESC_BOLD_ON = { 0x1B, 0x45, 0x01 };           // Bold on
        private static readonly byte[] ESC_BOLD_OFF = { 0x1B, 0x45, 0x00 };          // Bold off
        private static readonly byte[] ESC_DOUBLE_HEIGHT = { 0x1B, 0x21, 0x10 };     // Double height
        private static readonly byte[] ESC_NORMAL_SIZE = { 0x1B, 0x21, 0x00 };       // Normal size
        private static readonly byte[] ESC_CUT_PAPER = { 0x1D, 0x56, 0x00 };         // Cut paper
        private static readonly byte[] ESC_DRAWER_KICK = { 0x1B, 0x70, 0x00, 0x19, 0xFA };  // Open cash drawer (pin 2)

        public EscPosPrinterService(
            IFacilityContextService? facilityContext = null,
            string portName = "COM1", 
            int baudRate = 9600)
        {
            _facilityContext = facilityContext;
            _portName = portName;
            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                WriteTimeout = 5000,
                ReadTimeout = 5000
            };

            _printQueue = Channel.CreateUnbounded<Func<Task>>();
            _processorTask = Task.Run(ProcessQueueAsync);
        }

        private async Task ProcessQueueAsync()
        {
            while (await _printQueue.Reader.WaitToReadAsync())
            {
                while (_printQueue.Reader.TryRead(out var job))
                {
                    try
                    {
                        await job();
                    }
                    catch (Exception)
                    {
                        // Log queue processing error
                    }
                }
            }
        }

        public async Task PrintTransactionAsync(Transaction transaction)
        {
            await _printQueue.Writer.WriteAsync(() => ExecutePrintTransactionAsync(transaction));
        }

        public async Task PrintOrderAsync(Management.Domain.Models.Restaurant.RestaurantOrder order)
        {
            await _printQueue.Writer.WriteAsync(() => ExecutePrintOrderAsync(order));
        }

        private async Task ExecutePrintOrderAsync(Management.Domain.Models.Restaurant.RestaurantOrder order)
        {
            try
            {
                if (!_port.IsOpen)
                    _port.Open();

                // Initialize printer
                await WriteAsync(ESC_INIT);
                await Task.Delay(100);

                // Header - BIG ORG
                var facilityName = _facilityContext?.CurrentFacility.ToString() ?? "Titan";
                
                await WriteAsync(ESC_ALIGN_CENTER);
                await WriteAsync(ESC_DOUBLE_HEIGHT);
                await WriteAsync(ESC_BOLD_ON);
                await WriteLineAsync($"*** {facilityName.ToUpper()} ***");
                await WriteLineAsync("RESTAURANT ORDER TICKET");
                await WriteLineAsync("");

                // ** TICKET NUMBER ** (BIG)
                await WriteAsync(ESC_DOUBLE_HEIGHT);
                await WriteLineAsync($"TICKET #{order.DailyOrderNumber}");
                await WriteAsync(ESC_NORMAL_SIZE);
                await WriteLineAsync("");

                // Order Details
                await WriteAsync(ESC_ALIGN_LEFT);
                await WriteAsync(ESC_BOLD_OFF);
                await WriteLineAsync($"Date: {order.CreatedAt:yyyy-MM-dd}");
                await WriteLineAsync($"Time: {order.CreatedAt:HH:mm:ss}");
                await WriteLineAsync($"Order: {order.Id.ToString().Substring(0, 8)}");
                if (!string.IsNullOrEmpty(order.TableNumber))
                {
                    await WriteLineAsync($"Table: {order.TableNumber}");
                }
                await WriteLineAsync("".PadRight(42, '-'));
                await WriteLineAsync("");

                // Line Items
                foreach (var item in order.Items)
                {
                    var line = $"{item.Name,-30} x{item.Quantity,2}";
                    await WriteLineAsync(line);
                }

                await WriteLineAsync("");
                await WriteLineAsync("".PadRight(42, '-'));
                await WriteLineAsync("");

                // Footer
                await WriteAsync(ESC_ALIGN_CENTER);
                await WriteLineAsync("Please keep this ticket.");
                await WriteLineAsync("Wait for your ticket number to be called.");
                await WriteLineAsync("");
                await WriteLineAsync("");

                // Cut paper
                await WriteAsync(ESC_CUT_PAPER);

                _port.Close();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Printer error on {_portName} (Order): {ex.Message}", ex);
            }
        }

        private async Task ExecutePrintTransactionAsync(Transaction transaction)
        {
            try
            {
                if (!_port.IsOpen)
                    _port.Open();

                // Initialize printer
                await WriteAsync(ESC_INIT);
                await Task.Delay(100);

                // Header - Centered, Bold, Double Height
                var facilityName = _facilityContext?.CurrentFacility.ToString() ?? "Titan";
                var headerText = $"TITAN {facilityName.ToUpper()}";

                await WriteAsync(ESC_ALIGN_CENTER);
                await WriteAsync(ESC_DOUBLE_HEIGHT);
                await WriteAsync(ESC_BOLD_ON);
                await WriteLineAsync(headerText);
                await WriteAsync(ESC_NORMAL_SIZE);
                await WriteAsync(ESC_BOLD_OFF);
                await WriteLineAsync("Professional Management System");
                await WriteLineAsync("Receipt of Transaction");
                await WriteLineAsync("");

                // Transaction Details
                await WriteAsync(ESC_ALIGN_LEFT);
                await WriteLineAsync($"Date: {transaction.Timestamp:yyyy-MM-dd HH:mm:ss}");
                await WriteLineAsync($"Transaction: {transaction.Id.ToString().Substring(0, 8)}");
                await WriteLineAsync("".PadRight(42, '-'));
                await WriteLineAsync("");

                // Line Items
                foreach (var item in transaction.Items)
                {
                    var line = $"{item.ProductName,-25} x{item.Quantity,2}";
                    var price = $"{item.Price:C}";
                    var total = $"{item.Total:C}";
                    
                    await WriteLineAsync(line);
                    await WriteLineAsync($"  @ {price,-10} = {total,10}");
                }

                await WriteLineAsync("");
                await WriteLineAsync("".PadRight(42, '-'));

                // Total - Bold, Double Height
                await WriteAsync(ESC_BOLD_ON);
                await WriteAsync(ESC_DOUBLE_HEIGHT);
                var totalLine = $"TOTAL:{transaction.TotalAmount,35:C}";
                await WriteLineAsync(totalLine);
                await WriteAsync(ESC_NORMAL_SIZE);
                await WriteAsync(ESC_BOLD_OFF);

                // Payment Method
                await WriteLineAsync($"Payment: {transaction.PaymentMethod}");
                await WriteLineAsync("");

                // Footer
                await WriteAsync(ESC_ALIGN_CENTER);
                await WriteLineAsync("Thank you for your business!");
                await WriteLineAsync("");
                await WriteLineAsync("");
                await WriteLineAsync("");

                // Cut paper
                await WriteAsync(ESC_CUT_PAPER);

                _port.Close();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Printer error on {_portName}: {ex.Message}", ex);
            }
        }

        public async Task OpenCashDrawerAsync()
        {
            await _printQueue.Writer.WriteAsync(async () => 
            {
                try
                {
                    if (!_port.IsOpen)
                        _port.Open();

                    await WriteAsync(ESC_DRAWER_KICK);
                    await Task.Delay(500);
                    _port.Close();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Cash drawer error on {_portName}: {ex.Message}", ex);
                }
            });
        }

        private async Task WriteAsync(byte[] data)
        {
            await _port.BaseStream.WriteAsync(data, 0, data.Length);
            await _port.BaseStream.FlushAsync();
        }

        private async Task WriteLineAsync(string text)
        {
            // Use Code Page 437 (DOS) for compatibility with most thermal printers
            var data = Encoding.GetEncoding(437).GetBytes(text + "\n");
            await WriteAsync(data);
        }

        public void Dispose()
        {
            if (_port?.IsOpen == true)
                _port.Close();
            _port?.Dispose();
        }
    }
}
