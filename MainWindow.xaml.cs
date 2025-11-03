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
        // Now honors a requested primaryIndex (0-based). If the requested primary is not enabled,
        // falls back to the first enabled display (previous behavior).
        private string BuildAlterDisplaysScript(bool[] enabled, int primaryIndex)
        {
            var sb = new StringBuilder();

            // Determine which display should be set primary when enabling
            int firstEnabled = Array.FindIndex(enabled, v => v);
            int primaryToSet = -1;

            if (primaryIndex >= 0 && primaryIndex < enabled.Length && enabled[primaryIndex])
            {
                primaryToSet = primaryIndex;
            }
            else
            {
                primaryToSet = firstEnabled;
            }

            // 1) Emit Enable lines first (and set primary immediately after enabling the chosen primary)
            for (int i = 0; i < enabled.Length; i++)
            {
                if (enabled[i])
                {
                    int displayNumber = i + 1;
                    sb.AppendLine($"Enable-Display {displayNumber}");

                    if (i == primaryToSet)
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

            int primaryIdx = GetProfilePrimaryIndex(1);

            string script = BuildAlterDisplaysScript(enabled, primaryIdx);
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

            int primaryIdx = GetProfilePrimaryIndex(2);

            string script = BuildAlterDisplaysScript(enabled, primaryIdx);
            WriteScript(AlterDisplaysPath, script);
            RunScript(AlterDisplaysPath);
        }

        private void ApplyProfile3_Click(object sender, RoutedEventArgs e)
        {
            var enabled = new bool[3]
            {
                Profile3_Display1.IsChecked == true,
                Profile3_Display2.IsChecked == true,
                Profile3_Display3.IsChecked == true
            };

            int primaryIdx = GetProfilePrimaryIndex(3);

            string script = BuildAlterDisplaysScript(enabled, primaryIdx);
            WriteScript(AlterDisplaysPath, script);
            RunScript(AlterDisplaysPath);
        }

        private int GetProfilePrimaryIndex(int profileNumber)
        {
            // returns 0-based index of selected primary, or -1 if none selected
            switch (profileNumber)
            {
                case 1:
                    if (Profile1_Primary1.IsChecked == true) return 0;
                    if (Profile1_Primary2.IsChecked == true) return 1;
                    if (Profile1_Primary3.IsChecked == true) return 2;
                    return -1;
                case 2:
                    if (Profile2_Primary1.IsChecked == true) return 0;
                    if (Profile2_Primary2.IsChecked == true) return 1;
                    if (Profile2_Primary3.IsChecked == true) return 2;
                    return -1;
                case 3:
                    if (Profile3_Primary1.IsChecked == true) return 0;
                    if (Profile3_Primary2.IsChecked == true) return 1;
                    if (Profile3_Primary3.IsChecked == true) return 2;
                    return -1;
                default:
                    return -1;
            }
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

                            // primary
                            SetPrimaryRadioButtons(1, data.Profile1.PrimaryIndex);
                        }

                        if (data.Profile2 != null)
                        {
                            Profile2_Display1.IsChecked = data.Profile2.Displays.Length > 0 && data.Profile2.Displays[0];
                            Profile2_Display2.IsChecked = data.Profile2.Displays.Length > 1 && data.Profile2.Displays[1];
                            Profile2_Display3.IsChecked = data.Profile2.Displays.Length > 2 && data.Profile2.Displays[2];
                            Profile2_Description.Text = data.Profile2.Description ?? string.Empty;

                            // primary
                            SetPrimaryRadioButtons(2, data.Profile2.PrimaryIndex);
                        }

                        if (data.Profile3 != null)
                        {
                            Profile3_Display1.IsChecked = data.Profile3.Displays.Length > 0 && data.Profile3.Displays[0];
                            Profile3_Display2.IsChecked = data.Profile3.Displays.Length > 1 && data.Profile3.Displays[1];
                            Profile3_Display3.IsChecked = data.Profile3.Displays.Length > 2 && data.Profile3.Displays[2];
                            Profile3_Description.Text = data.Profile3.Description ?? string.Empty;

                            // primary
                            SetPrimaryRadioButtons(3, data.Profile3.PrimaryIndex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load profiles: {ex.Message}");
            }
        }

        private void SetPrimaryRadioButtons(int profileNumber, int primaryIndex)
        {
            // Set radio buttons for given profile using a 0-based primaryIndex (-1 = none)
            if (primaryIndex < 0) primaryIndex = -1;

            switch (profileNumber)
            {
                case 1:
                    Profile1_Primary1.IsChecked = primaryIndex == 0;
                    Profile1_Primary2.IsChecked = primaryIndex == 1;
                    Profile1_Primary3.IsChecked = primaryIndex == 2;
                    break;
                case 2:
                    Profile2_Primary1.IsChecked = primaryIndex == 0;
                    Profile2_Primary2.IsChecked = primaryIndex == 1;
                    Profile2_Primary3.IsChecked = primaryIndex == 2;
                    break;
                case 3:
                    Profile3_Primary1.IsChecked = primaryIndex == 0;
                    Profile3_Primary2.IsChecked = primaryIndex == 1;
                    Profile3_Primary3.IsChecked = primaryIndex == 2;
                    break;
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
                        Description = Profile1_Description.Text ?? string.Empty,
                        PrimaryIndex = GetProfilePrimaryIndex(1)
                    },
                    Profile2 = new ProfileData
                    {
                        Displays = new[] { Profile2_Display1.IsChecked == true, Profile2_Display2.IsChecked == true, Profile2_Display3.IsChecked == true },
                        Description = Profile2_Description.Text ?? string.Empty,
                        PrimaryIndex = GetProfilePrimaryIndex(2)
                    },
                    Profile3 = new ProfileData
                    {
                        Displays = new[] { Profile3_Display1.IsChecked == true, Profile3_Display2.IsChecked == true, Profile3_Display3.IsChecked == true },
                        Description = Profile3_Description.Text ?? string.Empty,
                        PrimaryIndex = GetProfilePrimaryIndex(3)
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

        // 0-based index of the primary display for this profile. -1 = none selected.
        public int PrimaryIndex { get; set; } = -1;
    }
}
