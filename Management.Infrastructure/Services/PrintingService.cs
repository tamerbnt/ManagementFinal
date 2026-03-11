using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Management.Application.Interfaces;
using Management.Domain.Models;

namespace Management.Infrastructure.Services
{
    // Task 3: Hardware I/O Safety
    public class PrintingService
    {
        // "Thread Isolation: All Socket/TCP connections to printers must be wrapped in Task.Run()."
        // "Strict Timeouts: Implement a CancellationTokenSource with a 2-second timeout."
        
        public async Task<bool> ConnectAsync(string ip, int port)
        {
            // Thread Isolation
            return await Task.Run(async () =>
            {
                using var client = new TcpClient();
                try
                {
                    // Strict Timeout
                    using var cts = new CancellationTokenSource(2000); // 2s timeout
                    
                    // Connect with cancellation support (if .NET allows, otherwise Task.WhenAny trickery or ConnectAsync overloading)
                    // .NET 5+ / Standard 2.1 has ConnectAsync(IPAddress, int, CancellationToken) or similar? 
                    // TcpClient.ConnectAsync usually doesn't take Token in older versions. 
                    // Pattern for timeout:
                    
                    var connectTask = client.ConnectAsync(ip, port);
                    var timeoutTask = Task.Delay(2000, cts.Token);
                    
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        // Timeout triggered
                        throw new OperationCanceledException("Connection timed out.");
                    }
                    
                    // Propagate exception if connect failed
                    await connectTask;

                    // Fallback handled by catch below
                    return true;
                }
                catch (OperationCanceledException)
                {
                    // Fallback: Return structured result (false here signifying failure) instead of crashing
                    return false;
                }
                catch (Exception)
                {
                    // Handle other connection errors
                    return false;
                }
            });
        }
    }
}
