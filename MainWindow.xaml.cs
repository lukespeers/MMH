using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;
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

        // Config file next to the executable as requested
        private string ConfigFilePath = "";

        public MainWindow()
        {
            InitializeComponent();
            ScriptDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ScriptsFolder = System.IO.Path.Combine(ScriptDirectory, "scripts");

            ConfigFilePath = System.IO.Path.Combine(ScriptDirectory, "profiles.json");

            // Load any previously saved profiles
            LoadProfiles();

            // Save on close
            this.Closing += MainWindow_Closing;
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

        // --- Persist/Restore profiles ---

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                SaveProfiles();
            }
            catch
            {
                // ignore save errors on exit
            }
        }

        private void LoadProfiles()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    ProfilesData? data = JsonSerializer.Deserialize<ProfilesData>(json, options);

                    if (data != null)
                    {
                        if (data.Profile1 != null)
                        {
                            Profile1_Display1.IsChecked = data.Profile1.Displays.Length > 0 && data.Profile1.Displays[0];
                            Profile1_Display2.IsChecked = data.Profile1.Displays.Length > 1 && data.Profile1.Displays[1];
                            Profile1_Display3.IsChecked = data.Profile1.Displays.Length > 2 && data.Profile1.Displays[2];
                            Profile1_Description.Text = data.Profile1.Description ?? string.Empty;
                        }

                        if (data.Profile2 != null)
                        {
                            Profile2_Display1.IsChecked = data.Profile2.Displays.Length > 0 && data.Profile2.Displays[0];
                            Profile2_Display2.IsChecked = data.Profile2.Displays.Length > 1 && data.Profile2.Displays[1];
                            Profile2_Display3.IsChecked = data.Profile2.Displays.Length > 2 && data.Profile2.Displays[2];
                            Profile2_Description.Text = data.Profile2.Description ?? string.Empty;
                        }

                        if (data.Profile3 != null)
                        {
                            Profile3_Display1.IsChecked = data.Profile3.Displays.Length > 0 && data.Profile3.Displays[0];
                            Profile3_Display2.IsChecked = data.Profile3.Displays.Length > 1 && data.Profile3.Displays[1];
                            Profile3_Display3.IsChecked = data.Profile3.Displays.Length > 2 && data.Profile3.Displays[2];
                            Profile3_Description.Text = data.Profile3.Description ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load profiles: {ex.Message}");
            }
        }

        private void SaveProfiles()
        {
            try
            {
                var data = new ProfilesData
                {
                    Profile1 = new ProfileData
                    {
                        Displays = new[] { Profile1_Display1.IsChecked == true, Profile1_Display2.IsChecked == true, Profile1_Display3.IsChecked == true },
                        Description = Profile1_Description.Text ?? string.Empty
                    },
                    Profile2 = new ProfileData
                    {
                        Displays = new[] { Profile2_Display1.IsChecked == true, Profile2_Display2.IsChecked == true, Profile2_Display3.IsChecked == true },
                        Description = Profile2_Description.Text ?? string.Empty
                    },
                    Profile3 = new ProfileData
                    {
                        Displays = new[] { Profile3_Display1.IsChecked == true, Profile3_Display2.IsChecked == true, Profile3_Display3.IsChecked == true },
                        Description = Profile3_Description.Text ?? string.Empty
                    }
                };

                // Write next to the executable
                string dir = Path.GetDirectoryName(ConfigFilePath) ?? ScriptDirectory;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(ConfigFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save profiles: {ex.Message}");
            }
        }
    }

    // POCOs used for JSON persistence
    public class ProfilesData
    {
        public ProfileData? Profile1 { get; set; } = new ProfileData();
        public ProfileData? Profile2 { get; set; } = new ProfileData();
        public ProfileData? Profile3 { get; set; } = new ProfileData();
    }

    public class ProfileData
    {
        // default to three displays for backward compatibility
        public bool[] Displays { get; set; } = new[] { false, false, false };
        public string? Description { get; set; } = string.Empty;
    }
}
