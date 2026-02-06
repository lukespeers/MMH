using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
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

        // Current display count used to generate UI
        private int DisplayCount = 3;

        public MainWindow()
        {
            InitializeComponent();
            ScriptDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ScriptsFolder = System.IO.Path.Combine(ScriptDirectory, "scripts");

            ConfigFilePath = System.IO.Path.Combine(ScriptDirectory, "profiles.json");

            // Load any previously saved profiles (may adjust display count)
            LoadProfiles();

            // Save on close
            this.Closing += MainWindow_Closing;
        }

        // --- UI generation for dynamic number of displays ---

        private void GenerateConfigurator(int count)
        {
            if (count < 1) count = 1;
            DisplayCount = count;
            DisplayCountTextBox.Text = DisplayCount.ToString();

            // Helper to populate a profile's two panels
            void PopulateProfilePanels(StackPanel displaysPanel, StackPanel primaryPanel, int profileNumber)
            {
                displaysPanel.Children.Clear();
                primaryPanel.Children.Clear();

                for (int i = 0; i < DisplayCount; i++)
                {
                    var cb = new CheckBox
                    {
                        Content = $"{i + 1}",
                        Tag = i,
                        Margin = new Thickness(2)
                    };
                    displaysPanel.Children.Add(cb);

                    var rb = new RadioButton
                    {
                        Content = (i + 1).ToString(),
                        GroupName = $"Profile{profileNumber}Primary",
                        Tag = i,
                        Margin = new Thickness(2)
                    };
                    primaryPanel.Children.Add(rb);
                }
            }

            PopulateProfilePanels(Profile1_DisplaysPanel, Profile1_PrimaryPanel, 1);
            PopulateProfilePanels(Profile2_DisplaysPanel, Profile2_PrimaryPanel, 2);
            PopulateProfilePanels(Profile3_DisplaysPanel, Profile3_PrimaryPanel, 3);
        }

        // Regenerate button click
        private void Regenerate_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(DisplayCountTextBox.Text.Trim(), out int requested) && requested > 0)
            {
                GenerateConfigurator(requested);
            }
            else
            {
                MessageBox.Show("Enter a valid positive integer for displays.", "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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

        // Apply handlers for each profile (now read panels dynamically)
        private void ApplyProfile1_Click(object sender, RoutedEventArgs e)
        {
            var enabled = Profile1_DisplaysPanel.Children.OfType<CheckBox>().OrderBy(cb => (int)cb.Tag)
                .Select(cb => cb.IsChecked == true).ToArray();

            int primaryIdx = GetProfilePrimaryIndex(1);

            string script = BuildAlterDisplaysScript(enabled, primaryIdx);
            WriteScript(AlterDisplaysPath, script);
            RunScript(AlterDisplaysPath);
        }

        private void ApplyProfile2_Click(object sender, RoutedEventArgs e)
        {
            var enabled = Profile2_DisplaysPanel.Children.OfType<CheckBox>().OrderBy(cb => (int)cb.Tag)
                .Select(cb => cb.IsChecked == true).ToArray();

            int primaryIdx = GetProfilePrimaryIndex(2);

            string script = BuildAlterDisplaysScript(enabled, primaryIdx);
            WriteScript(AlterDisplaysPath, script);
            RunScript(AlterDisplaysPath);
        }

        private void ApplyProfile3_Click(object sender, RoutedEventArgs e)
        {
            var enabled = Profile3_DisplaysPanel.Children.OfType<CheckBox>().OrderBy(cb => (int)cb.Tag)
                .Select(cb => cb.IsChecked == true).ToArray();

            int primaryIdx = GetProfilePrimaryIndex(3);

            string script = BuildAlterDisplaysScript(enabled, primaryIdx);
            WriteScript(AlterDisplaysPath, script);
            RunScript(AlterDisplaysPath);
        }

        private int GetProfilePrimaryIndex(int profileNumber)
        {
            StackPanel panel = profileNumber switch
            {
                1 => Profile1_PrimaryPanel,
                2 => Profile2_PrimaryPanel,
                3 => Profile3_PrimaryPanel,
                _ => null
            };

            if (panel == null) return -1;

            var rb = panel.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true);
            if (rb != null && rb.Tag is int idx) return idx;
            return -1;
        }

        private void IdentifyMonitors_Click(object sender, RoutedEventArgs e)
        {
            var monitors = MonitorHelper.GetAllMonitors();
            foreach (var monitor in monitors)
            {
                var badge = new MonitorBadgeWindow(monitor.Index.ToString(), monitor.Bounds);
                badge.Show();

                _ = Task.Delay(3000).ContinueWith(_ =>
                {
                    badge.Dispatcher.Invoke(() => badge.Close());
                });
            }
        }


        private async Task<List<string>> GetMonitorIdsAsync()
        {
            var ids = new List<string>();

            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddScript(@"
            Import-Module DisplayConfig
            Get-DisplayConfig | Select-Object -ExpandProperty DisplayId
        ");

                var results = await Task.Run(() => ps.Invoke());
                foreach (var result in results)
                {
                    ids.Add(result.ToString());
                }
            }

            return ids;
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
                int maxDisplaysFound = DisplayCount;

                ProfilesData? data = null;

                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    data = JsonSerializer.Deserialize<ProfilesData>(json, options);
                }

                // if profile data exists, determine the max displays count used previously
                if (data != null)
                {
                    if (data.Profile1?.Displays != null)
                        maxDisplaysFound = Math.Max(maxDisplaysFound, data.Profile1.Displays.Length);
                    if (data.Profile2?.Displays != null)
                        maxDisplaysFound = Math.Max(maxDisplaysFound, data.Profile2.Displays.Length);
                    if (data.Profile3?.Displays != null)
                        maxDisplaysFound = Math.Max(maxDisplaysFound, data.Profile3.Displays.Length);
                }

                // Regenerate UI to accommodate maxDisplaysFound
                GenerateConfigurator(maxDisplaysFound);

                // Now populate values if data present
                if (data != null)
                {
                    if (data.Profile1 != null)
                    {
                        var checks = Profile1_DisplaysPanel.Children.OfType<CheckBox>().OrderBy(cb => (int)cb.Tag).ToArray();
                        for (int i = 0; i < checks.Length; i++)
                        {
                            checks[i].IsChecked = i < data.Profile1.Displays.Length && data.Profile1.Displays[i];
                        }
                        Profile1_Description.Text = data.Profile1.Description ?? string.Empty;
                        SetPrimaryRadioButtons(1, data.Profile1.PrimaryIndex);
                    }

                    if (data.Profile2 != null)
                    {
                        var checks = Profile2_DisplaysPanel.Children.OfType<CheckBox>().OrderBy(cb => (int)cb.Tag).ToArray();
                        for (int i = 0; i < checks.Length; i++)
                        {
                            checks[i].IsChecked = i < data.Profile2.Displays.Length && data.Profile2.Displays[i];
                        }
                        Profile2_Description.Text = data.Profile2.Description ?? string.Empty;
                        SetPrimaryRadioButtons(2, data.Profile2.PrimaryIndex);
                    }

                    if (data.Profile3 != null)
                    {
                        var checks = Profile3_DisplaysPanel.Children.OfType<CheckBox>().OrderBy(cb => (int)cb.Tag).ToArray();
                        for (int i = 0; i < checks.Length; i++)
                        {
                            checks[i].IsChecked = i < data.Profile3.Displays.Length && data.Profile3.Displays[i];
                        }
                        Profile3_Description.Text = data.Profile3.Description ?? string.Empty;
                        SetPrimaryRadioButtons(3, data.Profile3.PrimaryIndex);
                    }
                }
                else
                {
                    // no saved data: default to 3 displays
                    if (DisplayCount < 3)
                        GenerateConfigurator(3);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load profiles: {ex.Message}");
                // ensure at least the default UI exists
                GenerateConfigurator(DisplayCount);
            }
        }

        private void SetPrimaryRadioButtons(int profileNumber, int primaryIndex)
        {
            StackPanel panel = profileNumber switch
            {
                1 => Profile1_PrimaryPanel,
                2 => Profile2_PrimaryPanel,
                3 => Profile3_PrimaryPanel,
                _ => null
            };

            if (panel == null) return;

            foreach (var rb in panel.Children.OfType<RadioButton>())
            {
                if (rb.Tag is int idx)
                    rb.IsChecked = (idx == primaryIndex);
                else
                    rb.IsChecked = false;
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
                        Displays = Profile1_DisplaysPanel.Children.OfType<CheckBox>().OrderBy(cb => (int)cb.Tag).Select(cb => cb.IsChecked == true).ToArray(),
                        Description = Profile1_Description.Text ?? string.Empty,
                        PrimaryIndex = GetProfilePrimaryIndex(1)
                    },
                    Profile2 = new ProfileData
                    {
                        Displays = Profile2_DisplaysPanel.Children.OfType<CheckBox>().OrderBy(cb => (int)cb.Tag).Select(cb => cb.IsChecked == true).ToArray(),
                        Description = Profile2_Description.Text ?? string.Empty,
                        PrimaryIndex = GetProfilePrimaryIndex(2)
                    },
                    Profile3 = new ProfileData
                    {
                        Displays = Profile3_DisplaysPanel.Children.OfType<CheckBox>().OrderBy(cb => (int)cb.Tag).Select(cb => cb.IsChecked == true).ToArray(),
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
