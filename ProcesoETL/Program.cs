using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ProcesoETL;
using ProcesoETL.Application.Services;
using ProcesoETL.Core.Configuration;
using ProcesoETL.Core.Interfaces;
using ProcesoETL.Infrastructure.Services;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/etl-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting ETL Worker Service");

    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog
    builder.Services.AddSerilog();

    // Configure settings
    builder.Services.Configure<DataSourceSettings>(
        builder.Configuration.GetSection("DataSources"));
    builder.Services.Configure<ETLSettings>(
        builder.Configuration.GetSection("ETLSettings"));

    // Register DbContext
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("AnalyticsDb"),
            sqlOptions => sqlOptions.EnableRetryOnFailure()));

    // Register services
    builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<AppDbContext>());
    builder.Services.AddScoped<IStagingService, StagingService>();
    builder.Services.AddScoped<IDataLoader, DataLoader>();
    builder.Services.AddScoped<ETLPipeline>();

    // Register HttpClient with Polly policies
    builder.Services.AddHttpClient("ETLClient", client =>
    {
        var apiBaseUrl = builder.Configuration["DataSources:ApiBaseUrl"];
        if (!string.IsNullOrEmpty(apiBaseUrl))
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        }
        
        var apiKey = builder.Configuration["DataSources:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
        
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // Register Worker
    builder.Services.AddHostedService<ETLWorker>();

    var host = builder.Build();

    // Ensure database is created
    using (var scope = host.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();
        Log.Information("Database initialized");
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
