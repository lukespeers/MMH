using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MMH
{
    /// <summary>
    /// Remembers the last orientation the user confirmed for each monitor, separately for
    /// each profile, so the orientation prompt only appears when a monitor differs from what
    /// that profile expects.
    /// </summary>
    public class OrientationStore
    {
        public class Record
        {
            public int Orientation { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private class FileData
        {
            /// <summary>Profile applied most recently; used by the manual Check Orientation button.</summary>
            public int? LastProfile { get; set; }

            /// <summary>Key: "Profile{n}|{monitor key}".</summary>
            public Dictionary<string, Record> Records { get; set; } = new();
        }

        private readonly string _path;
        private FileData _data = new();

        public OrientationStore(string path)
        {
            _path = path;
            try
            {
                if (File.Exists(_path))
                {
                    // Files from the earlier per-monitor format (a plain dictionary) don't
                    // deserialize into this shape; they're treated as empty and each profile
                    // asks once.
                    var loaded = JsonSerializer.Deserialize<FileData>(File.ReadAllText(_path));
                    if (loaded?.Records != null)
                        _data = loaded;
                }
            }
            catch
            {
                _data = new();
            }
        }

        public int? LastProfile
        {
            get => _data.LastProfile;
            set { _data.LastProfile = value; Save(); }
        }

        private static string Key(int profile, MonitorHelper.MonitorInfo m) => $"Profile{profile}|{m.Key}";

        /// <summary>
        /// Returns a reason the monitor needs checking for this profile, or null if it matches
        /// what was last confirmed for this profile.
        /// </summary>
        public string? GetChangeReason(int profile, MonitorHelper.MonitorInfo m)
        {
            if (!_data.Records.TryGetValue(Key(profile, m), out var r))
                return $"not confirmed yet for profile {profile}";
            if (r.Orientation != m.Orientation)
                return $"orientation {m.Orientation} differs from profile {profile}'s {r.Orientation}";
            if ((r.Width > r.Height) != (m.Width > m.Height))
                return $"aspect {m.Width}x{m.Height} differs from profile {profile}'s {r.Width}x{r.Height}";
            return null;
        }

        public void Confirm(int profile, MonitorHelper.MonitorInfo m)
        {
            _data.Records[Key(profile, m)] = new Record { Orientation = m.Orientation, Width = m.Width, Height = m.Height };
            Save();
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(_path,
                    JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }),
                    Encoding.UTF8);
            }
            catch
            {
                // Not fatal: worst case the user is asked again next time.
            }
        }
    }
}
