using System;
using System.Windows;

namespace FocusAssistant
{
    public class ToastNotification
    {
        public string Message { get; set; }
        public string ButtonText { get; set; }
        public event EventHandler ButtonClicked;

        public ToastNotification()
        {
        }

        public void Show()
        {
            // Fallback: Use MessageBox for simplicity
            var result = MessageBox.Show(
                Message,
                "AI Intervention",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.OK)
            {
                ButtonClicked?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}