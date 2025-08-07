using FocusAssistant.Services;
using System.Configuration;
using System.Data;
using System.Windows;

namespace FocusAssistant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            // Ensure any tracking is stopped when app closes
            base.OnExit(e);
        }
        private async void CheckAIHealth()
        {
            var svc = new FlaskIntegrationService();
            bool ok = await svc.StartFlaskServerAsync();
            var pong = await svc.GetAnalyticsAsync();
            MessageBox.Show(ok && pong != null ? "✅ AI online" : "❌ AI unreachable");
        }
    }

}
