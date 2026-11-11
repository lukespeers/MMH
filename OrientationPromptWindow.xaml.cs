using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MMH
{
    /// <summary>
    /// Full-screen overlay for one monitor. Shows four DOWN petals rotated 0/90/180/270
    /// degrees; the user clicks the one that reads upright, which tells us how the
    /// picture is rotated relative to the physical screen.
    /// </summary>
    public partial class OrientationPromptWindow : Window
    {
        /// <summary>
        /// Clockwise quarter-turns of the label the user picked (0-3), or null if skipped.
        /// 0 means the screen is already the right way up.
        /// </summary>
        public int? ChosenQuarterTurns { get; private set; }

        public OrientationPromptWindow(Rect boundsDip, int monitorNumber)
        {
            InitializeComponent();
            MonitorNumberText.Text = monitorNumber.ToString();

            Left = boundsDip.Left;
            Top = boundsDip.Top;
            Width = boundsDip.Width;
            Height = boundsDip.Height;

            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };
            Loaded += (_, _) => Activate();
        }

        private void Petal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string tag } && int.TryParse(tag, out int turns))
            {
                ChosenQuarterTurns = turns;
                Close();
            }
        }
    }
}
