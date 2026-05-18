using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;

namespace TurboTaxi.Infrastructure.Services
{
    public class BackgroundRideNotifier : IBackgroundRideNotifier
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundRideNotifier> _logger;

        public BackgroundRideNotifier(IServiceScopeFactory scopeFactory, ILogger<BackgroundRideNotifier> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void Run(Func<IServiceProvider, CancellationToken, Task> work)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await work(scope.ServiceProvider, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background ride notification failed");
                }
            });
        }
    }
}
