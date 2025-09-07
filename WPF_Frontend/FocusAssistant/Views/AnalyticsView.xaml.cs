using FocusAssistant.Enums;
using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Export_Services.Interfaces;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    public partial class AnalyticsView : UserControl
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IExportFactory _exportFactory;

        public AnalyticsView(IAnalyticsService analyticsService,
                             IExportFactory exportFactory)
        {
            InitializeComponent();
            _analyticsService = analyticsService;
            _exportFactory = exportFactory;
            Loaded += async (s, e) => await LoadAnalyticsDataAsync();
        }

        private async Task LoadAnalyticsDataAsync()
        {
            try
            {
                var analytics = await _analyticsService.GetAnalyticsAsync();
                var insights = await _analyticsService.GetInsightsAsync();

                Dispatcher.Invoke(() =>
                {
                    // Check if analytics or insights are null
                    if (analytics == null || insights == null || insights.Insights == null)
                    {
                        SetDisconnectedState();
                        return;
                    }

                    // Top row
                    TodayProductivityText.Text = $"{analytics.ProductivityRate:F0}%";
                    TodayActivitiesText.Text = analytics.TotalActivities.ToString();
                    TodayInterventionsText.Text = analytics.RecentInterventions.ToString();

                    // Readiness
                    int pts = insights.Insights.CircadianInsights?.TotalDataPoints ?? 0;
                    int dpc = insights.Insights.BehavioralPatterns?.DailyPatternsCount ?? 0;
                    int sus = insights.Insights.StateSpaceCoverage?.TotalUniqueStates ?? 0;
                    double score = CalculateReadiness(pts, dpc, sus);

                    DataReadinessProgress.Value = score;
                    ReadinessStatusText.Text = score >= 80 ? "Ready" : score >= 50 ? "Almost Ready" : "Not Ready";
                    ReadinessMessageText.Text = score >= 80 ? "✅ Sufficient data for ML training."
                                                            : score >= 50 ? "⚠️ Good progress! Collect more data."
                                                                          : "❌ More data needed for ML training.";

                    DataPointsText.Text = pts.ToString();
                    StatesExploredText.Text = sus.ToString();
                    FeedbackText.Text = insights.Insights.LearningMetrics?.TotalFeedbackReceived.ToString() ?? "0";

                    // Lists
                    InterventionList.ItemsSource = insights.Insights.ActionEffectiveness != null
                        ? insights.Insights.ActionEffectiveness
                            .Select(kv => $"{kv.Key.Replace("_", " ")}  –  {kv.Value.AverageReward:F2}")
                            .ToList()
                        : new List<string> { "No data" };

                    AppUsageList.ItemsSource = analytics.TopApps != null
                        ? analytics.TopApps
                            .Select(kv => $"{kv.Key}  –  {kv.Value} min")
                            .ToList()
                        : new List<string> { "No data" };
                });
            }
            catch
            {
                Dispatcher.Invoke(SetDisconnectedState);
            }
        }

        private void SetDisconnectedState()
        {
            TodayProductivityText.Text = "ML disconnected";
            TodayActivitiesText.Text = "-";
            TodayInterventionsText.Text = "-";
            DataReadinessProgress.Value = 0;
            ReadinessStatusText.Text = "Disconnected";
            ReadinessMessageText.Text = "⚠️ ML service unavailable";
            InterventionList.ItemsSource = new List<string> { "ML disconnected" };
            AppUsageList.ItemsSource = new List<string> { "ML disconnected" };
        }

        private double CalculateReadiness(int pts, int dpc, int sus)
        {
            double a = Math.Min(100, (pts / 1000.0) * 40);
            double b = Math.Min(100, (dpc / 7.0) * 30);
            double c = Math.Min(100, (sus / 200.0) * 30);
            return a + b + c;
        }

        public async void ExportCSV(object sender, RoutedEventArgs e)
        {
            await Export(ExportFormat.Csv);
        }

        public async void ExportJSON(object sender, RoutedEventArgs e)
        {
            await Export(ExportFormat.Json);
        }

        private async Task Export(ExportFormat format)
        {
            var dlg = new SaveFileDialog
            {
                Filter = format == ExportFormat.Csv ? "CSV|*.csv" : "JSON|*.json",
                FileName = $"focus_assistant_{ExportTypeCombo.SelectedItem}_{DateTime.Now:yyyy-MM-dd}.{format.ToString().ToLower()}"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var exporter = _exportFactory.CreateExporter((ExportType)ExportTypeCombo.SelectedIndex, format);
                    await exporter.ExportAsync(dlg.FileName);
                    MessageBox.Show("Export finished", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}