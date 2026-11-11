using System;
using System.Windows;
using System.Windows.Input;

namespace MMH
{
    /// <summary>
    /// Full-screen "Identify Monitors" overlay for one monitor, styled like the
    /// orientation clover. Esc or a click raises DismissRequested so all monitors close together.
    /// </summary>
    public partial class MonitorBadgeWindow : Window
    {
        public event EventHandler? DismissRequested;

        public MonitorBadgeWindow(MonitorHelper.MonitorInfo monitor, Rect boundsDip)
        {
            InitializeComponent();

            MonitorText.Text = monitor.Index.ToString();
            TitleText.Text = $"Monitor {monitor.Index}";
            ResolutionText.Text = $"{monitor.Width} × {monitor.Height}";
            OrientationText.Text = monitor.Orientation switch
            {
                1 => "Portrait",
                2 => "Landscape (flipped)",
                3 => "Portrait (flipped)",
                _ => "Landscape",
            };
            PrimaryText.Text = monitor.IsPrimary ? "Primary" : "Secondary";
            DeviceText.Text = monitor.DeviceName.Replace(@"\\.\", "");

            Left = boundsDip.Left;
            Top = boundsDip.Top;
            Width = boundsDip.Width;
            Height = boundsDip.Height;

            KeyDown += (_, e) => { if (e.Key == Key.Escape) DismissRequested?.Invoke(this, EventArgs.Empty); };
            MouseLeftButtonDown += (_, _) => DismissRequested?.Invoke(this, EventArgs.Empty);
            Loaded += (_, _) => Activate();
        }
    }
}
