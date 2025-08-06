using FocusAssistant.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FocusAssistant.Views
{
    /// <summary>
    /// Interaction logic for AnalyticsView.xaml
    /// </summary>
    public partial class AnalyticsView : UserControl
    {
        private LoggingService _loggingService;
        private DataProcessor _dataProcessor;
        private ExportService _exportService;
        private ObservableCollection<WeeklyTrendItem> _weeklyTrends;
        private ObservableCollection<AppAnalysisItem> _productiveApps;
        private ObservableCollection<AppAnalysisItem> _distractingApps;

        public AnalyticsView()
        {
            InitializeComponent();
            InitializeServices();
            LoadAnalyticsData();
        }

        private void InitializeServices()
        {
            _loggingService = new LoggingService();
            _dataProcessor = new DataProcessor(_loggingService);
            _exportService = new ExportService(_loggingService);

            _weeklyTrends = new ObservableCollection<WeeklyTrendItem>();
            _productiveApps = new ObservableCollection<AppAnalysisItem>();
            _distractingApps = new ObservableCollection<AppAnalysisItem>();

            WeeklyTrendGrid.ItemsSource = _weeklyTrends;
            ProductiveAppsList.ItemsSource = _productiveApps;
            DistractingAppsList.ItemsSource = _distractingApps;
        }

        private void LoadAnalyticsData()
        {
            try
            {
                LoadTodaysData();
                LoadWeeklyTrends();
                LoadAppAnalysis();
                LoadMLReadiness();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading analytics data: {ex.Message}",
                              "Analytics Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
            }
        }

        private void LoadTodaysData()
        {
            var todayReport = _dataProcessor.GenerateDailyReport(DateTime.Today);

            TodayWorkTimeText.Text = $"{(int)todayReport.TotalWorkTimeHours}h {(int)((todayReport.TotalWorkTimeHours % 1) * 60)}m";
            TodayProductivityText.Text = $"{todayReport.ProductivityScore:F0}%";
            TodaySessionsText.Text = todayReport.NumberOfSessions.ToString();
            TodayAppSwitchesText.Text = todayReport.TotalAppSwitches.ToString();
        }

        private void LoadWeeklyTrends()
        {
            _weeklyTrends.Clear();

            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var report = _dataProcessor.GenerateDailyReport(date);

                var trendItem = new WeeklyTrendItem
                {
                    Date = date,
                    WorkTimeFormatted = $"{(int)report.TotalWorkTimeHours}h {(int)((report.TotalWorkTimeHours % 1) * 60)}m",
                    ProductivityFormatted = $"{report.ProductivityScore:F0}%",
                    NumberOfSessions = report.NumberOfSessions,
                    TopApp = report.TopProductiveApps.FirstOrDefault() ?? "None",
                    MostProductiveHour = report.MostProductiveHour ?? "N/A"
                };

                _weeklyTrends.Add(trendItem);
            }
        }

        private void LoadAppAnalysis()
        {
            _productiveApps.Clear();
            _distractingApps.Clear();

            var sessions = _loggingService.LoadWorkSessions(7);
            var allUsages = sessions.SelectMany(s => s.AppUsages).ToList();

            if (!allUsages.Any()) return;

            // Productive apps
            var productiveAppData = allUsages
                .Where(u => u.IsProductive)
                .GroupBy(u => u.AppName)
                .Select(g => new {
                    AppName = g.Key,
                    TotalTime = g.Sum(u => u.Duration.TotalMinutes),
                    Category = CategorizeApp(g.Key)
                })
                .OrderByDescending(x => x.TotalTime)
                .Take(5)
                .ToList();

            var totalProductiveTime = productiveAppData.Sum(x => x.TotalTime);

            foreach (var app in productiveAppData)
            {
                _productiveApps.Add(new AppAnalysisItem
                {
                    AppName = app.AppName,
                    Category = app.Category,
                    TimeSpent = FormatTime(app.TotalTime),
                    Percentage = totalProductiveTime > 0 ? $"{(app.TotalTime / totalProductiveTime * 100):F0}%" : "0%"
                });
            }

            // Distracting apps
            var distractingAppData = allUsages
                .Where(u => !u.IsProductive)
                .GroupBy(u => u.AppName)
                .Select(g => new {
                    AppName = g.Key,
                    TotalTime = g.Sum(u => u.Duration.TotalMinutes),
                    Category = CategorizeApp(g.Key)
                })
                .OrderByDescending(x => x.TotalTime)
                .Take(5)
                .ToList();

            var totalDistractingTime = distractingAppData.Sum(x => x.TotalTime);

            foreach (var app in distractingAppData)
            {
                _distractingApps.Add(new AppAnalysisItem
                {
                    AppName = app.AppName,
                    Category = app.Category,
                    TimeSpent = FormatTime(app.TotalTime),
                    Percentage = totalDistractingTime > 0 ? $"{(app.TotalTime / totalDistractingTime * 100):F0}%" : "0%"
                });
            }
        }

        private void LoadMLReadiness()
        {
            var sessions = _loggingService.LoadWorkSessions(30);
            var mlData = _dataProcessor.PrepareMLData(sessions);

            var uniqueApps = sessions.SelectMany(s => s.AppUsages).Select(u => u.AppName).Distinct().Count();
            var daysTracked = sessions.Select(s => s.StartTime.Date).Distinct().Count();

            DataPointsText.Text = mlData.Count.ToString();
            DaysTrackedText.Text = daysTracked.ToString();
            UniqueAppsText.Text = uniqueApps.ToString();

            // Calculate readiness score
            var readinessScore = CalculateMLReadiness(mlData.Count, daysTracked, uniqueApps);
            DataReadinessProgress.Value = readinessScore;

            if (readinessScore >= 80)
            {
                ReadinessStatusText.Text = "Ready";
                ReadinessStatusText.Foreground = System.Windows.Media.Brushes.Green;
                ReadinessMessageText.Text = "✅ Sufficient data collected for ML training. Model training can begin.";
            }
            else if (readinessScore >= 50)
            {
                ReadinessStatusText.Text = "Almost Ready";
                ReadinessStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                ReadinessMessageText.Text = $"⚠️ Good progress! Need {Math.Max(0, 100 - mlData.Count)} more data points or {Math.Max(0, 7 - daysTracked)} more days.";
            }
            else
            {
                ReadinessStatusText.Text = "Not Ready";
                ReadinessStatusText.Foreground = System.Windows.Media.Brushes.Red;
                ReadinessMessageText.Text = $"❌ More data needed. Collect {Math.Max(0, 100 - mlData.Count)} more data points over {Math.Max(0, 7 - daysTracked)} more days.";
            }
        }

        private double CalculateMLReadiness(int dataPoints, int daysTracked, int uniqueApps)
        {
            double dataPointsScore = Math.Min(100, (dataPoints / 100.0) * 40); // 40% weight
            double daysScore = Math.Min(100, (daysTracked / 7.0) * 40); // 40% weight
            double appsScore = Math.Min(100, (uniqueApps / 10.0) * 20); // 20% weight

            return dataPointsScore + daysScore + appsScore;
        }

        private void ExportCSV(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv"
                };

                var exportType = (ExportType)ExportTypeCombo.SelectedIndex;
                saveDialog.FileName = $"focus_assistant_{exportType}_{DateTime.Now:yyyy-MM-dd}.csv";

                if (saveDialog.ShowDialog() == true)
                {
                    _exportService.ExportToCSV(saveDialog.FileName, exportType, 30);
                    MessageBox.Show($"Data exported successfully to:\n{saveDialog.FileName}",
                                  "Export Complete",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}",
                              "Export Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void ExportJSON(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "json"
                };

                if (ExportTypeCombo.SelectedIndex <= 1)
                {
                    saveDialog.FileName = $"focus_assistant_ml_data_{DateTime.Now:yyyy-MM-dd}.json";

                    if (saveDialog.ShowDialog() == true)
                    {
                        _exportService.ExportMLTrainingData(saveDialog.FileName, 30);
                        MessageBox.Show($"ML training data exported to:\n{saveDialog.FileName}",
                                      "Export Complete",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                }
                else
                {
                    saveDialog.FileName = $"focus_assistant_daily_reports_{DateTime.Now:yyyy-MM-dd}.json";

                    if (saveDialog.ShowDialog() == true)
                    {
                        _exportService.ExportDailyReports(saveDialog.FileName, 30);
                        MessageBox.Show($"Daily reports exported to:\n{saveDialog.FileName}",
                                      "Export Complete",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}",
                              "Export Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private string CategorizeApp(string appName)
        {
            // Same categorization logic as in DataProcessor
            var categories = new Dictionary<string, string[]>
            {
                ["Development"] = new[]{ "devenv", "code", "pycharm", "intellij", "eclipse" },
                ["Communication"] = new[] { "outlook", "teams", "slack", "discord", "zoom" },
                ["Web Browser"] = new[] { "chrome", "firefox", "edge", "safari", "opera" },
                ["Entertainment"] = new[] { "spotify", "vlc", "netflix", "youtube", "steam" },
                ["Office"] = new[]{ "word", "excel", "powerpoint", "onenote", "notion" },
                ["Design"] = new[] { "photoshop", "illustrator", "figma", "sketch" }
            };

            foreach (var category in categories)
            {
                if (category.Value.Any(app => string.Equals(app, appName, StringComparison.OrdinalIgnoreCase)))
                    return category.Key;
            }
            return "Other";
        }

        private string FormatTime(double minutes)
        {
            if (minutes < 60)
                return $"{minutes:F0}m";
            else
                return $"{minutes / 60:F1}h";
        }
    }

    // Helper classes for data binding
    public class WeeklyTrendItem
    {
        public DateTime Date { get; set; }
        public string WorkTimeFormatted { get; set; }
        public string ProductivityFormatted { get; set; }
        public int NumberOfSessions { get; set; }
        public string TopApp { get; set; }
        public string MostProductiveHour { get; set; }
    }

    public class AppAnalysisItem
    {
        public string AppName { get; set; }
        public string Category { get; set; }
        public string TimeSpent { get; set; }
        public string Percentage { get; set; }
    }
}
