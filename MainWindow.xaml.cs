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
using System.Windows.Media;
using System.ComponentModel;

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

        // %LocalAppData% is always writable, unlike the folder next to the exe
        // if the app is ever installed under Program Files.
        private readonly string LogsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MMH", "logs");

        private string LogFilePath => Path.Combine(LogsFolder, $"mmh-{DateTime.Now:yyyy-MM-dd}.log");

        private static readonly object LogLock = new object();

        // Last orientation the user confirmed per monitor (orientation.json next to profiles.json)
        private OrientationStore? _orientationStore;

        // Monitors that currently have an orientation overlay open (by device name)
        private readonly HashSet<string> _orientationPromptsOpen = new();


        public MainWindow()
        {
            InitializeComponent();
            ScriptDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ScriptsFolder = System.IO.Path.Combine(ScriptDirectory, "scripts");

            ConfigFilePath = System.IO.Path.Combine(ScriptDirectory, "profiles.json");
            _orientationStore = new OrientationStore(System.IO.Path.Combine(ScriptDirectory, "orientation.json"));

            // Load any previously saved profiles (may adjust display count)
            LoadProfiles();

            foreach (var tb in new[] { Profile1_Description, Profile2_Description, Profile3_Description })
                tb.TextChanged += (_, _) => UpdateCards();
            UpdateCards();

            SourceInitialized += (_, _) => UseDarkTitleBar();

            // Mark the profile that matches the real display setup, and keep it current.
            Loaded += async (_, _) => await RefreshLiveDisplaysAsync();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

            // Save on close
            this.Closing += MainWindow_Closing;
        }

        // --- Compact card UI ---

        private bool _editMode;

        private static readonly SolidColorBrush MiniOnBrush = Frozen("#2F8F9D");
        private static readonly SolidColorBrush MiniPrimaryBrush = Frozen("#7FD3DC");
        private static readonly SolidColorBrush MiniOffBrush = Frozen("#3A4F56");
        private static readonly SolidColorBrush ActiveBorderBrush = Frozen("#2F8F9D");
        private static readonly SolidColorBrush IdleBorderBrush = Frozen("#2A3D44");
        private static readonly SolidColorBrush PrimaryDiffersBrush = Frozen("#E8B86A");

        private static SolidColorBrush Frozen(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        private TextBox GetDescriptionBox(int n) => n switch { 1 => Profile1_Description, 2 => Profile2_Description, _ => Profile3_Description };

        // --- Which profile matches the displays that are on right now ---

        // DisplayId -> state, as DisplayConfig's Get-DisplayInfo reports it (same numbering the profiles use).
        // null until the first read finishes.
        private Dictionary<int, (bool Active, bool Primary)>? _liveDisplays;
        private bool _liveReadFailed;
        private int _liveReadSeq;
        private int _displayChangeSeq;

        // Reads the live display state (no admin needed) and refreshes the cards.
        private async Task RefreshLiveDisplaysAsync()
        {
            int seq = ++_liveReadSeq;
            var result = await Task.Run(ReadLiveDisplays);
            if (seq != _liveReadSeq) return; // a newer read has started

            _liveReadFailed = result == null;
            _liveDisplays = result;
            UpdateCards();
            LogActiveProfileIfChanged();
        }

        private string? _lastLoggedActive;

        // One log line whenever the detected active profile changes (including the first read).
        private void LogActiveProfileIfChanged()
        {
            var (active, primaryDiffers) = DetermineActiveProfile();
            string label = active is int a ? $"Profile {a}" + (primaryDiffers ? " (primary differs)" : "") : "none";
            if (label == _lastLoggedActive) return;
            _lastLoggedActive = label;

            string live = _liveDisplays == null
                ? "unknown (read failed, using last applied)"
                : string.Join(", ", _liveDisplays.OrderBy(kv => kv.Key)
                    .Select(kv => $"{kv.Key} {(kv.Value.Active ? "on" : "off")}{(kv.Value.Primary ? " (primary)" : "")}"));
            AppendLog($"Active profile: {label} - live displays: {live}");
        }

        private Dictionary<int, (bool Active, bool Primary)>? ReadLiveDisplays()
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"ConvertTo-Json -Compress -InputObject @(Get-DisplayInfo | Select-Object DisplayId,Active,Primary)\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi)!;
                string json = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);

                var map = new Dictionary<int, (bool, bool)>();
                using var doc = JsonDocument.Parse(json);
                foreach (var d in doc.RootElement.EnumerateArray())
                    map[d.GetProperty("DisplayId").GetInt32()] = (d.GetProperty("Active").GetBoolean(), d.GetProperty("Primary").GetBoolean());
                return map;
            }
            catch (Exception ex)
            {
                AppendLog($"Reading live display state failed, falling back to last applied profile: {ex.Message}");
                return null;
            }
        }

        // Windows changed the display setup (Win+P, Settings, a monitor plugged in or out...).
        // Wait for it to settle, then re-read.
        private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.InvokeAsync(async () =>
        {
            int seq = ++_displayChangeSeq;
            await Task.Delay(1000);
            if (seq == _displayChangeSeq) await RefreshLiveDisplaysAsync();
        });

        // Displays the profile wants on, and the primary it will actually set (same fallback as the Apply script).
        private (bool[] Enabled, int Primary) GetProfileState(int n)
        {
            var enabled = GetDisplaysPanel(n)!.Children.OfType<CheckBox>()
                .OrderBy(cb => (int)cb.Tag).Select(cb => cb.IsChecked == true).ToArray();
            int primary = GetProfilePrimaryIndex(n);
            if (primary < 0 || primary >= enabled.Length || !enabled[primary])
                primary = Array.FindIndex(enabled, v => v);
            return (enabled, primary);
        }

        private enum LiveMatch { None, DisplaysOnly, Exact }

        // Exact: exactly the profile's displays are on and its primary is primary.
        // DisplaysOnly: the right displays are on but a different one is primary (e.g. after Win+P "Extend").
        // Displays that aren't connected count as off.
        private LiveMatch MatchLive(int n, Dictionary<int, (bool Active, bool Primary)> live)
        {
            var (enabled, primary) = GetProfileState(n);
            if (primary < 0) return LiveMatch.None; // profile not set up

            int slots = Math.Max(enabled.Length, live.Count == 0 ? 0 : live.Keys.Max());
            for (int i = 0; i < slots; i++)
            {
                bool want = i < enabled.Length && enabled[i];
                bool isOn = live.TryGetValue(i + 1, out var s) && s.Active;
                if (want != isOn) return LiveMatch.None;
            }
            return live.TryGetValue(primary + 1, out var p) && p.Primary ? LiveMatch.Exact : LiveMatch.DisplaysOnly;
        }

        // The active profile, and whether its primary display differs from the live one.
        // Exact matches win over displays-only ones; among equals, the profile applied last wins.
        private (int? Profile, bool PrimaryDiffers) DetermineActiveProfile()
        {
            int? last = _orientationStore?.LastProfile;
            if (_liveDisplays == null)
                return (_liveReadFailed ? last : null, false); // can't read the real state: best guess

            var live = _liveDisplays;
            foreach (var level in new[] { LiveMatch.Exact, LiveMatch.DisplaysOnly })
            {
                var matches = Enumerable.Range(1, 3).Where(n => MatchLive(n, live) == level).ToList();
                if (matches.Count == 0) continue;
                int pick = last is int l && matches.Contains(l) ? l : matches[0];
                return (pick, level == LiveMatch.DisplaysOnly);
            }
            return (null, false);
        }

        // Refreshes each card's title, mini display icons and Active highlight from the editors.
        private void UpdateCards()
        {
            var (active, primaryDiffers) = DetermineActiveProfile();
            for (int n = 1; n <= 3; n++)
            {
                var (title, mini, pill, card) = n switch
                {
                    1 => (Profile1_Title, Profile1_Mini, Profile1_Active, Profile1_Card),
                    2 => (Profile2_Title, Profile2_Mini, Profile2_Active, Profile2_Card),
                    _ => (Profile3_Title, Profile3_Mini, Profile3_Active, Profile3_Card),
                };

                string desc = GetDescriptionBox(n).Text.Trim();
                title.Text = desc.Length > 0 ? desc : $"Profile {n}";

                var (enabled, primary) = GetProfileState(n);

                mini.Children.Clear();
                if (!enabled.Any(v => v))
                {
                    mini.Children.Add(new TextBlock { Text = "Not set up", FontSize = 11, Foreground = MiniOffBrush });
                }
                for (int i = 0; i < enabled.Length && enabled.Any(v => v); i++)
                {
                    bool on = enabled[i], isPrimary = i == primary;
                    mini.Children.Add(new Border
                    {
                        Width = 22,
                        Height = 14,
                        CornerRadius = new CornerRadius(3),
                        Margin = new Thickness(0, 0, 4, 0),
                        Background = on ? (isPrimary ? MiniPrimaryBrush : MiniOnBrush) : Brushes.Transparent,
                        BorderBrush = on ? MiniPrimaryBrush : MiniOffBrush,
                        BorderThickness = new Thickness(1),
                        ToolTip = $"Display {i + 1}: " + (on ? (isPrimary ? "on, primary" : "on") : "off"),
                        Child = new TextBlock
                        {
                            Text = (i + 1).ToString(),
                            FontSize = 9,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = on ? (isPrimary ? Brushes.Black : Brushes.White) : MiniOffBrush,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        }
                    });
                }

                bool isActive = active == n;
                if (isActive && primaryDiffers && primary >= 0)
                {
                    mini.Children.Add(new TextBlock
                    {
                        Text = "Primary monitor differs",
                        FontSize = 11,
                        Foreground = PrimaryDiffersBrush,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0),
                        ToolTip = $"These displays are on, but display {primary + 1} isn't primary. Click to apply the profile and fix it."
                    });
                }
                pill.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                card.BorderBrush = isActive ? ActiveBorderBrush : IdleBorderBrush;
            }
        }

        private void EditToggle_Click(object sender, RoutedEventArgs e)
        {
            _editMode = !_editMode;
            var vis = _editMode ? Visibility.Visible : Visibility.Collapsed;
            Profile1_Editor.Visibility = Profile2_Editor.Visibility = Profile3_Editor.Visibility = vis;
            EditFooter.Visibility = vis;
            EditButton.Content = _editMode ? "" : ""; // check mark / pencil
            EditButton.ToolTip = _editMode ? "Done editing" : "Edit profiles";
            HintText.Text = _editMode ? "Pick displays and the primary, then click the check mark" : "Click a profile to apply it";

            if (!_editMode) SaveProfiles();
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // Dark title bar to match the window (Windows 10 20H1+ / 11; ignored elsewhere).
        private void UseDarkTitleBar()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int on = 1;
                DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref on, sizeof(int));
            }
            catch { /* cosmetic only */ }
        }

        // --- UI generation for dynamic number of displays ---

        private void GenerateConfigurator(int count)
        {
            if (count < 1) count = 1;
            DisplayCount = count;
            DisplayCountTextBox.Text = DisplayCount.ToString();

            // Helper to populate a profile's two panels
            void PopulateProfilePanels(WrapPanel displaysPanel, WrapPanel primaryPanel, int profileNumber)
            {
                displaysPanel.Children.Clear();
                primaryPanel.Children.Clear();

                for (int i = 0; i < DisplayCount; i++)
                {
                    var cb = new CheckBox
                    {
                        Content = $"{i + 1}",
                        Tag = i,
                        ToolTip = $"Turn display {i + 1} on"
                    };
                    cb.Checked += (_, _) => UpdateCards();
                    cb.Unchecked += (_, _) => UpdateCards();
                    displaysPanel.Children.Add(cb);

                    var rb = new RadioButton
                    {
                        Content = (i + 1).ToString(),
                        GroupName = $"Profile{profileNumber}Primary",
                        Tag = i,
                        ToolTip = $"Make display {i + 1} primary"
                    };
                    rb.Checked += (_, _) => UpdateCards();
                    rb.Unchecked += (_, _) => UpdateCards();
                    primaryPanel.Children.Add(rb);
                }
            }

            PopulateProfilePanels(Profile1_DisplaysPanel, Profile1_PrimaryPanel, 1);
            PopulateProfilePanels(Profile2_DisplaysPanel, Profile2_PrimaryPanel, 2);
            PopulateProfilePanels(Profile3_DisplaysPanel, Profile3_PrimaryPanel, 3);
        }

        // Logging must never crash the app, so all errors are swallowed.
        private void AppendLog(string message)
        {
            try
            {
                Directory.CreateDirectory(LogsFolder);
                lock (LogLock)
                {
                    File.AppendAllText(
                        LogFilePath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                        Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logging failed: {ex.Message}");
            }
        }

        // Regenerate button click
        private void Regenerate_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(DisplayCountTextBox.Text.Trim(), out int requested) && requested > 0)
            {
                GenerateConfigurator(requested);
                UpdateCards();
            }
            else
            {
                MessageBox.Show("Enter a valid positive integer for displays.", "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Helper: build script lines based on enabled array (index 0 -> display 1)
        // Now honors a requested primaryIndex (0-based). If the requested primary is not enabled,
        // falls back to the first enabled display (previous behavior).
        //
        // Every step first checks the display is actually connected, so the same profiles work
        // with 2 or 3 monitors plugged in:
        //  - wanted off but not connected: skipped (it's already off), not an error
        //  - wanted on/primary but not connected: skipped and recorded in $missing; the script
        //    writes them to missingFile and exits 2 (see WrapWithDiagnostics) so the app can tell the user
        // The result must be wrapped with perLine: false.
        private string BuildAlterDisplaysScript(bool[] enabled, int primaryIndex, string missingFile)
        {
            var sb = new StringBuilder();

            static string Q(string s) => s.Replace("'", "''");

            sb.AppendLine("$connected = @(Get-DisplayInfo | ForEach-Object { [int]$_.DisplayId })");
            sb.AppendLine("Write-Host \"Connected displays: $($connected -join ', ')\"");
            sb.AppendLine("$missing = @()");
            sb.AppendLine("function Invoke-Step([string]$desc, [scriptblock]$step) {");
            sb.AppendLine("    Write-Host \"Running: $desc\"");
            sb.AppendLine("    try { & $step } catch { Write-Warning \"FAILED: $desc - $($_.Exception.Message)\"; $script:failed = $true }");
            sb.AppendLine("}");

            void Enable(int n) => sb.AppendLine(
                $"if ($connected -contains {n}) {{ Invoke-Step 'Enable-Display {n}' {{ Enable-Display {n} -ErrorAction Stop }} }} " +
                $"else {{ Write-Warning 'Display {n} is not connected - wanted on, skipped'; if ($missing -notcontains {n}) {{ $missing += {n} }} }}");
            void Disable(int n) => sb.AppendLine(
                $"if ($connected -contains {n}) {{ Invoke-Step 'Disable-Display {n}' {{ Disable-Display {n} -ErrorAction Stop }} }} " +
                $"else {{ Write-Host 'Display {n} is not connected - already off, skipped' }}");
            void Primary(int n) => sb.AppendLine(
                $"if ($connected -contains {n}) {{ Invoke-Step 'Set-DisplayPrimary {n}' {{ Set-DisplayPrimary {n} -ErrorAction Stop }} }} " +
                $"else {{ Write-Warning 'Display {n} is not connected - cannot make it primary'; if ($missing -notcontains {n}) {{ $missing += {n} }} }}");

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

            for (int i = 0; i < enabled.Length; i++)
            {
                if (enabled[i])
                {
                    Enable(i + 1);
                }
            }

            // if only 1 monitor is to be enabled, ensure to set primary here
            // Count enabled monitors correctly (Array.ForEach returns void — use LINQ Count)
            int numOfEnabledMonitors = enabled.Count(v => v);
            bool primarySetEarly = false;
            if (numOfEnabledMonitors == 1 && primaryToSet >= 0)
            {
                Primary(primaryToSet + 1);
                primarySetEarly = true;
            }

            // 2) Then emit Disable lines for monitors that should be disabled
            for (int i = 0; i < enabled.Length; i++)
            {
                if (!enabled[i])
                {
                    Disable(i + 1);
                }
            }

            // 3) Finally, set the primary display (if any) at the end,
            // but only if we didn't already set it earlier.
            if (!primarySetEarly && primaryToSet >= 0)
            {
                Primary(primaryToSet + 1);
            }

            // Hand the missing displays back to the app (it can't read this elevated process's output).
            sb.AppendLine($"Set-Content -Path '{Q(missingFile)}' -Value ($missing -join ',') -Encoding UTF8");

            return sb.ToString();
        }

        private bool WriteScript(string fileName, string scriptContents)
        {
            try
            {
                Directory.CreateDirectory(ScriptsFolder);
                string path = Path.Combine(ScriptsFolder, fileName);
                File.WriteAllText(path, scriptContents, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR writing script: {ex}");
                MessageBox.Show($"Failed to write script: {ex.Message}", "MMH - Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ApplyProfile(int profileNumber)
        {
            var displaysPanel = GetDisplaysPanel(profileNumber);
            if (displaysPanel == null) return;

            var enabled = displaysPanel.Children.OfType<CheckBox>()
                .OrderBy(cb => (int)cb.Tag)
                .Select(cb => cb.IsChecked == true)
                .ToArray();

            int primaryIdx = GetProfilePrimaryIndex(profileNumber);

            string missingFile = Path.Combine(ScriptsFolder, "missing-displays.txt");
            try { File.Delete(missingFile); } catch { /* stale file only matters if exit code is 2 */ }

            string script = WrapWithDiagnostics(BuildAlterDisplaysScript(enabled, primaryIdx, missingFile), perLine: false);

            AppendLog($"--- Apply Profile {profileNumber} --- enabled=[{string.Join(",", enabled)}] primary={primaryIdx + 1}");
            AppendLog("Generated script:" + Environment.NewLine + script);

            // Don't run anything if the script couldn't be written (avoids running a stale script)
            if (!WriteScript(AlterDisplaysPath, script))
                return;

            if (_orientationStore != null)
                _orientationStore.LastProfile = profileNumber;
            UpdateCards();

            // Once the displays have switched, check whether any monitor's orientation
            // differs from what the user last confirmed for this profile.
            RunScript(AlterDisplaysPath, onExit: exitCode => Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(1500); // let Windows finish re-laying out the displays
                _ = RefreshLiveDisplaysAsync();
                CheckOrientation(profileNumber, force: false);

                if (exitCode == 2)
                    ShowMissingDisplaysNotice(profileNumber, missingFile);
            }));
        }

        // Exit code 2: the profile wanted displays on (or primary) that aren't connected right now.
        private void ShowMissingDisplaysNotice(int profileNumber, string missingFile)
        {
            string ids = "";
            try { ids = File.ReadAllText(missingFile).Trim(); } catch { }
            string list = string.IsNullOrEmpty(ids) ? "some displays" : $"display {ids.Replace(",", ", ")}";

            AppendLog($"Profile {profileNumber}: wanted {list} but not connected; the rest was applied.");
            MessageBox.Show(this,
                $"Profile {profileNumber} wants {list} turned on, but it isn't connected right now.\n\nEverything else in the profile was applied.",
                "MMH - Display not connected", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Manual check: confirmations are saved under the profile applied most recently.
        private void CheckOrientation_Click(object sender, RoutedEventArgs e) =>
            CheckOrientation(_orientationStore?.LastProfile, force: true);

        /// <summary>
        /// Opens one orientation overlay per monitor that needs it. With force=false only
        /// monitors whose orientation/aspect differs from what was last confirmed for
        /// <paramref name="profile"/> (or that were never confirmed for it) are asked.
        /// </summary>
        private void CheckOrientation(int? profile, bool force)
        {
            if (_orientationStore == null || (!force && profile == null)) return;

            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            foreach (var m in MonitorHelper.GetAllMonitors())
            {
                if (string.IsNullOrEmpty(m.DeviceName) || _orientationPromptsOpen.Contains(m.DeviceName))
                    continue;

                string desc = $"monitor {m.Index} ({m.DeviceName}, {m.Width}x{m.Height}, orientation {m.Orientation})";
                string? reason = force ? "manual check" : _orientationStore.GetChangeReason(profile!.Value, m);
                if (reason == null)
                {
                    AppendLog($"Orientation check: {desc} matches profile {profile} - no prompt");
                    continue;
                }

                AppendLog($"Orientation check: {desc} - {reason}");
                PromptMonitorOrientation(m, scale, profile);
            }
        }

        // Saves a confirmed orientation under the profile, if one has been applied.
        private void ConfirmOrientation(int? profile, MonitorHelper.MonitorInfo m)
        {
            if (profile == null)
            {
                AppendLog($"Monitor {m.Index}: no profile applied yet, orientation not saved.");
                return;
            }
            _orientationStore?.Confirm(profile.Value, m);
        }

        private static Rect ToDip(Rect px, double scale) =>
            new Rect(px.Left / scale, px.Top / scale, px.Width / scale, px.Height / scale);

        private void PromptMonitorOrientation(MonitorHelper.MonitorInfo m, double scale, int? profile)
        {
            _orientationPromptsOpen.Add(m.DeviceName);

            var prompt = new OrientationPromptWindow(ToDip(m.Bounds, scale), m.Index);
            prompt.Closed += async (_, _) =>
            {
                try
                {
                    int? turns = prompt.ChosenQuarterTurns;
                    if (turns == null)
                    {
                        AppendLog($"Orientation check skipped for monitor {m.Index}.");
                        return;
                    }

                    if (turns == 0)
                    {
                        AppendLog($"Monitor {m.Index} confirmed upright (orientation {m.Orientation}) for profile {profile}.");
                        ConfirmOrientation(profile, m);
                        return;
                    }

                    // The picked label is drawn rotated `turns` quarter-turns clockwise and reads
                    // upright, so the picture is off by that much: counter it.
                    int newOrientation = ((m.Orientation - turns.Value) % 4 + 4) % 4;
                    AppendLog($"Monitor {m.Index}: rotating {m.Orientation} -> {newOrientation}");

                    string? error = MonitorHelper.SetOrientation(m.DeviceName, newOrientation);
                    if (error != null)
                    {
                        AppendLog($"ERROR rotating monitor {m.Index}: {error}");
                        MessageBox.Show($"Could not rotate monitor {m.Index}: {error}", "MMH - Orientation",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    await Task.Delay(800);
                    var updated = MonitorHelper.GetAllMonitors().FirstOrDefault(x => x.DeviceName == m.DeviceName);
                    if (updated == null) return;

                    var confirm = new OrientationConfirmWindow(ToDip(updated.Bounds, VisualTreeHelper.GetDpi(this).DpiScaleX));
                    // Not ShowDialog: that would block the overlays on the other monitors.
                    var closed = new TaskCompletionSource();
                    confirm.Closed += (_, _) => closed.TrySetResult();
                    confirm.Show();
                    await closed.Task;

                    if (confirm.Kept)
                    {
                        AppendLog($"Monitor {m.Index}: kept orientation {updated.Orientation} for profile {profile}.");
                        ConfirmOrientation(profile, updated);
                    }
                    else
                    {
                        AppendLog($"Monitor {m.Index}: reverting to orientation {m.Orientation}.");
                        string? revertError = MonitorHelper.SetOrientation(m.DeviceName, m.Orientation);
                        if (revertError != null)
                            AppendLog($"ERROR reverting monitor {m.Index}: {revertError}");
                    }
                }
                finally
                {
                    _orientationPromptsOpen.Remove(m.DeviceName);
                }
            };
            prompt.Show();
        }

        // XAML event handlers: one-liners that keep your existing Click="..." bindings working
        private void ApplyProfile1_Click(object sender, RoutedEventArgs e) => ApplyProfile(1);
        private void ApplyProfile2_Click(object sender, RoutedEventArgs e) => ApplyProfile(2);
        private void ApplyProfile3_Click(object sender, RoutedEventArgs e) => ApplyProfile(3);

        private WrapPanel? GetDisplaysPanel(int profileNumber) => profileNumber switch
        {
            1 => Profile1_DisplaysPanel,
            2 => Profile2_DisplaysPanel,
            3 => Profile3_DisplaysPanel,
            _ => null
        };

        private int GetProfilePrimaryIndex(int profileNumber)
        {
            var panel = GetPrimaryPanel(profileNumber);
            if (panel == null) return -1;

            var rb = panel.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true);
            return rb?.Tag is int idx ? idx : -1;
        }

        private WrapPanel? GetPrimaryPanel(int profileNumber) => profileNumber switch
        {
            1 => Profile1_PrimaryPanel,
            2 => Profile2_PrimaryPanel,
            3 => Profile3_PrimaryPanel,
            _ => null
        };

        private void IdentifyMonitors_Click(object sender, RoutedEventArgs e)
        {
            if (_identifyBadges.Count > 0) return; // already showing

            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            foreach (var monitor in MonitorHelper.GetAllMonitors())
            {
                var badge = new MonitorBadgeWindow(monitor, ToDip(monitor.Bounds, scale));
                badge.DismissRequested += (_, _) => CloseIdentifyBadges();
                _identifyBadges.Add(badge);
                badge.Show();
            }

            // Fallback so the overlays never linger if the user walks away.
            // (Only closes this batch, not a later one opened after an early Esc.)
            int session = ++_identifySession;
            _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                if (session == _identifySession) CloseIdentifyBadges();
            }));
        }

        private readonly List<MonitorBadgeWindow> _identifyBadges = new();
        private int _identifySession;

        private void CloseIdentifyBadges()
        {
            var badges = _identifyBadges.ToList();
            _identifyBadges.Clear();
            foreach (var b in badges) b.Close();
        }

        private void RestoreAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Enable every display Windows knows about. The primary display is left as-is.
                // Multi-line block, so it's wrapped as a whole rather than line by line.
                string body = @"
$count = (Get-DisplayInfo | Measure-Object).Count
foreach ($n in 1..$count) {
    try { Enable-Display $n -ErrorAction Stop }
    catch { Write-Warning ""FAILED: Enable-Display $n - $($_.Exception.Message)""; $failed = $true }
}
";

                string script = WrapWithDiagnostics(body, perLine: false);
                AppendLog("--- Restore All Displays requested ---");

                if (!WriteScript(AlterDisplaysPath, script))
                    return;

                RunScript(AlterDisplaysPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
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

        /// <param name="perLine">
        /// true: wrap each line in its own try/catch (for simple one-line commands).
        /// false: insert the body as-is, for multi-line PowerShell blocks that handle their own errors
        /// (per-line wrapping would append -ErrorAction to lines ending in '}' and break parsing).
        /// </param>
        private string WrapWithDiagnostics(string body, bool perLine = true)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Import-Module DisplayConfig");
            sb.AppendLine("$failed = $false");
            sb.AppendLine("Write-Host '--- Displays BEFORE ---'");
            // If Get-DisplayInfo isn't found in your module version, use Get-DisplayConfig instead
            sb.AppendLine("Get-DisplayInfo | Format-Table -AutoSize | Out-String -Width 200 | Write-Host");

            if (!perLine)
            {
                sb.AppendLine(body.Trim());
            }
            else foreach (var line in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                sb.AppendLine($"Write-Host 'Running: {line}'");
                sb.AppendLine(
                    $"try {{ {line} -ErrorAction Stop }} " +
                    $"catch {{ Write-Warning \"FAILED: {line} - $($_.Exception.Message)\"; $failed = $true }}");
            }

            sb.AppendLine("Write-Host '--- Displays AFTER ---'");
            sb.AppendLine("Get-DisplayInfo | Format-Table -AutoSize | Out-String -Width 200 | Write-Host");
            sb.AppendLine("if ($failed) { exit 1 }");
            // Profile scripts set $missing for displays they wanted on but weren't connected.
            sb.AppendLine("if ($missing -and $missing.Count -gt 0) { exit 2 }");

            return sb.ToString();
        }

        /// <param name="onExit">Called (on a background thread) with the exit code once the script finishes.</param>
        private void RunScript(string psScriptName, Action<int>? onExit = null)
        {
            try
            {
                string scriptPath = Path.Combine(ScriptsFolder, psScriptName);
                Directory.CreateDirectory(LogsFolder);

                // Escape single quotes for PowerShell single-quoted strings
                static string Q(string s) => s.Replace("'", "''");

                // Start-Transcript captures everything the script writes (output, warnings, errors)
                // into the log file, from inside the elevated process.
                string command =
                    $"Start-Transcript -Path '{Q(LogFilePath)}' -Append | Out-Null; " +
                    $"try {{ & '{Q(scriptPath)}'; exit $LASTEXITCODE }} " +
                    $"catch {{ Write-Error $_; exit 1 }} " +
                    $"finally {{ Stop-Transcript | Out-Null }}";

                var startInfo = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"")
                {
                    UseShellExecute = true,   // required for "runas"
                    Verb = "runas",
                };

                AppendLog($"Launching {psScriptName} (elevated)");
                var proc = Process.Start(startInfo);

                // Record the exit code once PowerShell finishes, without blocking the UI.
                // (Written after the process exits so it doesn't collide with the transcript.)
                if (proc != null)
                {
                    _ = Task.Run(() =>
                    {
                        int exitCode;
                        try
                        {
                            proc.WaitForExit();
                            exitCode = proc.ExitCode;
                            AppendLog($"{psScriptName} finished with exit code {exitCode}");
                        }
                        finally { proc.Dispose(); }
                        onExit?.Invoke(exitCode);
                    });
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                AppendLog("UAC prompt was cancelled; script not run.");
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR launching script: {ex}");
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
            WrapPanel panel = profileNumber switch
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
