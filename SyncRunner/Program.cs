using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using Application;
using Domain;
using Infrastructure.Data;
using Infrastructure.Directory;
using Infrastructure.Workers;
using System.Globalization;
using Gelf.Extensions.Logging;
using Serilog.Sinks.Graylog.Core.Transport;

public class Program
{
    public static void Main(string[] args)
    {
        if (!EventLog.SourceExists("AscDbADSyncService"))
        {
            EventLog.CreateEventSource("AscDbADSyncService", "Application");
        }

        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "AscDbADSyncService";
            })
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Регистрация аргументов командной строки
                services.AddSingleton<string[]>(args);

                // Конфигурация
                var configuration = hostContext.Configuration;

                // Database
                services.AddDbContext<AscDbContext>(options =>
                    options.UseSqlServer(configuration.GetConnectionString("AscDb")));

                // Регистрация репозитория базы данных
                services.AddScoped<ISyncRepository>(provider =>
                    new EfSyncRepository(
                        provider.GetRequiredService<AscDbContext>(),
                        provider.GetRequiredService<ILogger<EfSyncRepository>>()));

                // LDAP
                var adConfig = configuration.GetSection("Ad");
                var fieldMappings = configuration.GetSection("FieldMappings")
                    .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

                services.AddSingleton<ISyncRepository>(provider =>
                    new LdapSyncRepository(
                        adConfig["LdapPath"],
                        adConfig["Username"],
                        adConfig["Password"],
                        fieldMappings,
                        provider.GetRequiredService<ILogger<LdapSyncRepository>>()));

                // Основной сервис синхронизации
                services.AddScoped<SyncService>(provider =>
                    new SyncService(
                        provider.GetServices<ISyncRepository>().First(x => x is EfSyncRepository),
                        provider.GetServices<ISyncRepository>().First(x => x is LdapSyncRepository),
                        provider.GetRequiredService<ILogger<SyncService>>(),
                        fieldMappings,
                        adConfig["SearchBy"]));

                // Фоновый сервис
                services.AddHostedService<SyncWorker>();

                services.Configure<HostOptions>(opts =>
                {
                    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                });
            })
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddEventLog(settings =>
                        {
                            settings.SourceName = "AscDbADSyncService";
                            settings.LogName = "Application";
                        })
                       .AddConfiguration(hostingContext.Configuration.GetSection("Logging"))
                       .AddConsole()
                       .AddSentry(options =>
                       {
                           var sentryConfig = hostingContext.Configuration.GetSection("Sentry");
                           options.Dsn = sentryConfig["Dsn"];
                           options.Environment = sentryConfig["Environment"];
                           options.Debug = bool.Parse(sentryConfig["Debug"] ?? "false");
                           options.TracesSampleRate = double.Parse(
                               sentryConfig["TracesSampleRate"] ?? "1",
                               CultureInfo.InvariantCulture);
                       });

                var graylogConfig = hostingContext.Configuration.GetSection("Graylog");
                if (!string.IsNullOrEmpty(graylogConfig["Host"]))
                {
                    logging.AddGelf(options =>
                    {
                        options.Host = graylogConfig["Host"];
                        options.Port = int.Parse(graylogConfig["Port"] ?? "12201");
                        options.Protocol = graylogConfig["Protocol"]?.ToUpper() == "UDP"
                            ? GelfProtocol.Udp
                            : GelfProtocol.Http;
                        options.AdditionalFields["facility"] = graylogConfig["Facility"];
                        options.LogSource = Environment.MachineName;
                    });
                }
            });
}