using Microsoft.Extensions.Hosting;
using WebApp.Services;

namespace WebApp.Services
{
    public class SocketServerHostedService : BackgroundService
    {
        private readonly SocketServer _socketServer;
        private readonly ILogger<SocketServerHostedService> _logger;

        public SocketServerHostedService(SocketServer socketServer, ILogger<SocketServerHostedService> logger)
        {
            _socketServer = socketServer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Iniciando Socket Server como background service");
                await _socketServer.StartAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error crítico en Socket Server: {ex.Message}");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deteniendo Socket Server");
            await base.StopAsync(cancellationToken);
        }
    }
}