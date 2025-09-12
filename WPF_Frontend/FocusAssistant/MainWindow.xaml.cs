using FocusAssistant.Data;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Flask;
using FocusAssistant.Services.Session;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

namespace FocusAssistant
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly WindowTracker _windowTracker;
        private readonly ISessionManager _sessionManager;   
        private readonly FlaskIntegrationFacade _facade;    
        private TrackingView _trackingViewInstance;
        private RecommendationsView _recommendationsViewInstance;

        public MainWindow(IServiceProvider serviceProvider, WindowTracker windowTracker, ISessionManager sessionManager, FlaskIntegrationFacade facade)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _facade = facade ?? throw new ArgumentNullException(nameof(facade));

            try
            {
                InitializeDatabase(); // Add this to create tables
                _trackingViewInstance = _serviceProvider.GetRequiredService<TrackingView>();
                MainContentFrame.Content = _trackingViewInstance;


                Console.WriteLine($"MainWindow initialized with TrackingView at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MainWindow initialization failed: {ex.Message}. Inner: {ex.InnerException?.Message ?? "None"}");
                throw;
            }
        }

        private void InitializeDatabase()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<FocusAssistantDbContext>();
                context.Database.EnsureCreated(); // Creates tables if they don’t exist
                Console.WriteLine($"✅ Database initialized successfully at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to initialize database: {ex.Message}. Inner: {ex.InnerException?.Message ?? "None"}");
                throw;
            }
        }

        // Rest of the code remains the same
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"MainWindow Loaded at {DateTime.Now:HH:mm:ss.fff}");
                if (!_windowTracker.IsTracking)
                {
                    await _windowTracker.StartTrackingAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MainWindow Loaded failed: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"MainWindow load failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                if (_windowTracker.IsTracking)
                {
                    await _windowTracker.StopTrackingAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during window close: {ex.Message}");
            }
        }

        #region Navigation Methods
        private void ShowDashboard(object sender, RoutedEventArgs e)
        {
            try
            {
                var dashboardView = _serviceProvider.GetRequiredService<DashboardView>();
                MainContentFrame.Content = dashboardView;
            }
            catch (Exception ex)
            {
                ShowError("Dashboard", ex);
            }
        }

        private void ShowTracking(object sender, RoutedEventArgs e)
        {
            try
            {
                MainContentFrame.Content = _trackingViewInstance;
            }
            catch (Exception ex)
            {
                ShowError("Tracking", ex);
            }
        }

        private void ShowAnalytics(object sender, RoutedEventArgs e)
        {
            try
            {
                var analyticsView = _serviceProvider.GetRequiredService<AnalyticsView>();
                MainContentFrame.Content = analyticsView;
                Console.WriteLine("AnalyticsUserControl loaded successfully.");
            }
            catch (Exception ex)
            {
                ShowError("Analytics", ex);
            }
        }

        private void ShowGamification(object sender, RoutedEventArgs e)
        {
            try
            {
                MainContentFrame.Content = new System.Windows.Controls.TextBlock
                {
                    Text = "Achievements View\nComing soon...",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = System.Windows.TextAlignment.Center
                };
            }
            catch (Exception ex)
            {
                ShowError("Gamification", ex);
            }
        }

        private void ShowRecommendations(object sender, RoutedEventArgs e)
        {
            try
            {
                // Option 1: Cached instance (efficient, reuses)
                if (_recommendationsViewInstance == null)
                {
                    _recommendationsViewInstance = _serviceProvider.GetRequiredService<RecommendationsView>();
                }
                MainContentFrame.Content = _recommendationsViewInstance;

                // Option 2: Fresh instance each time (uncomment if preferred)
                // MainContentFrame.Content = _serviceProvider.GetRequiredService<RecommendationsView>();

                Console.WriteLine($"RecommendationsView displayed at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                ShowError("Recommendations", ex);
            }
        }

        private void ShowSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                MainContentFrame.Content = new System.Windows.Controls.TextBlock
                {
                    Text = "Settings View\nComing soon...",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = System.Windows.TextAlignment.Center
                };
            }
            catch (Exception ex)
            {
                ShowError("Settings", ex);
            }
        }

        private void ShowError(string viewName, Exception ex)
        {
            Console.WriteLine($"Show{viewName} failed: {ex.Message}\n{ex.StackTrace}");
            MessageBox.Show($"Failed to load {viewName}: {ex.Message}", $"{viewName} Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        #endregion
    }
}
