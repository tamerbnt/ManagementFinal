using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Behaviors
{
    /// <summary>
    /// MediatR pipeline behavior that logs performance metrics for long-running requests.
    /// </summary>
    public class PerformanceBehavior<TRequest, TResponse> 
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly Stopwatch _timer;
        private readonly ILogger<TRequest> _logger;

        public PerformanceBehavior(ILogger<TRequest> logger)
        {
            _timer = new Stopwatch();
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request, 
            RequestHandlerDelegate<TResponse> next, 
            CancellationToken cancellationToken)
        {
            _timer.Start();

            var response = await next();

            _timer.Stop();

            var elapsedMilliseconds = _timer.ElapsedMilliseconds;

            if (elapsedMilliseconds > 200)
            {
                var requestName = typeof(TRequest).Name;

                _logger.LogWarning(
                    "🔴 Performance Alert: {Name} took {ElapsedMilliseconds}ms (Threshold: 200ms)",
                    requestName, elapsedMilliseconds);
            }
            else if (elapsedMilliseconds > 50)
            {
                var requestName = typeof(TRequest).Name;

                _logger.LogInformation(
                    "🟡 Metric: {Name} took {ElapsedMilliseconds}ms",
                    requestName, elapsedMilliseconds);
            }

            return response;
        }
    }
}
