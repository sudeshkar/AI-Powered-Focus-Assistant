using FocusAssistant.Configuration;
using FocusAssistant.Core.Config;
using FocusAssistant.Core.Data.Abstractions;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Monitoring;
using FocusAssistant.Core.Reports;
using FocusAssistant.Core.Session;
using FocusAssistant.Data.EF;
using FocusAssistant.Data.Queries;
using FocusAssistant.Intelligence.Classification;
using FocusAssistant.Intelligence.Embeddings;
using FocusAssistant.Intelligence.Scoring;
using FocusAssistant.Intelligence.Slm;
using FocusAssistant.Core.Intelligence;
using System.Net.Http;
using FocusAssistant.Platform.Monitoring;
using FocusAssistant.Platform.Startup;
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
            services.AddSingleton<DayQueryService>();

            // ---- Configuration ----
            services.AddSingleton<IAppCategorizationConfig, AppCategorizationConfig>();

            // ---- Classification ----
            // The ruleset is registered concretely and then bound to both interfaces it
            // implements, so there is exactly one instance and therefore one set of caches.
            // IProductivityStrategy survives for the callers that still want a plain bool;
            // IRuleMatcher is the richer form the layered classifier consumes.
            services.AddSingleton<RuleBasedProductivityStrategy>();
            services.AddSingleton<IProductivityStrategy>(sp => sp.GetRequiredService<RuleBasedProductivityStrategy>());
            services.AddSingleton<IRuleMatcher>(sp => sp.GetRequiredService<RuleBasedProductivityStrategy>());

            // Until the "This is work" button exists there is nothing to store, but the
            // layer is registered so the classifier is written the same way either way.
            services.AddSingleton<IUserOverrideStore, NoUserOverrideStore>();

            // The generator itself is a registration so its lifetime and disposal are the
            // container's problem, not something two factory lambdas have to coordinate.
            services.AddSingleton(_ => MiniLmEmbeddingGenerator.Load(
                Path.Combine(AppContext.BaseDirectory, "Models", "minilm")));

            AddIntelligence(services);

            services.AddSingleton<IActivityClassifier, LayeredActivityClassifier>();

            // ---- Monitoring and sessions ----
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
            services.AddHostedService<EmbeddingWarmupHostedService>();
            services.AddHostedService<ClassificationRefinementService>();
            services.AddHostedService<SessionRecoveryService>();
            services.AddHostedService<TrackingHostedService>();
            services.AddSingleton<IAutoStartService, AutoStartService>();

            // ---- Views and view models ----
            services.AddSingleton<MainWindow>();
            services.AddSingleton<TrackingViewModel>();
            services.AddSingleton<TrackingView>();
            services.AddTransient<InsightsViewModel>();
            services.AddTransient<InsightsView>();
            services.AddTransient<TodayViewModel>();
            services.AddTransient<TodayView>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsView>();
        }

        /// <summary>
        /// Registers the on-device model layer, or abstaining stand-ins for it.
        /// </summary>
        /// <remarks>
        /// Loading the ONNX session is the one piece of I/O that cannot be deferred to a
        /// hosted service, because the object it produces is the dependency. It is cheap
        /// (~150 ms, measured) and it happens once, but if it throws - a corrupt file, an
        /// unsupported CPU - the whole graph would fail to build and the app would not
        /// start. So a failure here degrades to the null implementations instead: the app
        /// runs on keyword rules exactly as it did before this model existed.
        /// </remarks>
        private static void AddIntelligence(IServiceCollection services)
        {
            services.AddSingleton<ISemanticClassifier>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<IntelligenceOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<EmbeddingSemanticClassifier>>();

                if (!options.EnableSemanticClassifier)
                {
                    logger.LogInformation("Semantic classifier disabled by configuration");
                    return new NullSemanticClassifier();
                }

                try
                {
                    var embedder = GetEmbedder(sp);
                    return new EmbeddingSemanticClassifier(
                        embedder, logger, options.MinimumSimilarity, options.MinimumMargin);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Embedding model could not be loaded; using keyword rules only");
                    return new NullSemanticClassifier();
                }
            });

            // ---- Local language model ----
            // A long timeout, not the default 100 seconds: this client pulls a 2.7GB file,
            // and the per-read timeout is what matters on a slow connection.
            services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(30) });

            services.AddSingleton<IModelProvisioner>(sp => new HuggingFaceModelProvisioner(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<ILogger<HuggingFaceModelProvisioner>>(),
                Path.Combine(AppContext.BaseDirectory, "Assets", "phi35_manifest.json"),
                Path.Combine(AppPaths.ModelDirectory, "phi-3.5-mini-int4")));

            services.AddSingleton<ILocalLanguageModel>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<IntelligenceOptions>>().Value;
                if (!options.EnableSemanticClassifier)
                    return new NullLocalLanguageModel();

                return new PhiLocalLanguageModel(
                    sp.GetRequiredService<IModelProvisioner>(),
                    sp.GetRequiredService<ILogger<PhiLocalLanguageModel>>(),
                    Path.Combine(AppPaths.ModelDirectory, "phi-3.5-mini-int4"),
                    options.SlmIdleUnloadTimeout);
            });

            services.AddSingleton<IGoalRelevanceScorer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<IntelligenceOptions>>().Value;
                if (!options.EnableSemanticClassifier)
                    return new NullGoalRelevanceScorer();

                try
                {
                    return new EmbeddingGoalRelevanceScorer(
                        GetEmbedder(sp), sp.GetRequiredService<ILogger<EmbeddingGoalRelevanceScorer>>());
                }
                catch (Exception ex)
                {
                    sp.GetRequiredService<ILogger<EmbeddingGoalRelevanceScorer>>()
                        .LogError(ex, "Goal relevance scoring unavailable");
                    return new NullGoalRelevanceScorer();
                }
            });
        }

        /// <summary>
        /// The single embedding model instance, shared by the classifier and the goal
        /// scorer. Two ONNX sessions over the same 23MB file would double the memory and
        /// buy nothing - the generator already serialises its own calls.
        /// </summary>
        private static MiniLmEmbeddingGenerator GetEmbedder(IServiceProvider sp) =>
            sp.GetRequiredService<MiniLmEmbeddingGenerator>();
    }
}
