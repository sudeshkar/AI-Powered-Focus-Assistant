using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Session;
using FocusAssistant.Services.Session.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;  // For ObservableCollection
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

// Simple RelayCommand (if not using a MVVM toolkit)
public class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool> _canExecute;
    public RelayCommand(Func<Task> execute, Func<bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (() => true);
    }
    public event EventHandler CanExecuteChanged;
    public bool CanExecute(object parameter) => _canExecute();
    public async void Execute(object parameter) => await _execute();
}

namespace FocusAssistant.ViewModels
{
    public class RecommendationViewModel : INotifyPropertyChanged
    {
        private readonly ISessionManager _sessionManager;
        private readonly FlaskIntegrationFacade _facade;
        private string _recentInterventionMessage;
        private string _recentInterventionDetails;
        private string _productivityTrend;
        private string _energyPatterns;
        private string _optimalTimes;
        private double _qValueStd;  // Added for Std

        // Lists for binding - FIXED: Use ActionMetrics directly
        public ObservableCollection<string> AISuggestions { get; } = new();
        public ObservableCollection<string> MostActiveHours { get; } = new();
        public ObservableCollection<KeyValuePair<string, ActionMetrics>> ActionEffectiveness { get; } = new();  // FIXED: Matches Dictionary type
        public ObservableCollection<double> QValueStats { get; } = new();  // Min, Mean, Max

        public string RecentInterventionMessage
        {
            get => _recentInterventionMessage;
            set => SetProperty(ref _recentInterventionMessage, value);
        }

        public string RecentInterventionDetails
        {
            get => _recentInterventionDetails;
            set => SetProperty(ref _recentInterventionDetails, value);
        }

        public string ProductivityTrend
        {
            get => _productivityTrend;
            set => SetProperty(ref _productivityTrend, value);
        }

        public string EnergyPatterns
        {
            get => _energyPatterns;
            set => SetProperty(ref _energyPatterns, value);
        }

        public string OptimalTimes
        {
            get => _optimalTimes;
            set => SetProperty(ref _optimalTimes, value);
        }

        public double QValueStd  // Added binding for Std
        {
            get => _qValueStd;
            set => SetProperty(ref _qValueStd, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand TakeActionCommand { get; }

        public RecommendationViewModel(ISessionManager sessionManager, FlaskIntegrationFacade facade)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _facade = facade ?? throw new ArgumentNullException(nameof(facade));
            _sessionManager.AiInterventionReceived += OnAiInterventionReceived;

            RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
            TakeActionCommand = new RelayCommand(async () => await OnTakeActionAsync());

            RecentInterventionMessage = "No recent intervention";
            RecentInterventionDetails = "N/A";
            LoadDataAsync(); // Initial load

            // Auto-refresh every 5 minutes
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            timer.Tick += (s, e) => _ = LoadDataAsync();  // Fire-and-forget
            timer.Start();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Parallel calls for efficiency
                var suggestionsTask = _facade.GetSuggestionsAsync();
                var insightsTask = _facade.GetInsightsAsync();

                await Task.WhenAll(suggestionsTask, insightsTask);

                var suggestions = await suggestionsTask;
                var insights = await insightsTask;

                // Update on UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // AI Suggestions
                    AISuggestions.Clear();
                    if (suggestions?.Suggestions != null)
                    {
                        AISuggestions.Add($"Daily patterns: {suggestions.Suggestions.DailyPatternsCount}");
                        AISuggestions.Add($"Weekly patterns: {suggestions.Suggestions.WeeklyPatternsCount}");
                        ProductivityTrend = $"Trend: {suggestions.Suggestions.ProductivityTrends?.Trend ?? "N/A"}";
                    }
                    else
                    {
                        AISuggestions.Add("No suggestions available");
                    }

                    // Most Active Hours
                    MostActiveHours.Clear();
                    if (suggestions?.Suggestions?.MostActiveHours != null && suggestions.Suggestions.MostActiveHours.Any())
                    {
                        foreach (var h in suggestions.Suggestions.MostActiveHours.Where(l => l.Count >= 2))
                            MostActiveHours.Add($"{h[0]}: {h[1]} activities");
                    }
                    else
                    {
                        MostActiveHours.Add("No active hours data yet");
                    }

                    // Action Effectiveness - FIXED: Direct add, no conversion
                    ActionEffectiveness.Clear();
                    if (insights?.Insights?.ActionEffectiveness != null && insights.Insights.ActionEffectiveness.Any())
                    {
                        foreach (var kv in insights.Insights.ActionEffectiveness)
                        {
                            ActionEffectiveness.Add(kv);  // Now: KeyValuePair<string, ActionMetrics>
                        }
                    }
                    else
                    {
                        ActionEffectiveness.Add(new KeyValuePair<string, ActionMetrics>("No Data", null));
                    }

                    // Circadian Insights
                    if (insights?.Insights?.CircadianInsights != null)
                    {
                        EnergyPatterns = $"Phases: {string.Join(", ", insights.Insights.CircadianInsights.EnergyPatternsPhases ?? new List<string>())}";
                        if (insights.Insights.CircadianInsights.OptimalTimes?.TryGetValue("night", out double score) == true)
                            OptimalTimes = $"Night score: {score:F2}";
                        else
                            OptimalTimes = "N/A";
                    }
                    else
                    {
                        EnergyPatterns = "N/A";
                        OptimalTimes = "N/A";
                    }

                    // Q-Values - Added Std
                    QValueStats.Clear();
                    if (insights?.Insights?.LearningMetrics?.QValueStatistics != null)
                    {
                        var q = insights.Insights.LearningMetrics.QValueStatistics;
                        QValueStats.Add(q.Min);
                        QValueStats.Add(q.Mean);
                        QValueStats.Add(q.Max);
                        QValueStd = q.Std;
                    }
                    else
                    {
                        QValueStats.Add(0); QValueStats.Add(0); QValueStats.Add(0);
                        QValueStd = 0;
                    }

                    // Example: Add more from other sections (e.g., Personalization)
                    if (insights?.Insights?.PersonalizationProfile != null)
                    {
                        var profile = insights.Insights.PersonalizationProfile;
                        AISuggestions.Add($"Communication Style: {profile.Preferences?.CommunicationStyle ?? "N/A"}");
                        AISuggestions.Add($"Gamification: {(profile.Preferences?.GamificationPreference == true ? "Yes" : "No")}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AISuggestions.Clear();
                    AISuggestions.Add($"Error loading data: {ex.Message}");
                });
            }
        }

        private void OnAiInterventionReceived(object? sender, ActivityResponse e)
        {
            RecentInterventionMessage = e.InterventionMessage ?? "No recent intervention";
            RecentInterventionDetails = $"Action: {e.ActionTaken ?? "N/A"} | Risk: {e.DistractionRisk:F1} | Time: {DateTime.Now:HH:mm:ss}";
        }

        private async Task OnTakeActionAsync()
        {
            // TODO: Send feedback to train RL
            // e.g., await _facade.SendFeedbackAsync(new FeedbackRequest { InterventionId = "...", Helpful = true, ProductivityChange = 0.2f });
            RecentInterventionDetails += " | Action taken! (Feedback sent)";
            OnPropertyChanged(nameof(RecentInterventionDetails));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}