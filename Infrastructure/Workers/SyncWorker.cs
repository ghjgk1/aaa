using Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers
{
    public class SyncWorker : BackgroundService
    {
        public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(5);

        private readonly ILogger<SyncWorker> _logger;
        private readonly SyncService _syncService;
        private readonly string[] _args;
        private readonly IHostApplicationLifetime _appLifetime;

        public SyncWorker(
            ILogger<SyncWorker> logger,
            SyncService syncService,
            string[] args,
            IHostApplicationLifetime appLifetime)
        {
            _logger = logger;
            _syncService = syncService;
            _args = args;
            _appLifetime = appLifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var dryRun = _args.Contains("--dry-run");
            var runOnce = _args.Contains("--once");

            try
            {
                _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

                if (runOnce)
                {
                    await _syncService.SyncUsersAsync(dryRun);

                    _logger.LogInformation("Single run completed");
                    _appLifetime.StopApplication();
                    return;
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Starting sync cycle...");
                    try
                    {
                        await _syncService.SyncUsersAsync(dryRun);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Sync error");
                    }

                    await Task.Delay(SyncInterval, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogCritical(ex, "Fatal error");
                throw;
            }
        }
    }
}
