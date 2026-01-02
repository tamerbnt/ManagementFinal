using Polly;
using Polly.Retry;
using System;
using System.Net.Http;

namespace Management.Infrastructure.Configuration
{
    public static class ResiliencePolicyRegistry
    {
        // 1. Cloud Search Policy (Wait and Retry with Jitter)
        public static AsyncRetryPolicy CloudRetryPolicy => Policy
            .Handle<HttpRequestException>()
            .Or<Exception>(ex => ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase))
            .WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) 
                + TimeSpan.FromMilliseconds(new Random().Next(0, 100)));

        // 2. Hardware Controller Policy (Timeout & Circuit Breaker)
        public static AsyncRetryPolicy HardwareRetryPolicy => Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200));
            
        public static ISyncPolicy HardwareTimeoutPolicy => Policy
            .Timeout(TimeSpan.FromMilliseconds(500));
    }
}
