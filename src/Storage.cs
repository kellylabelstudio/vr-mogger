using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VRMogger
{
    public static class AtomicFile
    {
        public static void Write(string path, string contents)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temp = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                { writer.Write(contents); writer.Flush(); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static Dictionary<string, string> Read(string path)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim(); int split = line.IndexOf('=');
                if (split > 0 && !line.StartsWith("#", StringComparison.Ordinal)) values[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
            }
            return values;
        }
        public static string Encode(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); }
        public static string Decode(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
    }

    public sealed class SettingsStore
    {
        public string DirectoryPath { get; private set; }
        public string ConfigPath { get { return Path.Combine(DirectoryPath, "config.ini"); } }
        public SettingsStore(string path) { DirectoryPath = path; }
        public Settings Load()
        {
            Settings settings = new Settings();
            if (!File.Exists(ConfigPath)) return settings;
            Dictionary<string, string> v = AtomicFile.Read(ConfigPath);
            if (v.ContainsKey("DisplayId64")) settings.DisplayId = AtomicFile.Decode(v["DisplayId64"]);
            settings.GameMode = ReadMode(v, "Mog", settings.GameMode);
            settings.DesktopMode = ReadMode(v, "Desk", settings.DesktopMode);
            settings.RestoreCapturedMode = ReadBool(v, "RestoreCapturedMode", true);
            settings.DelaySeconds = Math.Max(0, Math.Min(300, ReadInt(v, "Delay", 10)));
            settings.SteamVROnly = ReadBool(v, "SteamVROnly", false);
            settings.Autostart = ReadBool(v, "Autostart", false);
            settings.StartMinimized = ReadBool(v, "StartMinimized", true);
            settings.DebugLogging = ReadBool(v, "DebugLog", false);
            if (v.ContainsKey("Triggers"))
                foreach (string raw in v["Triggers"].Split('|'))
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    string name = Settings.NormalizeProcess(raw);
                    if (!settings.Triggers.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) settings.Triggers.Add(name);
                }
            return settings;
        }
        public void Save(Settings settings)
        {
            if (settings.DelaySeconds < 0 || settings.DelaySeconds > 300) throw new ArgumentException("Restore delay must be 0 to 300 seconds.");
            StringBuilder b = new StringBuilder("Version=2\n");
            b.AppendLine("DisplayId64=" + AtomicFile.Encode(settings.DisplayId));
            WriteMode(b, "Mog", settings.GameMode); WriteMode(b, "Desk", settings.DesktopMode);
            b.AppendLine("RestoreCapturedMode=" + settings.RestoreCapturedMode);
            b.AppendLine("Delay=" + settings.DelaySeconds.ToString(CultureInfo.InvariantCulture));
            b.AppendLine("SteamVROnly=" + settings.SteamVROnly);
            b.AppendLine("Autostart=" + settings.Autostart);
            b.AppendLine("StartMinimized=" + settings.StartMinimized);
            b.AppendLine("DebugLog=" + settings.DebugLogging);
            List<string> triggers = new List<string>();
            foreach (string raw in settings.Triggers)
            { string name = Settings.NormalizeProcess(raw); if (!triggers.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) triggers.Add(name); }
            b.AppendLine("Triggers=" + string.Join("|", triggers.ToArray()));
            AtomicFile.Write(ConfigPath, b.ToString());
        }
        private static int ReadInt(Dictionary<string, string> v, string key, int fallback)
        {
            string text; int value;
            if (!v.TryGetValue(key, out text)) return fallback;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) throw new FormatException("Invalid setting: " + key);
            return value;
        }
        private static bool ReadBool(Dictionary<string, string> v, string key, bool fallback)
        {
            string text; bool value;
            if (!v.TryGetValue(key, out text)) return fallback;
            if (text == "1") return true; if (text == "0") return false;
            if (bool.TryParse(text, out value)) return value;
            throw new FormatException("Invalid setting: " + key);
        }
        private static DisplayMode ReadMode(Dictionary<string, string> v, string prefix, DisplayMode fallback)
        { return new DisplayMode(ReadInt(v, prefix + "Width", fallback.Width), ReadInt(v, prefix + "Height", fallback.Height), ReadInt(v, prefix + "Hz", fallback.Refresh)); }
        private static void WriteMode(StringBuilder b, string prefix, DisplayMode mode)
        { b.AppendLine(prefix + "Width=" + mode.Width); b.AppendLine(prefix + "Height=" + mode.Height); b.AppendLine(prefix + "Hz=" + mode.Refresh); }
        public void Log(Settings settings, string message)
        {
            if (!settings.DebugLogging) return;
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = Path.Combine(DirectoryPath, "debug.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Delete(path);
                File.AppendAllText(path, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine);
            }
            catch { /* Diagnostics must never interfere with restoring the display. */ }
        }
    }

    public sealed class FileRecoveryStore : IRecoveryStore
    {
        public string PathName { get; private set; }
        public FileRecoveryStore(string path) { PathName = path; }
        public RecoveryRecord Load()
        {
            if (!File.Exists(PathName)) return null;
            Dictionary<string, string> values = AtomicFile.Read(PathName);
            if (!values.ContainsKey("Version") || values["Version"] != "1") throw new FormatException("Unrecognized recovery record.");
            string id = AtomicFile.Decode(values["DisplayId64"]);
            if (string.IsNullOrWhiteSpace(id)) throw new FormatException("Missing recovery display identity.");
            return new RecoveryRecord(id, DisplayMode.Parse(values["Original"]), DisplayMode.Parse(values["Restore"]));
        }
        public void Save(RecoveryRecord record)
        {
            AtomicFile.Write(PathName, "Version=1\nDisplayId64=" + AtomicFile.Encode(record.DisplayId) +
                "\nOriginal=" + record.OriginalMode.Serialize() + "\nRestore=" + record.RestoreMode.Serialize() + "\n");
        }
        public void Clear() { File.Delete(PathName); }
    }
}
