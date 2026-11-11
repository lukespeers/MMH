using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

public static class MonitorHelper
{
    public class MonitorInfo
    {
        public int Index { get; set; }
        public Rect Bounds { get; set; }

        /// <summary>GDI device name, e.g. \\.\DISPLAY1. Used to change settings.</summary>
        public string DeviceName { get; set; } = "";

        /// <summary>Stable-ish id of the physical monitor (falls back to DeviceName).</summary>
        public string Key { get; set; } = "";

        /// <summary>Windows DMDO value: 0 = landscape, 1 = portrait, 2 = landscape flipped, 3 = portrait flipped.</summary>
        public int Orientation { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }

        public bool IsPrimary { get; set; }
    }

    public static List<MonitorInfo> GetAllMonitors()
    {
        var result = new List<MonitorInfo>();
        int index = 0;
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
            {
                var bounds = new Rect(lprcMonitor.Left, lprcMonitor.Top, lprcMonitor.Right - lprcMonitor.Left, lprcMonitor.Bottom - lprcMonitor.Top);
                var info = new MonitorInfo { Index = ++index, Bounds = bounds };

                var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    info.DeviceName = mi.szDevice;
                    info.IsPrimary = (mi.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY
                    info.Key = GetMonitorDeviceId(mi.szDevice) ?? mi.szDevice;

                    var dm = NewDevMode();
                    if (NativeMethods.EnumDisplaySettings(mi.szDevice, NativeMethods.ENUM_CURRENT_SETTINGS, ref dm))
                    {
                        info.Orientation = dm.dmDisplayOrientation;
                        info.Width = dm.dmPelsWidth;
                        info.Height = dm.dmPelsHeight;
                    }
                }

                result.Add(info);
                return true;
            }, IntPtr.Zero);
        return result;
    }

    /// <summary>
    /// Sets a display's orientation (DMDO 0-3), swapping width/height when switching
    /// between landscape and portrait. Returns null on success, or an error message.
    /// No admin rights needed.
    /// </summary>
    public static string? SetOrientation(string deviceName, int orientation)
    {
        var dm = NewDevMode();
        if (!NativeMethods.EnumDisplaySettings(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref dm))
            return $"Could not read current settings for {deviceName}.";

        orientation = ((orientation % 4) + 4) % 4;
        if (dm.dmDisplayOrientation == orientation)
            return null;

        bool wasPortrait = dm.dmDisplayOrientation % 2 == 1;
        bool willBePortrait = orientation % 2 == 1;
        if (wasPortrait != willBePortrait)
            (dm.dmPelsWidth, dm.dmPelsHeight) = (dm.dmPelsHeight, dm.dmPelsWidth);

        dm.dmDisplayOrientation = orientation;
        dm.dmFields = NativeMethods.DM_DISPLAYORIENTATION | NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT;

        int rc = NativeMethods.ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, NativeMethods.CDS_UPDATEREGISTRY, IntPtr.Zero);
        return rc == NativeMethods.DISP_CHANGE_SUCCESSFUL ? null : $"ChangeDisplaySettingsEx returned {rc} for {deviceName}.";
    }

    private static string? GetMonitorDeviceId(string adapterDeviceName)
    {
        var dd = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
        if (NativeMethods.EnumDisplayDevices(adapterDeviceName, 0, ref dd, 0) && !string.IsNullOrEmpty(dd.DeviceID))
            return dd.DeviceID;
        return null;
    }

    private static NativeMethods.DEVMODE NewDevMode()
    {
        var dm = new NativeMethods.DEVMODE
        {
            dmDeviceName = new string('\0', 32),
            dmFormName = new string('\0', 32),
        };
        dm.dmSize = (short)Marshal.SizeOf<NativeMethods.DEVMODE>();
        return dm;
    }

    private static class NativeMethods
    {
        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int DM_DISPLAYORIENTATION = 0x00000080;
        public const int DM_PELSWIDTH = 0x00080000;
        public const int DM_PELSHEIGHT = 0x00100000;
        public const uint CDS_UPDATEREGISTRY = 0x00000001;
        public const int DISP_CHANGE_SUCCESSFUL = 0;

        public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }
    }
}
