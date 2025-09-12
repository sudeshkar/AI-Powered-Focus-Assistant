using FocusAssistant.Models.Response_Models;
using FocusAssistant.Services.Flask;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.ViewModels;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FocusAssistant.Views
{
    public partial class RecommendationsView : UserControl
    {
        public RecommendationsView(ISessionManager sessionManager, FlaskIntegrationFacade facade)
        {
            InitializeComponent();
            DataContext = new RecommendationViewModel(sessionManager, facade); // Pass facade here
        }
    }
}