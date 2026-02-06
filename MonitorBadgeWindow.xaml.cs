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
using static System.Net.Mime.MediaTypeNames;

namespace MMH
{
    /// <summary>
    /// Interaction logic for MonitorBadgeWindow.xaml
    /// </summary>
    public partial class MonitorBadgeWindow : Window
    {
        public MonitorBadgeWindow(string text, Rect bounds)

        {
            InitializeComponent();
            MonitorText.Text = text;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = bounds.Left + (bounds.Width - Width) / 2;
            Top = bounds.Top + (bounds.Height - Height) / 2;

        }
    }
}
