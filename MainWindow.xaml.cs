using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Management.Automation;

namespace MMH
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnLeftOnly_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the base directory of the application (typically the release directory)
                string scriptDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // Combine the base directory with the relative path to your scripts
                string scriptsFolder = System.IO.Path.Combine(scriptDirectory, "scripts");

                // Combine the scripts folder with the PowerShell script file name
                string powerShellScriptPath = System.IO.Path.Combine(scriptsFolder, "EnableLPrimary.ps1");

                // Prepare the PowerShell command
                string powerShellCommand = $"-NoProfile -ExecutionPolicy Bypass -File \"{powerShellScriptPath}\"";

                // Start the PowerShell process with elevated privileges
                ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe", powerShellCommand)
                {
                    UseShellExecute = true,
                    Verb = "runas", // Run as administrator
                };

                Process.Start(startInfo);
                Console.WriteLine("PowerShell script executed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private void btnThreeMonitors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the base directory of the application (typically the release directory)
                string scriptDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // Combine the base directory with the relative path to your scripts
                string scriptsFolder = System.IO.Path.Combine(scriptDirectory, "scripts");

                // Combine the scripts folder with the PowerShell script file name
                string powerShellScriptPath = System.IO.Path.Combine(scriptsFolder, "Enable3Displays.ps1");

                // Prepare the PowerShell command
                string powerShellCommand = $"-NoProfile -ExecutionPolicy Bypass -File \"{powerShellScriptPath}\"";

                // Start the PowerShell process with elevated privileges
                ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe", powerShellCommand)
                {
                    UseShellExecute = true,
                    Verb = "runas", // Run as administrator
                };

                Process.Start(startInfo);
                Console.WriteLine("PowerShell script executed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}
