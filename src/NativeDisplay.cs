using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VRMogger
{
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DisplayDevice
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }
        // The display arm of DEVMODEW's union, followed by its common fields.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            public short SpecVersion, DriverVersion, Size, DriverExtra;
            public int Fields, PositionX, PositionY, Orientation, FixedOutput;
            public short Color, Duplex, YResolution, TTOption, Collate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
            public short LogPixels;
            public int BitsPerPel, Width, Height, DisplayFlags, Frequency;
            public int ICMMethod, ICMIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayDevices(string name, uint index, ref DisplayDevice device, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsExW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplaySettingsEx(string name, int index, ref DevMode mode, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "ChangeDisplaySettingsExW")]
        internal static extern int ChangeDisplaySettingsEx(string name, ref DevMode mode, IntPtr window, uint flags, IntPtr parameter);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr window, int id);
    }

    public sealed class NativeDisplayService : IDisplayService
    {
        public static int DevModeSize { get { return Marshal.SizeOf(typeof(NativeMethods.DevMode)); } }
        public static int DisplayDeviceSize { get { return Marshal.SizeOf(typeof(NativeMethods.DisplayDevice)); } }
        private static NativeMethods.DevMode EmptyMode()
        { return new NativeMethods.DevMode { Size = (short)DevModeSize }; }
        private static NativeMethods.DisplayDevice EmptyDevice()
        { return new NativeMethods.DisplayDevice { cb = DisplayDeviceSize }; }

        public IList<DisplayTarget> GetDisplays()
        {
            List<DisplayTarget> result = new List<DisplayTarget>();
            for (uint i = 0; ; i++)
            {
                NativeMethods.DisplayDevice adapter = EmptyDevice();
                if (!NativeMethods.EnumDisplayDevices(null, i, ref adapter, 0)) break;
                if ((adapter.StateFlags & 1) == 0 || (adapter.StateFlags & 8) != 0) continue;
                string identity = null, label = null;
                for (uint j = 0; ; j++)
                {
                    NativeMethods.DisplayDevice monitor = EmptyDevice();
                    if (!NativeMethods.EnumDisplayDevices(adapter.DeviceName, j, ref monitor, 1)) break;
                    if ((monitor.StateFlags & 1) == 0) continue;
                    if (!string.IsNullOrWhiteSpace(monitor.DeviceID)) identity = "monitor:" + monitor.DeviceID;
                    label = monitor.DeviceString;
                    break;
                }
                // Some virtual display drivers expose no monitor interface. Keep their adapter
                // identity, never silently fall back to a different/primary monitor.
                if (string.IsNullOrEmpty(identity)) identity = "adapter:" + adapter.DeviceID + "|" + adapter.DeviceKey + "|" + adapter.DeviceName;
                if (string.IsNullOrEmpty(label)) label = adapter.DeviceString;
                result.Add(new DisplayTarget(identity, adapter.DeviceName, label, (adapter.StateFlags & 4) != 0));
            }
            return result;
        }

        private DisplayTarget Find(string id)
        {
            foreach (DisplayTarget target in GetDisplays())
                if (string.Equals(target.Id, id, StringComparison.OrdinalIgnoreCase)) return target;
            return null;
        }
        public IList<DisplayMode> GetModes(string displayId)
        {
            List<DisplayMode> modes = new List<DisplayMode>();
            DisplayTarget display = Find(displayId);
            if (display == null) return modes;
            for (int i = 0; ; i++)
            {
                NativeMethods.DevMode mode = EmptyMode();
                if (!NativeMethods.EnumDisplaySettingsEx(display.DeviceName, i, ref mode, 0)) break;
                if (mode.BitsPerPel < 24 || mode.Width < 1 || mode.Height < 1) continue;
                DisplayMode entry = new DisplayMode(mode.Width, mode.Height, mode.Frequency);
                if (!modes.Contains(entry)) modes.Add(entry);
            }
            DisplayMode current; string error;
            if (TryGetCurrent(displayId, out current, out error) && !modes.Contains(current)) modes.Add(current);
            modes.Sort(delegate(DisplayMode a, DisplayMode b) {
                int width = b.Width.CompareTo(a.Width); if (width != 0) return width;
                int height = b.Height.CompareTo(a.Height); return height != 0 ? height : b.Refresh.CompareTo(a.Refresh);
            });
            return modes;
        }

        public bool TryGetCurrent(string displayId, out DisplayMode mode, out string error)
        {
            mode = null; error = null;
            DisplayTarget target = Find(displayId);
            if (target == null) { error = "The selected monitor is disconnected or its identity changed. Reconnect it or choose a monitor in Settings."; return false; }
            NativeMethods.DevMode native = EmptyMode();
            if (!NativeMethods.EnumDisplaySettingsEx(target.DeviceName, -1, ref native, 0))
            { error = "Windows could not read the selected monitor's current mode."; return false; }
            mode = new DisplayMode(native.Width, native.Height, native.Frequency);
            return true;
        }

        public bool TryApply(string displayId, DisplayMode requested, out string error)
        {
            error = null;
            DisplayTarget target = Find(displayId);
            if (target == null) { error = "The selected monitor is disconnected. No other monitor was changed."; return false; }
            NativeMethods.DevMode native = EmptyMode();
            if (!NativeMethods.EnumDisplaySettingsEx(target.DeviceName, -1, ref native, 0))
            { error = "Could not capture the selected monitor's mode."; return false; }
            // Width, height and refresh only. No topology, primary-display, registry or unsafe-mode flags.
            native.Fields = 0x00080000 | 0x00100000 | 0x00400000;
            native.Width = requested.Width; native.Height = requested.Height; native.Frequency = requested.Refresh;
            int test = NativeMethods.ChangeDisplaySettingsEx(target.DeviceName, ref native, IntPtr.Zero, 2, IntPtr.Zero);
            if (test != 0) { error = "Windows rejected " + requested + " during validation (code " + test + ")."; return false; }
            int applied = NativeMethods.ChangeDisplaySettingsEx(target.DeviceName, ref native, IntPtr.Zero, 0, IntPtr.Zero);
            if (applied != 0) { error = "The display driver could not apply " + requested + " (code " + applied + ")."; return false; }
            DisplayMode actual;
            if (!TryGetCurrent(displayId, out actual, out error)) return false;
            // Drivers may report 'default refresh' as the actual refresh instead of 0/1.
            if (actual.Width != requested.Width || actual.Height != requested.Height ||
                (requested.Refresh > 1 && actual.Refresh != requested.Refresh))
            { error = "The driver reported success but the requested mode was not applied. The app will restore the captured mode."; return false; }
            return true;
        }
    }
}
