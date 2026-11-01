// Copyright (c) 2026 Kelly Label Studio x MK2XX2. MIT licensed.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace VRMogger
{
    public sealed class DisplayMode : IEquatable<DisplayMode>
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Refresh { get; private set; }
        public DisplayMode(int width, int height, int refresh)
        {
            if (width < 1 || width > 32768 || height < 1 || height > 32768 || refresh < 0 || refresh > 2000)
                throw new ArgumentOutOfRangeException("width", "Invalid display mode.");
            Width = width; Height = height; Refresh = refresh;
        }
        public bool Equals(DisplayMode other)
        { return other != null && Width == other.Width && Height == other.Height && Refresh == other.Refresh; }
        public override bool Equals(object value) { return Equals(value as DisplayMode); }
        public override int GetHashCode() { return (Width * 397 ^ Height) * 397 ^ Refresh; }
        public override string ToString()
        { return Width + " x " + Height + " @ " + (Refresh > 1 ? Refresh + " Hz" : "default refresh"); }
        public string Serialize()
        { return string.Join(",", new[] { Width.ToString(CultureInfo.InvariantCulture), Height.ToString(CultureInfo.InvariantCulture), Refresh.ToString(CultureInfo.InvariantCulture) }); }
        public static DisplayMode Parse(string text)
        {
            string[] p = text.Split(',');
            if (p.Length != 3) throw new FormatException("Invalid saved display mode.");
            return new DisplayMode(int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture));
        }
    }

    public sealed class DisplayTarget
    {
        public string Id { get; private set; }
        public string DeviceName { get; private set; }
        public string Name { get; private set; }
        public bool IsPrimary { get; private set; }
        public DisplayTarget(string id, string deviceName, string name, bool primary)
        { Id = id; DeviceName = deviceName; Name = name; IsPrimary = primary; }
        public override string ToString() { return DeviceName.Replace(@"\\.\", "") + " - " + Name + (IsPrimary ? " (primary)" : ""); }
    }

    public sealed class Settings
    {
        public string DisplayId = "";
        public List<string> Triggers = new List<string>();
        public DisplayMode GameMode = new DisplayMode(1280, 720, 60);
        public DisplayMode DesktopMode = new DisplayMode(1920, 1080, 60);
        public bool RestoreCapturedMode = true;
        public int DelaySeconds = 10;
        public bool SteamVROnly;
        public bool Autostart;
        public bool StartMinimized = true;
        public bool DebugLogging;
        public Settings Copy()
        {
            Settings copy = (Settings)MemberwiseClone();
            copy.Triggers = new List<string>(Triggers);
            return copy;
        }
        public static string NormalizeProcess(string input)
        {
            string name = System.IO.Path.GetFileName(input.Trim().Trim('"'));
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '|', '\r', '\n', '\\', '/' }) >= 0)
                throw new FormatException("Enter an executable name, such as VRChat.exe.");
            return name + ".exe";
        }
    }

    public sealed class RecoveryRecord
    {
        public string DisplayId;
        public DisplayMode OriginalMode;
        public DisplayMode RestoreMode;
        public RecoveryRecord(string displayId, DisplayMode original, DisplayMode restore)
        { DisplayId = displayId; OriginalMode = original; RestoreMode = restore; }
    }

    public interface IDisplayService
    {
        IList<DisplayTarget> GetDisplays();
        IList<DisplayMode> GetModes(string displayId);
        bool TryGetCurrent(string displayId, out DisplayMode mode, out string error);
        bool TryApply(string displayId, DisplayMode mode, out string error);
    }

    public interface IRecoveryStore
    {
        RecoveryRecord Load();
        void Save(RecoveryRecord record);
        void Clear();
    }
}
