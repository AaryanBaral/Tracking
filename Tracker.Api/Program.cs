using Tracker.Api.Configuration;
using Tracker.Api.Data;
using Tracker.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.AddTrackerApiServices();

var app = builder.Build();
await app.EnsureDatabaseConnectivityAsync();
app.UseTrackerApiPipeline();

app.MapGet("/", () => "Tracker API");

app.MapIngestEndpoints();
app.MapAuthEndpoints();
app.MapCompaniesEndpoints();
app.MapDevicesEndpoints();
app.MapHealthEndpoints();

try
{
    app.Logger.LogInformation("Tracker API starting.");
    await app.RunAsync();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Tracker API terminated unexpectedly.");
    throw;
}
