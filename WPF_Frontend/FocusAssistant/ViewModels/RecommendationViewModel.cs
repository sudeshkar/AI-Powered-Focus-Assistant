using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FocusAssistant.ViewModels
{
    /// <summary>
    /// Backs the Recommendations view: what the agent has learned, and the most
    /// recent suggestion it made.
    /// </summary>
    public class RecommendationViewModel : ObservableObject, IDisposable
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

        private readonly ISessionManager _sessionManager;
        private readonly FlaskIntegrationFacade _facade;
        private readonly DispatcherTimer _refreshTimer;

        private string _recentInterventionMessage = "No recent suggestion";
        private string _recentInterventionDetails = "N/A";
        private string _productivityTrend = "Trend: N/A";
        private string _energyPatterns = "N/A";
        private string _optimalTimes = "N/A";
        private double _qValueStd;
        private bool _disposed;

        public ObservableCollection<string> AISuggestions { get; } = new();
        public ObservableCollection<string> MostActiveHours { get; } = new();
        public ObservableCollection<KeyValuePair<string, ActionMetrics>> ActionEffectiveness { get; } = new();
        public ObservableCollection<double> QValueStats { get; } = new();

        public string RecentInterventionMessage { get => _recentInterventionMessage; set => SetProperty(ref _recentInterventionMessage, value); }
        public string RecentInterventionDetails { get => _recentInterventionDetails; set => SetProperty(ref _recentInterventionDetails, value); }
        public string ProductivityTrend { get => _productivityTrend; set => SetProperty(ref _productivityTrend, value); }
        public string EnergyPatterns { get => _energyPatterns; set => SetProperty(ref _energyPatterns, value); }
        public string OptimalTimes { get => _optimalTimes; set => SetProperty(ref _optimalTimes, value); }
        public double QValueStd { get => _qValueStd; set => SetProperty(ref _qValueStd, value); }

        public ICommand RefreshCommand { get; }
        public ICommand TakeActionCommand { get; }

        public RecommendationViewModel(ISessionManager sessionManager, FlaskIntegrationFacade facade)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _facade = facade ?? throw new ArgumentNullException(nameof(facade));

            RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
            TakeActionCommand = new AsyncRelayCommand(OnTakeActionAsync);

            _sessionManager.AiInterventionReceived += OnAiInterventionReceived;

            _refreshTimer = new DispatcherTimer { Interval = RefreshInterval };
            _refreshTimer.Tick += (_, _) => _ = LoadDataAsync();
            _refreshTimer.Start();

            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            SuggestionsResponse? suggestions;
            InsightsResponse? insights;

            try
            {
                var suggestionsTask = _facade.GetSuggestionsAsync();
                var insightsTask = _facade.GetInsightsAsync();
                await Task.WhenAll(suggestionsTask, insightsTask);

                suggestions = await suggestionsTask;
                insights = await insightsTask;
            }
            catch (Exception ex)
            {
                await OnUiThread(() =>
                {
                    AISuggestions.Clear();
                    AISuggestions.Add($"Could not reach the AI backend: {ex.Message}");
                });
                return;
            }

            await OnUiThread(() =>
            {
                ApplySuggestions(suggestions);
                ApplyInsights(insights);
            });
        }

        private void ApplySuggestions(SuggestionsResponse? response)
        {
            AISuggestions.Clear();
            MostActiveHours.Clear();

            var patterns = response?.Suggestions;
            if (patterns is null)
            {
                AISuggestions.Add("No suggestions available yet");
                ProductivityTrend = "Trend: N/A";
                MostActiveHours.Add("No activity data yet");
                return;
            }

            AISuggestions.Add($"Daily patterns recorded: {patterns.DailyPatternsCount}");
            AISuggestions.Add($"Weekly patterns recorded: {patterns.WeeklyPatternsCount}");
            ProductivityTrend = $"Trend: {patterns.ProductivityTrends?.Trend ?? "N/A"}";

            var hours = patterns.MostActiveHours?.Where(h => h.Count >= 2).ToList();
            if (hours is { Count: > 0 })
            {
                foreach (var hour in hours)
                    MostActiveHours.Add($"{hour[0]}: {hour[1]} activities");
            }
            else
            {
                MostActiveHours.Add("No activity data yet");
            }
        }

        private void ApplyInsights(InsightsResponse? response)
        {
            ActionEffectiveness.Clear();
            QValueStats.Clear();

            var insights = response?.Insights;
            if (insights is null)
            {
                EnergyPatterns = "N/A";
                OptimalTimes = "N/A";
                QValueStats.Add(0);
                QValueStats.Add(0);
                QValueStats.Add(0);
                QValueStd = 0;
                return;
            }

            if (insights.ActionEffectiveness is { Count: > 0 })
            {
                // Most-used first, so the list leads with what the agent actually relies on.
                foreach (var entry in insights.ActionEffectiveness.OrderByDescending(kv => kv.Value.TotalUses))
                    ActionEffectiveness.Add(entry);
            }

            var circadian = insights.CircadianInsights;
            EnergyPatterns = circadian?.EnergyPatternsPhases is { Count: > 0 } phases
                ? $"Phases: {string.Join(", ", phases)}"
                : "N/A";

            OptimalTimes = FormatOptimalTimes(circadian?.OptimalTimes);

            var q = insights.LearningMetrics?.QValueStatistics;
            QValueStats.Add(q?.Min ?? 0);
            QValueStats.Add(q?.Mean ?? 0);
            QValueStats.Add(q?.Max ?? 0);
            QValueStd = q?.Std ?? 0;

            var preferences = insights.PersonalizationProfile?.Preferences;
            if (preferences is not null)
            {
                AISuggestions.Add($"Communication style: {preferences.CommunicationStyle ?? "N/A"}");
                AISuggestions.Add($"Gamification: {(preferences.GamificationPreference ? "on" : "off")}");
            }
        }

        /// <summary>Shows the best window overall rather than only reporting "night".</summary>
        private static string FormatOptimalTimes(Dictionary<string, double>? optimalTimes)
        {
            if (optimalTimes is not { Count: > 0 })
                return "N/A";

            var best = optimalTimes.OrderByDescending(kv => kv.Value).First();
            return $"Best focus window: {best.Key} ({best.Value:F2})";
        }

        private void OnAiInterventionReceived(object? sender, ActivityResponse e)
        {
            _ = OnUiThread(() =>
            {
                RecentInterventionMessage = e.InterventionMessage ?? "No recent suggestion";
                RecentInterventionDetails =
                    $"Action: {e.ActionTaken ?? "N/A"} | Risk: {e.DistractionRisk:P0} | {DateTime.Now:HH:mm:ss}";
            });
        }

        private Task OnTakeActionAsync()
        {
            // Feedback for a specific suggestion is sent by AiInterventionWindow,
            // which holds the intervention id this view model does not have.
            RecentInterventionDetails += " | Acknowledged";
            OnPropertyChanged(nameof(RecentInterventionDetails));
            return Task.CompletedTask;
        }

        private static Task OnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
                return Task.CompletedTask;

            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _refreshTimer.Stop();
            _sessionManager.AiInterventionReceived -= OnAiInterventionReceived;
        }
    }
}
