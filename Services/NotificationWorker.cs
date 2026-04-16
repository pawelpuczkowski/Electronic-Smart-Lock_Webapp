using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZamekDoDrzwi.Services
{
    public class NotificationWorker : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<NotificationWorker> _logger;

        public NotificationWorker(IConfiguration config, ILogger<NotificationWorker> logger)
        {
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationWorker uruchomiony.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    NotificationProcessor.ProcessNewEvents(_config);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd workera powiadomień.");
                }

                // jeśli coś było wysyłane – krótki delay, jeśli pusto – dłuższy
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
