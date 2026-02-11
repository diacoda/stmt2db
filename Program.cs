using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/log.json", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json",
                             optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var outputDir = PathHelper.ResolvePath(config["OutputDir"] ?? "csv-output");

            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(config);
                    services.AddScoped<StatementAnalyzer>();
                    services.AddScoped<StatementProcessor>();
                })
                .Build();

            await ProcessStatements(host, outputDir);

            Log.Warning("All done.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static async Task ProcessStatements(IHost host, string statementDir)
    {
        using var scope = host.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<StatementProcessor>();
        await processor.Run(statementDir);
    }
}
