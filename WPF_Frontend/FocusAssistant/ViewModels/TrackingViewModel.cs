using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FocusAssistant.ViewModels
{
    public class TrackingViewModel : INotifyPropertyChanged
    {
        private bool _isTracking;
        private string _statusText;
        private string _recentInterventionMessage;
        private string _currentApp;
        private string _currentWindow;
        private string _currentDuration;
        private string _productivityRate;
        private string _totalActivities;
        private string _recentInterventions;
        private string _productivityScore;
        private string _productiveTime;
        private string _distractedTime;
        private string _mostProductiveHour;
        private string _leastProductiveHour;
        private string _productivityStreak;
        private string _userEngagementTrend;
        private string _actionEffectiveness;
        private ObservableCollection<ActivityLogItem> _activityLog = new ObservableCollection<ActivityLogItem>();

        public bool IsTracking
        {
            get => _isTracking;
            set { _isTracking = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string RecentInterventionMessage
        {
            get => _recentInterventionMessage;
            set { _recentInterventionMessage = value; OnPropertyChanged(); }
        }

        public string CurrentApp
        {
            get => _currentApp;
            set { _currentApp = value; OnPropertyChanged(); }
        }

        public string CurrentWindow
        {
            get => _currentWindow;
            set { _currentWindow = value; OnPropertyChanged(); }
        }

        public string CurrentDuration
        {
            get => _currentDuration;
            set { _currentDuration = value; OnPropertyChanged(); }
        }

        public string ProductivityRate
        {
            get => _productivityRate;
            set { _productivityRate = value; OnPropertyChanged(); }
        }

        public string TotalActivities
        {
            get => _totalActivities;
            set { _totalActivities = value; OnPropertyChanged(); }
        }

        public string RecentInterventions
        {
            get => _recentInterventions;
            set { _recentInterventions = value; OnPropertyChanged(); }
        }

        public string ProductivityScore
        {
            get => _productivityScore;
            set { _productivityScore = value; OnPropertyChanged(); }
        }

        public string ProductiveTime
        {
            get => _productiveTime;
            set { _productiveTime = value; OnPropertyChanged(); }
        }

        public string DistractedTime
        {
            get => _distractedTime;
            set { _distractedTime = value; OnPropertyChanged(); }
        }

        public string MostProductiveHour
        {
            get => _mostProductiveHour;
            set { _mostProductiveHour = value; OnPropertyChanged(); }
        }

        public string LeastProductiveHour
        {
            get => _leastProductiveHour;
            set { _leastProductiveHour = value; OnPropertyChanged(); }
        }

        public string ProductivityStreak
        {
            get => _productivityStreak;
            set { _productivityStreak = value; OnPropertyChanged(); }
        }

        public string UserEngagementTrend
        {
            get => _userEngagementTrend;
            set { _userEngagementTrend = value; OnPropertyChanged(); }
        }

        public string ActionEffectiveness
        {
            get => _actionEffectiveness;
            set { _actionEffectiveness = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ActivityLogItem> ActivityLog
        {
            get => _activityLog;
            set { _activityLog = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ActivityLogItem
    {
        public string AppName { get; set; }
        public string DurationText { get; set; }
    }
}