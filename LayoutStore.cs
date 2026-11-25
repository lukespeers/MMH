using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MMH
{
    /// <summary>
    /// The monitor layout (position and rotation of each monitor) saved for each profile,
    /// in layouts.json next to the exe. Monitors are identified by their device id
    /// (MonitorInfo.Key), the same key the orientation store uses.
    /// </summary>
    public class LayoutStore
    {
        public class Entry
        {
            public string Key { get; set; } = "";
            public int X { get; set; }
            public int Y { get; set; }
            public int Orientation { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsPrimary { get; set; }

            /// <summary>Monitor number when it was saved (as Identify Monitors shows it), for labels only.</summary>
            public int Number { get; set; }
        }

        private readonly string _path;
        private Dictionary<string, List<Entry>> _data = new();

        public LayoutStore(string path)
        {
            _path = path;
            try
            {
                if (File.Exists(_path))
                    _data = JsonSerializer.Deserialize<Dictionary<string, List<Entry>>>(File.ReadAllText(_path)) ?? new();
            }
            catch
            {
                _data = new();
            }
        }

        private static string Key(int profile) => $"Profile{profile}";

        public List<Entry>? Get(int profile) =>
            _data.TryGetValue(Key(profile), out var list) && list.Count > 0 ? list : null;

        public void Save(int profile, IEnumerable<Entry> entries)
        {
            _data[Key(profile)] = entries.ToList();
            Write();
        }

        public static Entry FromMonitor(MonitorHelper.MonitorInfo m) => new()
        {
            Key = m.Key,
            X = m.PositionX,
            Y = m.PositionY,
            Orientation = m.Orientation,
            Width = m.Width,
            Height = m.Height,
            IsPrimary = m.IsPrimary,
            Number = m.Index,
        };

        public void Clear(int profile)
        {
            if (_data.Remove(Key(profile))) Write();
        }

        private void Write()
        {
            try
            {
                File.WriteAllText(_path,
                    JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }),
                    Encoding.UTF8);
            }
            catch
            {
                // Not fatal: the layout just isn't remembered.
            }
        }

        /// <summary>
        /// Works out where the monitors that are on now should go for a saved layout.
        /// Positions are shifted so the current primary stays at 0,0. Returns null (with a reason)
        /// when the layout can't be used, e.g. the current primary isn't part of it.
        /// </summary>
        public static List<MonitorHelper.LayoutTarget>? Plan(List<Entry> layout, List<MonitorHelper.MonitorInfo> live, out string reason)
        {
            var byKey = layout.ToDictionary(e => e.Key, e => e);
            var primary = live.FirstOrDefault(m => m.IsPrimary);
            if (primary == null || !byKey.TryGetValue(primary.Key, out var p))
            {
                reason = "the current primary monitor isn't in the saved layout";
                return null;
            }

            var targets = live.Where(m => byKey.ContainsKey(m.Key))
                .Select(m =>
                {
                    var e = byKey[m.Key];
                    return new MonitorHelper.LayoutTarget(m.DeviceName, e.X - p.X, e.Y - p.Y, e.Orientation);
                })
                .ToList();

            bool alreadyThere = targets.All(t =>
            {
                var m = live.First(x => x.DeviceName == t.DeviceName);
                return m.PositionX == t.X && m.PositionY == t.Y && m.Orientation == t.Orientation;
            });
            reason = alreadyThere ? "already matches" : "";
            return targets;
        }
    }
}
