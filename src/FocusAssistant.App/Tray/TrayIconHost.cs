using FocusAssistant.Core.Session;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace FocusAssistant.Tray
{
    /// <summary>
    /// The tray icon: the app's main surface once the window is closed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With tracking running in the background the window is shut most of the time, so this
    /// is where the app has to be legible. The tooltip carries the state worth knowing at a
    /// glance - whether a session is running and how much focused time is in it - so the
    /// common question is answered by hovering rather than by restoring a window.
    /// </para>
    /// <para>
    /// WinForms' NotifyIcon rather than a WPF one: WPF has never shipped a tray icon, and
    /// WPF-UI removed its own in v4. This is the standard way to do it and costs one
    /// framework reference; hand-rolling Shell_NotifyIcon would be more code for the same
    /// result and a worse right-click menu.
    /// </para>
    /// </remarks>
    public sealed class TrayIconHost : IDisposable
    {
        private readonly ISessionEngine _sessionEngine;
        private readonly Action _showWindow;
        private readonly Action _exit;

        private NotifyIcon? _icon;
        private bool _disposed;

        public TrayIconHost(ISessionEngine sessionEngine, Action showWindow, Action exit)
        {
            _sessionEngine = sessionEngine ?? throw new ArgumentNullException(nameof(sessionEngine));
            _showWindow = showWindow ?? throw new ArgumentNullException(nameof(showWindow));
            _exit = exit ?? throw new ArgumentNullException(nameof(exit));
        }

        public void Initialize()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Focus Assistant", null, (_, _) => _showWindow());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, _) => _exit());

            _icon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "Focus Assistant",
                Visible = true,
                ContextMenuStrip = menu,
            };

            // Double-click, not single: a single left click on a tray icon is how people
            // dismiss the balloon or just brush past it, and popping a window open for that
            // is startling.
            _icon.DoubleClick += (_, _) => _showWindow();

            UpdateTooltip();
        }

        /// <summary>
        /// Refreshes the hover text. Cheap enough to call whenever the window state changes;
        /// the statistics are computed from memory, not from the database.
        /// </summary>
        public void UpdateTooltip()
        {
            if (_icon is null)
                return;

            try
            {
                string text;
                if (!_sessionEngine.IsSessionActive)
                {
                    text = "Focus Assistant - not tracking";
                }
                else
                {
                    var focused = _sessionEngine.GetTodayStatistics().TotalProductiveTime;
                    var goal = _sessionEngine.CurrentGoal;

                    text = goal is null
                        ? $"Focused {(int)focused.TotalHours}h {focused.Minutes}m today"
                        : $"Focused {(int)focused.TotalHours}h {focused.Minutes}m · {goal}";
                }

                // The shell truncates anything past 63 characters, and a goal can be long.
                _icon.Text = text.Length > 63 ? text[..60] + "..." : text;
            }
            catch (Exception)
            {
                // A tooltip is never worth an exception reaching the dispatcher.
                _icon.Text = "Focus Assistant";
            }
        }

        /// <summary>
        /// Uses the executable's own icon, falling back to a stock one.
        /// </summary>
        /// <remarks>
        /// The app has no icon of its own yet. Falling back to the generic application icon
        /// keeps the tray usable rather than throwing, and swapping in a real .ico later is
        /// a one-line change here.
        /// </remarks>
        private static Icon LoadIcon()
        {
            try
            {
                var path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path))
                {
                    var extracted = Icon.ExtractAssociatedIcon(path);
                    if (extracted is not null)
                        return extracted;
                }
            }
            catch (Exception)
            {
                // Fall through to the stock icon.
            }

            return SystemIcons.Application;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_icon is not null)
            {
                // Explicitly hidden first: an undisposed tray icon lingers as a ghost until
                // the user hovers over it.
                _icon.Visible = false;
                _icon.Dispose();
            }
        }
    }
}
