using FocusAssistant.Data;
using FocusAssistant.Models;
using FocusAssistant.Services.Datafetch.Interfaces;
using FocusAssistant.SQL_analytics;
using FocusAssistant.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

namespace FocusAssistant.Views
{
    /// <summary>
    /// Interaction logic for AnalyticsView.xaml
    /// </summary>
    public partial class AnalyticsView : UserControl
    {
        private readonly IServiceProvider _serviceProvider;
        public AnalyticsView(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();


            var workSessionService = _serviceProvider.GetRequiredService<IBaseService<WorkSession>>();
            var appUsageService = _serviceProvider.GetRequiredService<IBaseService<AppUsage>>();
            var analyticsService = new AnalyticsServiceSQL(workSessionService, appUsageService);
            DataContext = new AnalyticsViewModel(analyticsService);
        }

        
    }
}

