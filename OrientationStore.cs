using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MMH
{
    /// <summary>
    /// Remembers the last orientation the user confirmed for each monitor, so the
    /// orientation prompt only appears when something looks different.
    /// </summary>
    public class OrientationStore
    {
        public class Record
        {
            public int Orientation { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private readonly string _path;
        private Dictionary<string, Record> _records = new();

        public OrientationStore(string path)
        {
            _path = path;
            try
            {
                if (File.Exists(_path))
                    _records = JsonSerializer.Deserialize<Dictionary<string, Record>>(File.ReadAllText(_path)) ?? new();
            }
            catch
            {
                _records = new();
            }
        }

        /// <summary>
        /// Returns a reason the monitor needs checking, or null if it matches what was last confirmed.
        /// </summary>
        public string? GetChangeReason(MonitorHelper.MonitorInfo m)
        {
            if (!_records.TryGetValue(m.Key, out var r))
                return "no confirmed orientation saved yet";
            if (r.Orientation != m.Orientation)
                return $"orientation changed {r.Orientation} -> {m.Orientation}";
            if ((r.Width > r.Height) != (m.Width > m.Height))
                return $"aspect changed {r.Width}x{r.Height} -> {m.Width}x{m.Height}";
            return null;
        }

        public void Confirm(MonitorHelper.MonitorInfo m)
        {
            _records[m.Key] = new Record { Orientation = m.Orientation, Width = m.Width, Height = m.Height };
            try
            {
                File.WriteAllText(_path,
                    JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true }),
                    Encoding.UTF8);
            }
            catch
            {
                // Not fatal: worst case the user is asked again next time.
            }
        }
    }
}
