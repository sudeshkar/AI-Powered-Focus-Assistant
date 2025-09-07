using FocusAssistant.Models.Response_Models;
using FocusAssistant.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    public partial class RecommendationView : UserControl
    {
        private readonly RecommendationViewModel _viewModel;

        public RecommendationView()
        {
            InitializeComponent();
            _viewModel = (RecommendationViewModel)DataContext;
            Loaded += async (s, e) => await LoadRecommendationDataAsync();
        }

        private async Task LoadRecommendationDataAsync()
        {
            try
            {
                // hard-coded JSON (replace with real calls when time)
                var insights = JsonConvert.DeserializeObject<InsightsResponse>(GetInsightsJson());
                var suggestions = JsonConvert.DeserializeObject<SuggestionsResponse>(GetSuggestionsJson());
                var action = JsonConvert.DeserializeObject<ActionResponse>(GetActionJson());

                Dispatcher.Invoke(() =>
                {
                    // AI suggestions
                    _viewModel.AISuggestions.Clear();
                    if (suggestions?.Suggestions != null)
                    {
                        _viewModel.AISuggestions.Add($"Daily patterns: {suggestions.Suggestions.DailyPatternsCount}");
                        _viewModel.AISuggestions.Add($"Weekly patterns: {suggestions.Suggestions.WeeklyPatternsCount}");
                        _viewModel.AISuggestions.Add($"Productivity trend: {suggestions.Suggestions.ProductivityTrends?.Trend ?? "N/A"}");
                    }
                    else
                    {
                        _viewModel.AISuggestions.Add("No suggestions available");
                    }

                    // Most active hours
                    _viewModel.MostActiveHours.Clear();
                    if (suggestions?.Suggestions?.MostActiveHours != null)
                    {
                        foreach (var h in suggestions.Suggestions.MostActiveHours.Where(l => l.Count >= 2))
                            _viewModel.MostActiveHours.Add($"{h[0]}: {h[1]} activities");
                    }

                    _viewModel.ProductivityTrend = $"Trend: {suggestions?.Suggestions?.ProductivityTrends?.Trend ?? "N/A"}";

                    // Action effectiveness
                    _viewModel.ActionEffectiveness.Clear();
                    if (insights?.Insights?.ActionEffectiveness != null)
                    {
                        foreach (var kv in insights.Insights.ActionEffectiveness)
                            _viewModel.ActionEffectiveness.Add(kv);
                    }

                    // Circadian
                    if (insights?.Insights?.CircadianInsights != null)
                    {
                        _viewModel.EnergyPatterns = $"Phases: {string.Join(", ", insights.Insights.CircadianInsights.EnergyPatternsPhases ?? new List<string>())}";
                        if (insights.Insights.CircadianInsights.OptimalTimes?.TryGetValue("night", out double score) == true)
                            _viewModel.OptimalTimes = $"Night score: {score:F2}";
                        else
                            _viewModel.OptimalTimes = "N/A";
                    }
                    else
                    {
                        _viewModel.EnergyPatterns = "N/A";
                        _viewModel.OptimalTimes = "N/A";
                    }

                    // Intervention
                    if (action != null)
                    {
                        _viewModel.RecentInterventionMessage = action.InterventionMessage ?? "No recent intervention";
                        _viewModel.RecentInterventionDetails = $"Action: {action.ActionTaken ?? "N/A"} | Risk: {action.DistractionRisk:F1} | Time: {action.Timestamp ?? "N/A"}";
                    }
                    else
                    {
                        _viewModel.RecentInterventionMessage = "No recent intervention";
                        _viewModel.RecentInterventionDetails = "N/A";
                    }

                    // Q-values
                    if (insights?.Insights?.LearningMetrics?.QValueStatistics != null)
                    {
                        var q = insights.Insights.LearningMetrics.QValueStatistics;
                        _viewModel.QValueStats.Clear();
                        _viewModel.QValueStats.Add(q.Min);
                        _viewModel.QValueStats.Add(q.Mean);
                        _viewModel.QValueStats.Add(q.Max);
                    }
                    else
                    {
                        _viewModel.QValueStats.Clear();
                        _viewModel.QValueStats.Add(0); _viewModel.QValueStats.Add(0); _viewModel.QValueStats.Add(0);
                    }
                });
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    _viewModel.AISuggestions.Clear(); _viewModel.AISuggestions.Add("Error loading data.");
                });
            }
        }

        // ---- hard-coded JSON stubs ----
        private string GetInsightsJson() => @"{""insights"":{""action_effectiveness"":{""adaptive_blocking"":{""average_reward"":3.0,""success_rate"":1.0,""total_uses"":3}},""circadian_insights"":{""energy_patterns_phases"":[""night""],""optimal_times"":{""night"":2.716},""total_data_points"":456},""learning_metrics"":{""q_value_statistics"":{""min"":-0.036,""mean"":0.081,""max"":2.462}}},""status"":""success""}";
        private string GetSuggestionsJson() => @"{""suggestions"":{""daily_patterns_count"":1,""weekly_patterns_count"":0,""productivity_trends"":{""trend"":""stable""},""most_active_hours"":[[""hour_5"",456]]},""status"":""success""}";
        private string GetActionJson() => @"{""action_taken"":""adaptive_blocking"",""distraction_risk"":0.9,""intervention_message"":""Quest: block distracting sites"",""intervention_id"":""int_1757207252964"",""timestamp"":""2025-09-07T06:37:32.964256"",""status"":""success""}";
    }
}