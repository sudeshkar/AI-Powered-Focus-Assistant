using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FocusAssistant
{
    public partial class MainWindow : Window
    {
        private bool isTracking = false;

        public MainWindow()
        {
            InitializeComponent();
            ShowDashboard(null, null); 
        }

        private void ShowDashboard(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Content = new Views.DashboardView();
        }

        private void ShowTracking(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Content = new Views.TrackingView();
             
        }

        private void ShowGamification(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Content = new TextBlock
            {
                Text = "🏆 Achievements View\n\nComing soon...",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private void ShowRecommendations(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Content = new TextBlock
            {
                Text = "💡 Recommendations View\n\nComing soon...",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private void ShowSettings(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Content = new TextBlock
            {
                Text = "⚙️ Settings View\n\nComing soon...",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private void ToggleTracking(object sender, RoutedEventArgs e)
        {
            isTracking = !isTracking;
            if (isTracking)
            {
                StartTrackingButton.Content = "⏹️ Stop Tracking";
                StartTrackingButton.Background = System.Windows.Media.Brushes.Green;
                // TODO: Start tracking logic
            }
            else
            {
                StartTrackingButton.Content = "🔴 Start Tracking";
                StartTrackingButton.Background = System.Windows.Media.Brushes.Red;
                // TODO: Stop tracking logic
            }
        }
    }
}