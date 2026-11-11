using System;
using System.Windows;
using System.Windows.Threading;

namespace MMH
{
    /// <summary>
    /// Shown after a rotation is applied. Reverts automatically unless the user
    /// clicks Keep, like Windows' own "Keep these display settings?" prompt.
    /// </summary>
    public partial class OrientationConfirmWindow : Window
    {
        private const int TimeoutSeconds = 15;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
        private int _remaining = TimeoutSeconds;

        public bool Kept { get; private set; }

        public OrientationConfirmWindow(Rect boundsDip)
        {
            InitializeComponent();

            Left = boundsDip.Left;
            Top = boundsDip.Top;
            Width = boundsDip.Width;
            Height = boundsDip.Height;

            UpdateCountdown();
            _timer.Tick += (_, _) =>
            {
                _remaining--;
                if (_remaining <= 0) Close();
                else UpdateCountdown();
            };
            Loaded += (_, _) => { Activate(); _timer.Start(); };
            Closed += (_, _) => _timer.Stop();
        }

        private void UpdateCountdown() =>
            CountdownText.Text = $"Reverting in {_remaining} second{(_remaining == 1 ? "" : "s")}";

        private void Keep_Click(object sender, RoutedEventArgs e)
        {
            Kept = true;
            Close();
        }

        private void Revert_Click(object sender, RoutedEventArgs e) => Close();
    }
}
