using Microsoft.Extensions.Options;

namespace EcommerceShop.Payments.Application;

public sealed class ReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReconciliationOptions _options;
    private readonly ILogger<ReconciliationWorker> _logger;

    public ReconciliationWorker(IServiceScopeFactory scopeFactory, IOptions<ReconciliationOptions> options, ILogger<ReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Scheduled reconciliation is off");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ReconciliationService>().RunAsync("schedule", stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Scheduled reconciliation failed");
            }
        }
    }
}
