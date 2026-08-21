using AvaBot.Application;
using AvaBot.Domain.Interfaces;
using AvaBot.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AvaBot.Console;

internal static class ConsoleBootstrapper
{
    public static async Task<int> RunAsync()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            using var host = CreateHost();
            await host.StartAsync();

            var runner = host.Services.GetRequiredService<IBotRunner>();
            var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var exitCode = await runner.RunAsync(applicationLifetime.ApplicationStopping);

            await host.StopAsync();
            return exitCode;
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Execução do AvaBot cancelada");
            return 2;
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "AvaBot encerrado inesperadamente");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static IHost CreateHost() =>
        new HostBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseConsoleLifetime()
            .ConfigureAppConfiguration((_, configuration) => configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true))
            .UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext())
            .ConfigureServices((context, services) =>
            {
                services.AddApplication(context.Configuration);
                services.AddInfrastructure(context.Configuration);
            })
            .Build();
}
