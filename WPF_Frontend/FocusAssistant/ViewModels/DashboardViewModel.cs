using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FocusAssistant.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly DashboardViewModel _viewModel;

        // Fields for the properties
        private string _productivityRate;
        private string _totalActivities;
        private string _recentInterventions;
        private ObservableCollection<KeyValuePair<string, int>> _topApps = new ObservableCollection<KeyValuePair<string, int>>();
        private int _topAppsCount;


         
         
        public DashboardViewModel()
        {
            // Initial values, could be set to defaults or empty state
            _productivityRate = "0%";
            _totalActivities = "0";
            _recentInterventions = "0";
            _topAppsCount = 0;
        }

        // Parameterized constructor (optional, for passing data directly)
        public DashboardViewModel(string productivityRate, string totalActivities, string recentInterventions, ObservableCollection<KeyValuePair<string, int>> topApps)
        {
            _productivityRate = productivityRate;
            _totalActivities = totalActivities;
            _recentInterventions = recentInterventions;
            _topApps = topApps;
            _topAppsCount = topApps.Count;
        }

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

        // Properties for binding
        public string ProductivityRate
        {
            get => _productivityRate;
            set => SetProperty(ref _productivityRate, value);
        }

        public string TotalActivities
        {
            get => _totalActivities;
            set => SetProperty(ref _totalActivities, value);
        }

        public string RecentInterventions
        {
            get => _recentInterventions;
            set => SetProperty(ref _recentInterventions, value);
        }

        public ObservableCollection<KeyValuePair<string, int>> TopApps
        {
            get => _topApps;
            set => SetProperty(ref _topApps, value);
        }

        public int TopAppsCount
        {
            get => _topAppsCount;
            set => SetProperty(ref _topAppsCount, value);
        }
    }
}
