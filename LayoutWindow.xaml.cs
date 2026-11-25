using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MMH
{
    /// <summary>
    /// Edits the monitor layout saved for one profile, like Windows Settings > Display:
    /// drag monitors to arrange them and pick each one's rotation. Editing doesn't touch the
    /// screens; Save stores the layout (restored whenever that profile is applied), and
    /// "Save &amp; apply now" also applies it with the Keep/Revert prompt.
    /// Positions are in physical pixels, like Windows uses them; the primary stays at 0,0.
    /// </summary>
    public partial class LayoutWindow : Window
    {
        private class Tile
        {
            public LayoutStore.Entry E = null!;   // as loaded (saved, or live when nothing is saved)
            public double X, Y;
            public int W, H, Orientation;
            public bool Connected;                 // the monitor is on right now
            public Border View = null!;
        }

        private readonly LayoutStore _layouts;
        private readonly OrientationStore _orientation;
        private readonly Action<string> _log;

        private int _profile = 1;
        private bool _hasSaved;
        private List<Tile> _tiles = new();
        private Tile? _selected;
        private Tile? _dragging;
        private Point _dragOffset;      // pointer position inside the dragged tile, in pixels
        private double _scale = 0.1;    // canvas units per desktop pixel
        private double _originX, _originY;
        private bool _updatingChips;

        public LayoutWindow(LayoutStore layouts, OrientationStore orientation, int profile, Action<string> log)
        {
            InitializeComponent();
            _layouts = layouts;
            _orientation = orientation;
            _log = log;
            _profile = profile is >= 1 and <= 3 ? profile : 1;

            Loaded += (_, _) =>
            {
                _updatingChips = true;
                (_profile switch { 2 => Profile2Chip, 3 => Profile3Chip, _ => Profile1Chip }).IsChecked = true;
                _updatingChips = false;
                LoadProfile();
            };
        }

        // --- Model ---

        /// <summary>
        /// Loads the profile's saved layout. Without one, starts from the monitors that are on now.
        /// </summary>
        private void LoadProfile()
        {
            var live = MonitorHelper.GetAllMonitors();
            var liveKeys = live.Select(m => m.Key).ToHashSet();
            var saved = _layouts.Get(_profile);
            _hasSaved = saved != null;

            var entries = saved ?? live.Select(LayoutStore.FromMonitor).ToList();

            // Use today's monitor numbers in labels where the monitor is connected.
            foreach (var e in entries)
            {
                var m = live.FirstOrDefault(x => x.Key == e.Key);
                if (m != null) e.Number = m.Index;
            }

            string? selectedKey = _selected?.E.Key;
            _tiles = entries.Select(e => new Tile
            {
                E = e, X = e.X, Y = e.Y, W = e.Width, H = e.Height, Orientation = e.Orientation,
                Connected = liveKeys.Contains(e.Key),
            }).ToList();
            _selected = _tiles.FirstOrDefault(t => t.E.Key == selectedKey)
                        ?? _tiles.FirstOrDefault(t => t.E.IsPrimary)
                        ?? _tiles.FirstOrDefault();
            BuildViews();
        }

        private bool HasChanges => !_hasSaved || _tiles.Any(t =>
            (int)Math.Round(t.X) != t.E.X || (int)Math.Round(t.Y) != t.E.Y || t.Orientation != t.E.Orientation);

        private List<LayoutStore.Entry> CurrentEntries() => _tiles.Select(t => new LayoutStore.Entry
        {
            Key = t.E.Key,
            X = (int)Math.Round(t.X),
            Y = (int)Math.Round(t.Y),
            Orientation = t.Orientation,
            Width = t.W,
            Height = t.H,
            IsPrimary = t.E.IsPrimary,
            Number = t.E.Number,
        }).ToList();

        // Keeps the primary monitor at 0,0, as Windows requires.
        private void Normalize()
        {
            var p = _tiles.FirstOrDefault(t => t.E.IsPrimary);
            if (p == null) return;
            double dx = p.X, dy = p.Y;
            foreach (var t in _tiles) { t.X -= dx; t.Y -= dy; }
        }

        /// <summary>
        /// Moves a tile to the nearest spot where it touches another monitor along an edge
        /// without overlapping any, snapping to aligned edges when close.
        /// </summary>
        private void Snap(Tile t)
        {
            var others = _tiles.Where(o => o != t).ToList();
            if (others.Count == 0) { t.X = 0; t.Y = 0; return; }

            var candidates = new List<Point>();
            foreach (var o in others)
            {
                double thr = Math.Max(60, 0.08 * Math.Max(Math.Max(o.W, o.H), Math.Max(t.W, t.H)));
                double y = Align(Math.Clamp(t.Y, o.Y - t.H + 1, o.Y + o.H - 1), o.Y, o.Y + o.H, t.H, thr);
                double x = Align(Math.Clamp(t.X, o.X - t.W + 1, o.X + o.W - 1), o.X, o.X + o.W, t.W, thr);
                candidates.Add(new Point(o.X + o.W, y)); // right of o
                candidates.Add(new Point(o.X - t.W, y)); // left of o
                candidates.Add(new Point(x, o.Y + o.H)); // below o
                candidates.Add(new Point(x, o.Y - t.H)); // above o
            }

            var best = candidates
                .Where(c => !others.Any(o => Overlaps(c.X, c.Y, t.W, t.H, o)))
                .OrderBy(c => (c.X - t.X) * (c.X - t.X) + (c.Y - t.Y) * (c.Y - t.Y))
                .Cast<Point?>()
                .FirstOrDefault();
            if (best is Point b)
            {
                t.X = Math.Round(b.X);
                t.Y = Math.Round(b.Y);
            }
        }

        private static double Align(double v, double start, double end, double size, double thr)
        {
            if (Math.Abs(v - start) < thr) return start;            // tops / lefts line up
            if (Math.Abs(v + size - end) < thr) return end - size;  // bottoms / rights line up
            return v;
        }

        private static bool Overlaps(double x, double y, int w, int h, Tile o) =>
            x < o.X + o.W && x + w > o.X && y < o.Y + o.H && y + h > o.Y;

        // --- View ---

        private static readonly Brush TileBrush = Brush("#2F8F9D");
        private static readonly Brush TileSelectedBrush = Brush("#3FB4C3");
        private static readonly Brush TileOfflineBrush = Brush("#24414A");
        private static readonly Brush TileStrokeBrush = Brush("#7FD3DC");
        private static readonly Brush TileDimStrokeBrush = Brush("#1F5E68");

        private static Brush Brush(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        private void BuildViews()
        {
            LayoutCanvas.Children.Clear();
            foreach (var t in _tiles)
            {
                var text = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
                text.Children.Add(new TextBlock { Text = t.E.Number > 0 ? t.E.Number.ToString() : "?", FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
                text.Children.Add(Small($"{t.W}×{t.H}"));
                if (t.E.IsPrimary) text.Children.Add(Small("primary"));
                if (!t.Connected) text.Children.Add(Small("not connected"));

                var view = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Child = text,
                    Cursor = Cursors.SizeAll,
                    ToolTip = $"Monitor {t.E.Number}" + (t.Connected ? "" : " (not connected right now)") + " - drag to move, click to select",
                };
                view.MouseLeftButtonDown += (s, e) => Tile_MouseDown(t, e);
                view.MouseMove += (s, e) => Tile_MouseMove(t, e);
                view.MouseLeftButtonUp += (s, e) => Tile_MouseUp(t, e);
                t.View = view;
                LayoutCanvas.Children.Add(view);
            }
            Fit();
            PositionViews();
            UpdateSelection();

            static TextBlock Small(string s) => new() { Text = s, FontSize = 10, Foreground = Brushes.White, Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center };
        }

        // Scales the whole arrangement to fit the canvas, centered.
        private void Fit()
        {
            if (_tiles.Count == 0) return;
            double cw = LayoutCanvas.ActualWidth > 0 ? LayoutCanvas.ActualWidth : 526;
            double ch = LayoutCanvas.ActualHeight > 0 ? LayoutCanvas.ActualHeight : 258;
            double minX = _tiles.Min(t => t.X), minY = _tiles.Min(t => t.Y);
            double bw = _tiles.Max(t => t.X + t.W) - minX, bh = _tiles.Max(t => t.Y + t.H) - minY;
            _scale = Math.Min((cw - 48) / bw, (ch - 48) / bh);
            _originX = minX - (cw / _scale - bw) / 2;
            _originY = minY - (ch / _scale - bh) / 2;
        }

        private void PositionViews()
        {
            foreach (var t in _tiles)
            {
                Canvas.SetLeft(t.View, (t.X - _originX) * _scale + 1);
                Canvas.SetTop(t.View, (t.Y - _originY) * _scale + 1);
                t.View.Width = Math.Max(8, t.W * _scale - 2);   // small gap so touching monitors stay distinct
                t.View.Height = Math.Max(8, t.H * _scale - 2);
            }
        }

        private void UpdateSelection()
        {
            foreach (var t in _tiles)
            {
                bool sel = t == _selected;
                t.View.Background = sel ? TileSelectedBrush : t.Connected ? TileBrush : TileOfflineBrush;
                t.View.BorderBrush = sel ? TileStrokeBrush : TileDimStrokeBrush;
                t.View.BorderThickness = new Thickness(sel ? 2 : 1);
            }

            _updatingChips = true;
            foreach (var chip in new[] { Orient0, Orient1, Orient2, Orient3 })
            {
                chip.IsEnabled = _selected != null;
                chip.IsChecked = _selected != null && int.Parse((string)chip.Tag) == _selected.Orientation;
            }
            _updatingChips = false;

            SelectedText.Text = _selected == null ? "" : $"Monitor {_selected.E.Number}";
            UpdateStatus();
        }

        private void UpdateStatus(string? message = null)
        {
            ClearButton.IsEnabled = _hasSaved;
            if (message != null) { StatusText.Text = message; return; }

            if (!_hasSaved)
                StatusText.Text = $"Profile {_profile} has no saved layout yet. This is the current arrangement; arrange it and click Save.";
            else
                StatusText.Text = (HasChanges ? "Unsaved changes. " : "") +
                    $"Profile {_profile}'s saved layout is restored whenever you apply the profile.";
        }

        // --- Dragging ---

        private Point ToDesktop(Point canvasPoint) =>
            new(canvasPoint.X / _scale + _originX, canvasPoint.Y / _scale + _originY);

        private void Tile_MouseDown(Tile t, MouseButtonEventArgs e)
        {
            _selected = t;
            UpdateSelection();

            var p = ToDesktop(e.GetPosition(LayoutCanvas));
            _dragOffset = new Point(p.X - t.X, p.Y - t.Y);
            _dragging = t;
            t.View.CaptureMouse();
            e.Handled = true;
        }

        private void Tile_MouseMove(Tile t, MouseEventArgs e)
        {
            if (_dragging != t || !t.View.IsMouseCaptured) return;
            var p = ToDesktop(e.GetPosition(LayoutCanvas));
            t.X = p.X - _dragOffset.X;
            t.Y = p.Y - _dragOffset.Y;
            PositionViews();
        }

        private void Tile_MouseUp(Tile t, MouseButtonEventArgs e)
        {
            if (_dragging != t) return;
            _dragging = null;
            t.View.ReleaseMouseCapture();

            Snap(t);
            Normalize();
            Fit();
            PositionViews();
            UpdateStatus();
        }

        // --- Controls ---

        private void Orientation_Checked(object sender, RoutedEventArgs e)
        {
            if (_updatingChips || _selected == null || sender is not RadioButton { Tag: string tag }) return;
            int o = int.Parse(tag);
            if (o == _selected.Orientation) return;

            if (o % 2 != _selected.Orientation % 2)
                (_selected.W, _selected.H) = (_selected.H, _selected.W);
            _selected.Orientation = o;

            Snap(_selected);
            Normalize();
            BuildViews(); // the size label changed
        }

        private void Profile_Checked(object sender, RoutedEventArgs e)
        {
            if (_updatingChips || !IsLoaded || sender is not RadioButton { Tag: string tag }) return;
            _profile = int.Parse(tag);
            _selected = null;
            LoadProfile();
        }

        private void Reset_Click(object sender, RoutedEventArgs e) => LoadProfile();

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _layouts.Clear(_profile);
            _log($"Layout: forgot the saved layout for profile {_profile}");
            LoadProfile();
            UpdateStatus($"Forgot Profile {_profile}'s layout. Applying it no longer moves or rotates monitors.");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Save();
            UpdateStatus($"Saved. Profile {_profile}'s layout is restored the next time you apply it.");
        }

        private void Save()
        {
            var entries = CurrentEntries();
            _layouts.Save(_profile, entries);

            // The layout sets each monitor's rotation, so the after-Apply orientation prompt
            // shouldn't ask about these monitors for this profile.
            foreach (var e in entries)
                _orientation.Confirm(_profile, new MonitorHelper.MonitorInfo { Key = e.Key, Orientation = e.Orientation, Width = e.Width, Height = e.Height });

            _log($"Layout: saved to profile {_profile}: " +
                 string.Join("; ", entries.Select(e => $"monitor {e.Number} at {e.X},{e.Y} {e.Width}x{e.Height} orientation {e.Orientation}")));
            LoadProfile();
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            Save();

            var live = MonitorHelper.GetAllMonitors();
            var targets = LayoutStore.Plan(_layouts.Get(_profile)!, live, out string reason);
            if (targets == null)
            {
                UpdateStatus($"Saved, but not applied now: {reason}. Apply Profile {_profile} first.");
                return;
            }
            if (reason == "already matches")
            {
                UpdateStatus("Saved. The monitors already match this layout.");
                return;
            }

            var previous = live.Select(m => new MonitorHelper.LayoutTarget(m.DeviceName, m.PositionX, m.PositionY, m.Orientation)).ToList();

            SetBusy(true);
            try
            {
                _log("Layout: applying " + string.Join("; ", targets.Select(t => $"{t.DeviceName} at {t.X},{t.Y} orientation {t.Orientation}")));
                string? error = MonitorHelper.ApplyLayout(targets);
                if (error != null)
                {
                    _log($"Layout: apply failed: {error}");
                    MonitorHelper.ApplyLayout(previous);
                    UpdateStatus($"Saved, but Windows didn't accept the layout right now ({error}).");
                    return;
                }

                await Task.Delay(1200); // let Windows re-lay out the desktop

                var primary = MonitorHelper.GetAllMonitors().FirstOrDefault(m => m.IsPrimary);
                bool kept = false;
                if (primary != null)
                {
                    double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
                    var b = primary.Bounds;
                    var confirm = new OrientationConfirmWindow(new Rect(b.X / dpi, b.Y / dpi, b.Width / dpi, b.Height / dpi), "Keep this layout?");
                    confirm.ShowDialog();
                    kept = confirm.Kept;
                }

                if (kept)
                {
                    UpdateStatus($"Saved and applied. Profile {_profile}'s layout is restored whenever you apply it.");
                }
                else
                {
                    _log("Layout: reverted the screens (the saved layout is kept)");
                    MonitorHelper.ApplyLayout(previous);
                    await Task.Delay(800);
                    UpdateStatus("Screens reverted. The layout is still saved to the profile; edit it or click Forget.");
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            ApplyButton.IsEnabled = SaveButton.IsEnabled = ResetButton.IsEnabled = !busy;
            LayoutCanvas.IsHitTestVisible = !busy;
        }
    }
}
