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
    }

}
