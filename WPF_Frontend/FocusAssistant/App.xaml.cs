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
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _ = EnsureBackendAsync(); // fire-and-forget on UI thread startup
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }

        private async Task EnsureBackendAsync()
        {
            try
            {
                var svc = new FlaskIntegrationService();
                bool ok = await svc.StartFlaskServerAsync();
                if (ok)
                {
                    await svc.GetAnalyticsAsync();
                }
            }
            catch { }
        }
    }

}
