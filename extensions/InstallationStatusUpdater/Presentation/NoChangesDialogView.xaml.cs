using Playnite.SDK;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace InstallationStatusUpdater.Presentation
{
    public partial class NoChangesDialogView : UserControl
    {
        private readonly DispatcherTimer _timer;
        private int _remainingSeconds = 5;

        public NoChangesDialogView()
        {
            InitializeComponent();
            UpdateCountdownText();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;

            Loaded += (s, e) => _timer.Start();
            Unloaded += (s, e) => _timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _remainingSeconds--;
            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                CloseParentWindow();
            }
            else
            {
                UpdateCountdownText();
            }
        }

        private void UpdateCountdownText()
        {
            var format = ResourceProvider.GetString("LOCInstallation_Status_Updater_NoChangesOkButton");
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "OK ({0}s)";
            }
            OkButton.Content = string.Format(format, _remainingSeconds);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            CloseParentWindow();
        }

        private void CloseParentWindow()
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
