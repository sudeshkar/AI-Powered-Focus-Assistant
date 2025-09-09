using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FocusAssistant
{
    /// <summary>
    /// Interaction logic for AiInterventionWindow.xaml
    /// </summary>
    public partial class AiInterventionWindow : Window
    {
        public AiInterventionWindow(string message)
        {
            InitializeComponent();
            AiTextBlock.Text = message;

            // Optional: auto-close after 5 seconds
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                this.Close();
            };
            timer.Start();
        }
    }

}
