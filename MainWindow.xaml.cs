using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MMH
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Absolute paths requested by the user
        private const string AlterDisplaysPath = @"AlterDisplays.ps1";
        private const string AlterDisplaysPath2 = @"AlterDisplays2.ps1";
        private const string AlterDisplaysPath3 = @"AlterDisplays3.ps1";

        private string ScriptDirectory = "";
        private string ScriptsFolder = "";

        public MainWindow()
        {
            InitializeComponent();
            ScriptDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ScriptsFolder = System.IO.Path.Combine(ScriptDirectory, "scripts");
        }

        // Helper: build script lines based on enabled array (index 0 -> display 1)
        private string BuildAlterDisplaysScript(bool[] enabled)
        {
            var sb = new StringBuilder();

            // Find first enabled display (if any) so we can set it primary immediately after enabling it
            int firstEnabled = Array.FindIndex(enabled, v => v);

            // 1) Emit Enable lines first (and set primary immediately after enabling the first enabled)
            for (int i = 0; i < enabled.Length; i++)
            {
                if (enabled[i])
                {
                    int displayNumber = i + 1;
                    sb.AppendLine($"Enable-Display {displayNumber}");

                    if (i == firstEnabled)
                    {
                        sb.AppendLine($"Set-DisplayPrimary {displayNumber}");
                    }
                }
            }

            // 2) Then emit Disable lines for monitors that should be disabled
            for (int i = 0; i < enabled.Length; i++)
            {
                if (!enabled[i])
                {
                    int displayNumber = i + 1;
                    sb.AppendLine($"Disable-Display {displayNumber}");
                }
            }
            //sb.AppendLine($"Read-Host -Prompt \"Press Enter to close this window\""); //uncomment for debugging
            
            // Update preview textbox (so user sees what was executed)
            ScriptPreview.Text = sb.ToString();

            return sb.ToString();
        }

        // Writes script and runs RunPSCommand.bat with the script path as its first argument (elevated)
        private void WriteScript(string fileName, string scriptContents)
        {
            try
            {
                // Write the script to the exact file requested
                string powerShellScriptPath = System.IO.Path.Combine(ScriptsFolder, fileName);
                File.WriteAllText(powerShellScriptPath, scriptContents, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to write or run script: {ex.Message}", "MMH - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Apply handlers for each profile
        private void ApplyProfile1_Click(object sender, RoutedEventArgs e)
        {
            var enabled = new bool[3]
            {
                Profile1_Display1.IsChecked == true,
                Profile1_Display2.IsChecked == true,
                Profile1_Display3.IsChecked == true
            };

            string script = BuildAlterDisplaysScript(enabled);
            WriteScript(AlterDisplaysPath, script);
            RunScript(AlterDisplaysPath);
        }

        private void ApplyProfile2_Click(object sender, RoutedEventArgs e)
        {
            var enabled = new bool[3]
            {
                Profile2_Display1.IsChecked == true,
                Profile2_Display2.IsChecked == true,
                Profile2_Display3.IsChecked == true
            };

            string script = BuildAlterDisplaysScript(enabled);
            WriteScript(AlterDisplaysPath2, script);
            RunScript(AlterDisplaysPath2);
        }

        private void ApplyProfile3_Click(object sender, RoutedEventArgs e)
        {
            var enabled = new bool[3]
            {
                Profile3_Display1.IsChecked == true,
                Profile3_Display2.IsChecked == true,
                Profile3_Display3.IsChecked == true
            };

            string script = BuildAlterDisplaysScript(enabled);
            WriteScript(AlterDisplaysPath3, script);
            RunScript(AlterDisplaysPath3);
        }

        private void RunScript(string psScriptName)
        {
            try
            {
                string powerShellScriptPath = System.IO.Path.Combine(ScriptsFolder, psScriptName);
                string powerShellCommand = $"-NoProfile -ExecutionPolicy Bypass -File \"{powerShellScriptPath}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe", powerShellCommand)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        // Existing buttons (unchanged)
        private void btnLeftOnly_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string powerShellScriptPath = System.IO.Path.Combine(ScriptsFolder, "EnableLPrimary.ps1");
                string powerShellCommand = $"-NoProfile -ExecutionPolicy Bypass -File \"{powerShellScriptPath}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe", powerShellCommand)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private void btnThreeMonitors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string powerShellScriptPath = System.IO.Path.Combine(ScriptsFolder, "Enable3Displays.ps1");
                string powerShellCommand = $"-NoProfile -ExecutionPolicy Bypass -File \"{powerShellScriptPath}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe", powerShellCommand)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }
    }
}
