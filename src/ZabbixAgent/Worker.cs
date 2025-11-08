namespace ZabbixAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AgentService _agent;

    public Worker(ILogger<Worker> logger, AgentService agent)
    {
        _logger = logger;
        _agent = agent;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("App version: {version}", Assembly.GetExecutingAssembly().GetName().Version?.ToString());

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await _agent.StartAsync();
            await Task.Delay(-1, stoppingToken);
        }
    }
}
