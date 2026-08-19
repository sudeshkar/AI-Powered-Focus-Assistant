using FocusAssistant.Core.Intervention;
using FocusAssistant.Core.Monitoring;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace FocusAssistant.Views
{
    /// <summary>
    /// The nudge itself: bottom-right, never steals focus, auto-dismisses if ignored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ShowActivated"/> alone is not enough to guarantee a WPF window never takes
    /// focus - it controls whether <c>Show()</c> activates it, but the window can still be
    /// activated later by the usual Windows means (clicking its taskbar entry, Alt+Tab). The
    /// real guarantee is the Win32 <c>WS_EX_NOACTIVATE</c> extended style, applied to the
    /// HWND once it exists. Stealing focus once, mid-keystroke, is the single fastest way to
    /// turn a helpful nudge into a hated one - it does not just interrupt the user, it eats
    /// whatever they were about to type.
    /// </para>
    /// <para>
    /// Auto-dismiss counts as <see cref="InterventionResponse.Ignored"/> rather than leaving
    /// the response unrecorded, because "the user did not interact with it" is itself the
    /// outcome the policy's de-escalation logic needs to see.
    /// </para>
    /// </remarks>
    public partial class NudgeWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(12);

        private readonly InterventionSuggestion _suggestion;
        private readonly IWindowActivator _windowActivator;
        private readonly DispatcherTimer _autoDismissTimer;
        private System.Threading.Tasks.TaskCompletionSource<InterventionResponse>? _completion;

        public NudgeWindow(InterventionSuggestion suggestion, IWindowActivator windowActivator)
        {
            _suggestion = suggestion ?? throw new ArgumentNullException(nameof(suggestion));
            _windowActivator = windowActivator ?? throw new ArgumentNullException(nameof(windowActivator));

            InitializeComponent();

            MessageText.Text = suggestion.Message;
            BackButton.Content = string.IsNullOrEmpty(suggestion.ReturnApp)
                ? "Got it"
                : $"Back to {suggestion.ReturnApp}";

            SourceInitialized += (_, _) => ApplyNoActivateStyle();
            Loaded += (_, _) => PositionBottomRight();

            _autoDismissTimer = new DispatcherTimer { Interval = AutoDismissAfter };
            _autoDismissTimer.Tick += (_, _) => Complete(InterventionResponse.Ignored);
        }

        /// <summary>
        /// Shows the window and returns once the user has answered, dismissed it, or it has
        /// auto-dismissed.
        /// </summary>
        public System.Threading.Tasks.Task<InterventionResponse> ShowAndAwaitResponseAsync()
        {
            _completion = new System.Threading.Tasks.TaskCompletionSource<InterventionResponse>();

            Show();
            BeginFadeIn();
            _autoDismissTimer.Start();

            return _completion.Task;
        }

        private void ApplyNoActivateStyle()
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        private void PositionBottomRight()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 16;
            Top = area.Bottom - ActualHeight - 16;
        }

        private void BeginFadeIn()
        {
            Opacity = 0;
            var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, animation);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_suggestion.ReturnApp))
                _windowActivator.ActivateByProcessName(_suggestion.ReturnApp);

            Complete(InterventionResponse.ActedImmediately);
        }

        private void SnoozeButton_Click(object sender, RoutedEventArgs e) =>
            Complete(InterventionResponse.ActedLater);

        private void ThisIsWorkButton_Click(object sender, RoutedEventArgs e) =>
            Complete(InterventionResponse.DismissedPolitely);

        private void Complete(InterventionResponse response)
        {
            if (_completion is null || _completion.Task.IsCompleted)
                return;

            _autoDismissTimer.Stop();
            _completion.SetResult(response);
            Close();
        }
    }
}
