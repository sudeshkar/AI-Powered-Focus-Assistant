using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FocusAssistant.Models.Response_Models
{
    

    public class InsightsResponse : BaseResponse
    {
        [JsonPropertyName("insights")]
        public Insights? Insights { get; set; }
    }

    public class Insights
    {
        [JsonPropertyName("action_effectiveness")]
        public Dictionary<string, ActionMetrics>? ActionEffectiveness { get; set; }

        [JsonPropertyName("adaptive_performance")]
        public AdaptivePerformance? AdaptivePerformance { get; set; }

        [JsonPropertyName("behavioral_patterns")]
        public BehavioralPatterns? BehavioralPatterns { get; set; }

        [JsonPropertyName("circadian_insights")]
        public CircadianInsights? CircadianInsights { get; set; }

        [JsonPropertyName("learning_metrics")]
        public LearningMetrics? LearningMetrics { get; set; }

        [JsonPropertyName("personalization_profile")]
        public PersonalizationProfile? PersonalizationProfile { get; set; }

        [JsonPropertyName("prediction_accuracy")]
        public PredictionAccuracy? PredictionAccuracy { get; set; }

        [JsonPropertyName("state_space_coverage")]
        public StateSpaceCoverage? StateSpaceCoverage { get; set; }

        [JsonPropertyName("user_engagement_trends")]
        public UserEngagementTrends? UserEngagementTrends { get; set; }
    }

    public class ActionMetrics
    {
        [JsonPropertyName("average_reward")]
        public double AverageReward { get; set; }

        [JsonPropertyName("success_rate")]
        public double SuccessRate { get; set; }

        [JsonPropertyName("total_uses")]
        public int TotalUses { get; set; }
    }

    public class AdaptivePerformance
    {
        [JsonPropertyName("average_performance")]
        public double AveragePerformance { get; set; }

        [JsonPropertyName("current_thresholds")]
        public CurrentThresholds? CurrentThresholds { get; set; }

        [JsonPropertyName("performance_trend")]
        public string? PerformanceTrend { get; set; }
    }

    public class CurrentThresholds
    {
        [JsonPropertyName("distraction_risk")]
        public double DistractionRisk { get; set; }

        [JsonPropertyName("energy_low")]
        public double EnergyLow { get; set; }

        [JsonPropertyName("focus_break_needed")]
        public double FocusBreakNeeded { get; set; }

        [JsonPropertyName("productivity_concern")]
        public double ProductivityConcern { get; set; }
    }

    public class BehavioralPatterns
    {
        [JsonPropertyName("daily_patterns_count")]
        public int DailyPatternsCount { get; set; }

        [JsonPropertyName("most_active_hours")]
        public List<List<object>>? MostActiveHours { get; set; }

        [JsonPropertyName("productivity_trends")]
        public ProductivityTrends? ProductivityTrends { get; set; }

        [JsonPropertyName("weekly_patterns_count")]
        public int WeeklyPatternsCount { get; set; }
    }

    public class ProductivityTrends
    {
        [JsonPropertyName("trend")]
        public string? Trend { get; set; }
    }

    public class CircadianInsights
    {
        [JsonPropertyName("energy_patterns_phases")]
        public List<string>? EnergyPatternsPhases { get; set; }

        [JsonPropertyName("optimal_times")]
        public Dictionary<string, double>? OptimalTimes { get; set; }

        [JsonPropertyName("total_data_points")]
        public int TotalDataPoints { get; set; }
    }

    public class LearningMetrics
    {
        [JsonPropertyName("exploration_rate")]
        public double ExplorationRate { get; set; }

        [JsonPropertyName("learning_rate")]
        public double LearningRate { get; set; }

        [JsonPropertyName("q_value_statistics")]
        public QValueStatistics? QValueStatistics { get; set; }

        [JsonPropertyName("recent_performance")]
        public double RecentPerformance { get; set; }

        [JsonPropertyName("total_feedback_received")]
        public int TotalFeedbackReceived { get; set; }

        [JsonPropertyName("total_states_explored")]
        public int TotalStatesExplored { get; set; }
    }

    public class QValueStatistics
    {
        [JsonPropertyName("max")]
        public double Max { get; set; }

        [JsonPropertyName("mean")]
        public double Mean { get; set; }

        [JsonPropertyName("min")]
        public double Min { get; set; }

        [JsonPropertyName("std")]
        public double Std { get; set; }
    }

    public class PersonalizationProfile
    {
        [JsonPropertyName("feedback_reliability")]
        public double FeedbackReliability { get; set; }

        [JsonPropertyName("preferences")]
        public Preferences? Preferences { get; set; }

        [JsonPropertyName("response_patterns_count")]
        public int ResponsePatternsCount { get; set; }
    }

    public class Preferences
    {
        [JsonPropertyName("communication_style")]
        public string? CommunicationStyle { get; set; }

        [JsonPropertyName("gamification_preference")]
        public bool GamificationPreference { get; set; }

        [JsonPropertyName("motivation_type")]
        public string? MotivationType { get; set; }

        [JsonPropertyName("statistics_interest")]
        public bool StatisticsInterest { get; set; }
    }

    public class PredictionAccuracy
    {
        [JsonPropertyName("accuracy")]
        public double Accuracy { get; set; }

        [JsonPropertyName("sample_size")]
        public int SampleSize { get; set; }
    }

    public class StateSpaceCoverage
    {
        [JsonPropertyName("average_visits_per_state")]
        public double AverageVisitsPerState { get; set; }

        [JsonPropertyName("least_visited_state_visits")]
        public int LeastVisitedStateVisits { get; set; }

        [JsonPropertyName("most_visited_state_visits")]
        public int MostVisitedStateVisits { get; set; }

        [JsonPropertyName("total_unique_states")]
        public int TotalUniqueStates { get; set; }
    }

    public class UserEngagementTrends
    {
        [JsonPropertyName("average_engagement")]
        public double AverageEngagement { get; set; }

        [JsonPropertyName("trend")]
        public string? Trend { get; set; }

        [JsonPropertyName("trend_strength")]
        public double TrendStrength { get; set; }
    }

    public class SuggestionsResponse : BaseResponse
    {
        [JsonPropertyName("suggestions")]
        public Suggestions? Suggestions { get; set; }
    }

    public class Suggestions
    {
        [JsonPropertyName("daily_patterns_count")]
        public int DailyPatternsCount { get; set; }

        [JsonPropertyName("most_active_hours")]
        public List<List<object>>? MostActiveHours { get; set; }

        [JsonPropertyName("productivity_trends")]
        public ProductivityTrends? ProductivityTrends { get; set; }

        [JsonPropertyName("weekly_patterns_count")]
        public int WeeklyPatternsCount { get; set; }
    }

    public class ActionResponse : BaseResponse
    {
        [JsonPropertyName("action_taken")]
        public string? ActionTaken { get; set; }

        [JsonPropertyName("distraction_risk")]
        public double DistractionRisk { get; set; }

        [JsonPropertyName("intervention_id")]
        public string? InterventionId { get; set; }

        [JsonPropertyName("intervention_message")]
        public string? InterventionMessage { get; set; }
    }
}