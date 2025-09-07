using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using FocusAssistant.Models.Response_Models;
using LiveChartsCore;

namespace FocusAssistant.ViewModels
{
    public class RecommendationViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<string> _aiSuggestions = new ObservableCollection<string>();
        private ObservableCollection<string> _mostActiveHours = new ObservableCollection<string>();
        private string _productivityTrend;
        private ObservableCollection<KeyValuePair<string, ActionMetrics>> _actionEffectiveness = new ObservableCollection<KeyValuePair<string, ActionMetrics>>();
        private string _energyPatterns;
        private string _optimalTimes;
        private string _recentInterventionMessage;
        private string _recentInterventionDetails;
        public List<double> QValueStats { get; } = new List<double>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public ObservableCollection<string> AISuggestions
        {
            get => _aiSuggestions;
            set => SetProperty(ref _aiSuggestions, value);
        }

        public ObservableCollection<string> MostActiveHours
        {
            get => _mostActiveHours;
            set => SetProperty(ref _mostActiveHours, value);
        }

        public string ProductivityTrend
        {
            get => _productivityTrend;
            set => SetProperty(ref _productivityTrend, value);
        }

        public ObservableCollection<KeyValuePair<string, ActionMetrics>> ActionEffectiveness
        {
            get => _actionEffectiveness;
            set => SetProperty(ref _actionEffectiveness, value);
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

        
        
    }
}