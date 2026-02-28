using Agent.Service.Collectors;
using Agent.Service.Identity;
using Agent.Service.Infrastructure.Outbox;
using Agent.Service.Tracking;
using Agent.Service.Workers;
using Agent.Shared.Abstractions;
using Agent.Shared.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Agent.Service.Infrastructure;
using Agent.Service;

var builder = Host.CreateDefaultBuilder(args);

// This makes the host use Windows Service lifetime when actually running as a service.
if (WindowsServiceHelpers.IsWindowsService())
{
    builder.UseWindowsService(options =>
    {
        options.ServiceName = "EmployeeTrackerAgent";
    });
}

builder.ConfigureServices((context, services) =>
{
    // Config
    services.Configure<AgentConfig>(context.Configuration.GetSection("Agent"));

    // Collectors
    services.AddSingleton<IIdleCollector, NoopIdleCollector>();
    services.AddSingleton<IAppCollector, NoopAppCollector>();

    // Identity
    services.AddSingleton<DeviceIdentityStore>();

    // Persistence (Outbox)
    services.AddSingleton<OutboxRepository>();
    services.AddSingleton<IOutboxService, OutboxService>();
    services.AddSingleton<OutboxSenderState>();

    // Sessionization
    services.AddSingleton<WebSessionizer>();
    services.AddSingleton<AppSessionizer>();
    services.AddSingleton<IdleSessionizer>();

    // Infrastructure
    services.AddHttpClient("backend")
        .ConfigureHttpClient((serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<AgentConfig>>().Value;
            if (!string.IsNullOrWhiteSpace(config.CompanyEnrollmentKey))
            {
                client.DefaultRequestHeaders.Add("X-Company-Enroll", config.CompanyEnrollmentKey);
            }
        });

    // Workers
    services.AddHostedService<ShutdownFlushService>();
    services.AddHostedService<OutboxSenderWorker>();
    services.AddHostedService<DeviceSessionWorker>();
    services.AddHostedService<Worker>(); // Local API + Web Events
    services.AddHostedService<HeartbeatWorker>();

});

var host = builder.Build();
var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

// Init DB
var outboxRepo = host.Services.GetRequiredService<OutboxRepository>();
outboxRepo.Init();

try
{
    startupLogger.LogInformation("Agent.Service starting.");
    host.Run();
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Agent.Service terminated unexpectedly.");
    throw;
}
