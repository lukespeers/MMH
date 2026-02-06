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
    }

    public static List<MonitorInfo> GetAllMonitors()
    {
        var result = new List<MonitorInfo>();
        int index = 0;
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
            {
                var bounds = new Rect(lprcMonitor.Left, lprcMonitor.Top, lprcMonitor.Right - lprcMonitor.Left, lprcMonitor.Bottom - lprcMonitor.Top);
                result.Add(new MonitorInfo { Index = ++index, Bounds = bounds });
                return true;
            }, IntPtr.Zero);
        return result;
    }

    private static class NativeMethods
    {
        public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}