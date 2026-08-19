using FocusAssistant.Configuration;
using FocusAssistant.Core.Config;
using FocusAssistant.Core.Data.Abstractions;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Monitoring;
using FocusAssistant.Core.Reports;
using FocusAssistant.Core.Session;
using FocusAssistant.Data.EF;
using FocusAssistant.Data.Queries;
using FocusAssistant.Platform.Monitoring;
using FocusAssistant.ViewModels;
using FocusAssistant.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// Builds the application host: configuration, logging, and the object graph.
    /// </summary>
    /// <remarks>
    /// Split out of App.xaml.cs so the composition root is one readable file rather
    /// than a method wedged between WPF lifecycle overrides.
    /// <para>
    /// The rule this file must keep: <b>nothing here performs I/O.</b> Building the
    /// host constructs objects and reads appsettings.json, and that is all. Creating
    /// directories, opening the database, and loading models are the jobs of hosted
    /// services, which run after the window is already on screen. Breaking that rule
    /// puts disk latency in front of the first frame.
    /// </para>
    /// </remarks>
    public static class AppHost
    {
        public static IHost Build()
        {
            var builder = Host.CreateApplicationBuilder();

            builder.Configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            ConfigureLogging(builder.Logging);
            ConfigureServices(builder.Services, builder.Configuration);

            return builder.Build();
        }

        private static void ConfigureLogging(ILoggingBuilder logging)
        {
            // A rolling file under LocalAppData, because the app spends most of its life
            // with no window open and a console nobody sees. Directory creation is the one
            // I/O exception in this file: the sink needs a path that exists before the
            // first write, and it is a single mkdir on a path we already own.
            Directory.CreateDirectory(AppPaths.LogDirectory);

            var serilog = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .WriteTo.File(
                    Path.Combine(AppPaths.LogDirectory, "focusassistant-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: true)
                .CreateLogger();

            logging.ClearProviders();
            logging.AddSerilog(serilog, dispose: true);
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // ---- Options ----
            services.Configure<MonitoringOptions>(configuration.GetSection(MonitoringOptions.SectionName));
            services.Configure<IntelligenceOptions>(configuration.GetSection(IntelligenceOptions.SectionName));
            services.Configure<PrivacyOptions>(configuration.GetSection(PrivacyOptions.SectionName));

            // ---- Database ----
            // A factory rather than a scoped DbContext: the consumers below are singletons
            // driven by polling timers, and a shared context would be used concurrently
            // from several threads.
            services.AddDbContextFactory<FocusAssistantDbContext>(options =>
                options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

            services.AddSingleton(typeof(IBaseService<>), typeof(BaseService<>));
            services.AddSingleton<AnalyticsServiceSQL>();

            // ---- Configuration ----
            services.AddSingleton<IAppCategorizationConfig, AppCategorizationConfig>();

            // ---- Monitoring and sessions ----
            services.AddSingleton<IProductivityStrategy, RuleBasedProductivityStrategy>();
            services.AddSingleton<IWindowMonitor>(sp => new WindowsApiWindowMonitor(
                sp.GetRequiredService<ILogger<WindowsApiWindowMonitor>>(),
                sp.GetRequiredService<IOptions<MonitoringOptions>>().Value.WindowPollInterval));
            services.AddSingleton<IIdleMonitor>(sp => new WindowsApiIdleMonitor(
                sp.GetRequiredService<ILogger<WindowsApiIdleMonitor>>(),
                sp.GetRequiredService<IOptions<MonitoringOptions>>().Value.IdleThreshold));
            services.AddSingleton<ISessionEngine, SessionEngine>();
            services.AddSingleton<WindowTracker>();
            services.AddSingleton<IReportGenerator, DailyReportGenerator>();

            // ---- Startup ----
            services.AddSingleton<StartupState>();
            services.AddHostedService<DatabaseMigrationHostedService>();

            // ---- Views and view models ----
            services.AddSingleton<MainWindow>();
            services.AddSingleton<TrackingViewModel>();
            services.AddSingleton<TrackingView>();
            services.AddTransient<AnalyticsViewModel>();
            services.AddTransient<AnalyticsView>();
            services.AddSingleton<RecommendationViewModel>();
            services.AddSingleton<RecommendationsView>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<DashboardView>();
        }
    }
}
