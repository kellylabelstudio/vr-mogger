using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VRMogger;

internal static class SessionTests
{
    private static int passed, failed;
    private static readonly DisplayMode Desktop = new DisplayMode(1920, 1080, 165);
    private static readonly DisplayMode Game = new DisplayMode(1280, 720, 60);
    private sealed class FakeDisplay : IDisplayService
    {
        public Dictionary<string, DisplayMode> Current = new Dictionary<string, DisplayMode> { { "laptop", Desktop }, { "external", new DisplayMode(2560, 1440, 144) } };
        public List<string> Writes = new List<string>();
        public bool FailApply, PartlyApply, FailRestore;
        public IList<DisplayTarget> GetDisplays()
        {
            List<DisplayTarget> result = new List<DisplayTarget>();
            foreach (string id in Current.Keys) result.Add(new DisplayTarget(id, id == "laptop" ? "DISPLAY1" : "DISPLAY2", id, id == "laptop"));
            return result;
        }
        public IList<DisplayMode> GetModes(string id)
        { return Current.ContainsKey(id) ? new List<DisplayMode> { Desktop, Game, new DisplayMode(2560, 1440, 144) } : new List<DisplayMode>(); }
        public bool TryGetCurrent(string id, out DisplayMode mode, out string error)
        { error = null; if (Current.TryGetValue(id, out mode)) return true; error = "Monitor disconnected"; return false; }
        public bool TryApply(string id, DisplayMode mode, out string error)
        {
            error = null;
            if (!Current.ContainsKey(id)) { error = "Monitor disconnected"; return false; }
            Writes.Add(id + ":" + mode.Serialize());
            if ((FailApply && mode.Equals(Game)) || (FailRestore && !mode.Equals(Game)))
            { if (PartlyApply) Current[id] = mode; error = "Driver refused mode"; return false; }
            Current[id] = mode; return true;
        }
    }
    private sealed class MemoryRecovery : IRecoveryStore
    {
        public RecoveryRecord Record;
        public bool FailSave, FailClear, FailLoad;
        public int Saves;
        public RecoveryRecord Load() { if (FailLoad) throw new IOException("Corrupt record"); return Record; }
        public void Save(RecoveryRecord value) { if (FailSave) throw new IOException("Disk full"); Saves++; Record = new RecoveryRecord(value.DisplayId, value.OriginalMode, value.RestoreMode); }
        public void Clear() { if (FailClear) throw new IOException("Access denied"); Record = null; }
    }
    private sealed class Fixture
    {
        public FakeDisplay Displays = new FakeDisplay();
        public MemoryRecovery Recovery = new MemoryRecovery();
        public Settings Settings = new Settings { DisplayId = "laptop", GameMode = Game };
        public SessionController Controller;
        public Fixture() { Controller = new SessionController(Displays, Recovery); }
        public void Enter() { Controller.Tick(true, Settings, 0); Assert(Controller.IsActive, "Expected active session"); }
        public void End(long when) { Controller.Tick(false, Settings, when); Controller.Tick(false, Settings, when + Settings.DelaySeconds * 1000L); }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Equal(object expected, object actual)
    { Assert(object.Equals(expected, actual), "Expected " + expected + ", got " + actual); }
    private static void Test(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    [STAThread]
    private static int Main()
    {
        Test("Idle never changes a display", delegate { Fixture f = new Fixture(); f.Controller.Tick(false, f.Settings, 0); Equal(0, f.Displays.Writes.Count); });
        Test("Selected external monitor only", delegate {
            Fixture f = new Fixture(); f.Settings.DisplayId = "external"; f.Enter();
            Equal(Desktop, f.Displays.Current["laptop"]); Equal(Game, f.Displays.Current["external"]);
            f.End(2000); Equal(new DisplayMode(2560, 1440, 144), f.Displays.Current["external"]);
        });
        Test("Capture actual mode at every session", delegate {
            Fixture f = new Fixture(); f.Enter(); f.End(2000);
            DisplayMode changed = new DisplayMode(1600, 900, 75); f.Displays.Current["laptop"] = changed;
            f.Controller.Tick(true, f.Settings, 30000); f.End(32000); Equal(changed, f.Displays.Current["laptop"]);
        });
        Test("Delay is respected", delegate {
            Fixture f = new Fixture(); f.Enter(); f.Controller.Tick(false, f.Settings, 2000);
            f.Controller.Tick(false, f.Settings, 11999); Assert(f.Controller.IsActive, "Restored too early");
            f.Controller.Tick(false, f.Settings, 12000); Assert(!f.Controller.HasSession, "Did not restore"); Equal(Desktop, f.Displays.Current["laptop"]);
        });
        Test("Zero restore delay", delegate { Fixture f = new Fixture(); f.Settings.DelaySeconds = 0; f.Enter(); f.Controller.Tick(false, f.Settings, 100); Assert(!f.Controller.HasSession, "Zero delay did not restore"); });
        Test("Game restarts during delay", delegate {
            Fixture f = new Fixture(); f.Enter(); f.Controller.Tick(false, f.Settings, 2000); f.Controller.Tick(true, f.Settings, 8000);
            f.Controller.Tick(true, f.Settings, 15000); Equal(1, f.Recovery.Saves); Assert(f.Controller.IsActive, "Session should still be active");
            f.End(16000); Equal(Desktop, f.Displays.Current["laptop"]);
        });
        Test("Another trigger keeps session active", delegate { Fixture f = new Fixture(); f.Enter(); f.Controller.Tick(true, f.Settings, 30000); Equal(1, f.Displays.Writes.Count); Assert(f.Controller.IsActive, "Another matching game should keep the mode"); });
        Test("Restore normal exit immediately", delegate { Fixture f = new Fixture(); f.Enter(); string e; Assert(f.Controller.TryRestore(1, out e), e); Equal(Desktop, f.Displays.Current["laptop"]); Assert(f.Recovery.Record == null, "Recovery not cleared"); });
        Test("Custom desktop preset is preserved", delegate { Fixture f = new Fixture(); f.Settings.RestoreCapturedMode = false; f.Settings.DesktopMode = new DisplayMode(2560, 1440, 144); f.Enter(); f.End(100); Equal(f.Settings.DesktopMode, f.Displays.Current["laptop"]); });
        Test("Unavailable custom restore mode blocks entry", delegate { Fixture f = new Fixture(); f.Settings.RestoreCapturedMode = false; f.Settings.DesktopMode = new DisplayMode(999, 777, 89); f.Controller.Tick(true, f.Settings, 0); Equal(0, f.Displays.Writes.Count); Assert(!f.Controller.IsActive, "Unsupported restore preset must not enter"); });
        Test("Settings changes cannot lose active session target", delegate { Fixture f = new Fixture(); f.Enter(); f.Settings.DisplayId = "external"; f.End(100); Equal(Desktop, f.Displays.Current["laptop"]); Equal(new DisplayMode(2560, 1440, 144), f.Displays.Current["external"]); });
        Test("Recovery must be saved before a mode change", delegate { Fixture f = new Fixture(); f.Recovery.FailSave = true; f.Controller.Tick(true, f.Settings, 0); Equal(0, f.Displays.Writes.Count); Assert(!f.Controller.HasSession, "Failed persistence should not own a session"); });
        Test("Failed apply does not mark active or retry tightly", delegate { Fixture f = new Fixture(); f.Displays.FailApply = true; f.Controller.Tick(true, f.Settings, 0); Assert(!f.Controller.IsActive, "Failed switch marked active"); Equal(Desktop, f.Displays.Current["laptop"]); f.Controller.Tick(true, f.Settings, 2000); Equal(1, f.Displays.Writes.Count); f.Controller.Tick(true, f.Settings, 10000); Equal(2, f.Displays.Writes.Count); });
        Test("Partly applied failed change rolls back", delegate { Fixture f = new Fixture(); f.Displays.FailApply = true; f.Displays.PartlyApply = true; f.Controller.Tick(true, f.Settings, 0); Equal(Desktop, f.Displays.Current["laptop"]); Assert(!f.Controller.HasSession, "Partial change not rolled back"); });
        Test("Failed restore retains snapshot and retries", delegate { Fixture f = new Fixture(); f.Enter(); f.Displays.FailRestore = true; f.End(2000); Assert(f.Controller.HasSession && f.Recovery.Record != null, "Restore failure lost recovery"); f.Displays.FailRestore = false; f.Controller.Tick(false, f.Settings, 22000); Equal(Desktop, f.Displays.Current["laptop"]); Assert(!f.Controller.HasSession, "Restore retry did not finish"); });
        Test("Unplugged monitor never falls back to primary", delegate {
            Fixture f = new Fixture(); f.Settings.DisplayId = "external"; f.Enter(); f.Displays.Current.Remove("external"); f.End(2000);
            Assert(f.Controller.HasSession, "Must retain disconnected monitor's snapshot"); Equal(Desktop, f.Displays.Current["laptop"]);
            f.Displays.Current["external"] = Game; f.Controller.Tick(false, f.Settings, 22000);
            Equal(new DisplayMode(2560, 1440, 144), f.Displays.Current["external"]);
        });
        Test("Missing selected monitor cannot change another", delegate { Fixture f = new Fixture(); f.Settings.DisplayId = "missing"; f.Controller.Tick(true, f.Settings, 0); Equal(0, f.Displays.Writes.Count); });
        Test("Manual restoration suppresses current game", delegate { Fixture f = new Fixture(); f.Enter(); string e; Assert(f.Controller.ManualRestore(100, out e), e); f.Controller.Tick(true, f.Settings, 20000); Equal(Desktop, f.Displays.Current["laptop"]); f.Controller.Tick(false, f.Settings, 22000); f.Controller.Tick(true, f.Settings, 24000); Assert(f.Controller.IsActive, "New game should trigger after all previous games close"); });
        Test("Manual Mog Mode holds without a game", delegate { Fixture f = new Fixture(); string e; Assert(f.Controller.TryEnter(f.Settings, true, 0, out e), e); f.Controller.Tick(false, f.Settings, 50000); Assert(f.Controller.IsActive, "Manual mode must hold"); Assert(f.Controller.ManualRestore(50001, out e), e); Equal(Desktop, f.Displays.Current["laptop"]); });
        Test("Pausing restores and resuming can re-enter", delegate { Fixture f = new Fixture(); f.Enter(); string e; Assert(f.Controller.SetPaused(true, 100, out e), e); f.Controller.Tick(true, f.Settings, 20000); Equal(Desktop, f.Displays.Current["laptop"]); f.Controller.SetPaused(false, 21000, out e); f.Controller.Tick(true, f.Settings, 22000); Assert(f.Controller.IsActive, "Resume did not re-enter"); });
        Test("Recovery from another instance preserves original", delegate { Fixture f = new Fixture(); f.Enter(); SessionController restarted = new SessionController(f.Displays, f.Recovery); Assert(restarted.LoadRecovery() != null, "Missing crash marker"); string e; Assert(restarted.TryRestore(0, out e), e); Equal(Desktop, f.Displays.Current["laptop"]); });
        Test("Declining recovery keeps current display", delegate { Fixture f = new Fixture(); f.Enter(); SessionController restarted = new SessionController(f.Displays, f.Recovery); restarted.LoadRecovery(); string e; Assert(restarted.DiscardRecovery(out e), e); Equal(Game, f.Displays.Current["laptop"]); Equal(1, f.Displays.Writes.Count); });
        Test("Corrupt recovery blocks changes until discarded", delegate { Fixture f = new Fixture(); f.Recovery.FailLoad = true; try { f.Controller.LoadRecovery(); } catch (IOException) { } f.Controller.Tick(true, f.Settings, 0); Equal(0, f.Displays.Writes.Count); string e; Assert(f.Controller.DiscardRecovery(out e), e); f.Controller.Tick(true, f.Settings, 1); Assert(f.Controller.IsActive, "Did not unblock"); });
        Test("Failed recovery discard blocks automatic restoration", delegate { Fixture f = new Fixture(); f.Enter(); SessionController restarted = new SessionController(f.Displays, f.Recovery); restarted.LoadRecovery(); f.Recovery.FailClear = true; string e; Assert(!restarted.DiscardRecovery(out e), "Expected failed discard"); restarted.Tick(false, f.Settings, 40000); Equal(Game, f.Displays.Current["laptop"]); });
        Test("Failed cleanup cannot forget restoration", delegate { Fixture f = new Fixture(); f.Enter(); f.Recovery.FailClear = true; f.End(2000); Assert(f.Controller.HasSession, "Recovery should remain pending"); int writes = f.Displays.Writes.Count; f.Recovery.FailClear = false; f.Controller.Tick(false, f.Settings, 22000); Equal(writes, f.Displays.Writes.Count); Assert(!f.Controller.HasSession, "Cleanup retry failed"); });
        Test("Legacy settings migrate with deduplicated processes", LegacyConfig);
        Test("Settings and recovery round trip", StorageRoundTrip);
        Test("Invalid persisted settings are reported", InvalidConfig);
        Test("Recovery parse errors are reported", InvalidRecovery);
        Test("Unicode Win32 struct sizes", delegate { Equal(220, NativeDisplayService.DevModeSize); Equal(840, NativeDisplayService.DisplayDeviceSize); });
        Test("Read-only Windows display probe", delegate { NativeDisplayService d = new NativeDisplayService(); foreach (DisplayTarget t in d.GetDisplays()) { DisplayMode m; string e; Assert(d.TryGetCurrent(t.Id, out m, out e), e); Assert(d.GetModes(t.Id).Contains(m), "Current mode missing from supported list"); } });
        Test("Settings window layout and screenshot", UiScreenshot);
        Console.WriteLine("\n" + passed + " passed; " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static string TempFolder() { string p = Path.Combine(Path.GetTempPath(), "VRMoggerTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
    private static void LegacyConfig()
    {
        string folder = TempFolder();
        try {
            File.WriteAllText(Path.Combine(folder, "config.ini"), "MogWidth=1280\nMogHeight=720\nMogHz=60\nDeskWidth=1920\nDeskHeight=1080\nDeskHz=165\nDelay=10\nTriggers=VRChat.exe|vrchat.EXE|Beat Saber.exe\nAutostart=True\nSteamVROnly=1\n");
            Settings s = new SettingsStore(folder).Load(); Equal(2, s.Triggers.Count); Equal(Desktop, s.DesktopMode); Assert(s.RestoreCapturedMode && s.Autostart && s.SteamVROnly, "Legacy options lost");
        } finally { Directory.Delete(folder, true); }
    }
    private static void StorageRoundTrip()
    {
        string folder = TempFolder();
        try {
            SettingsStore settings = new SettingsStore(folder); Settings s = new Settings { DisplayId = "monitor:example=ID/雪", GameMode = Game, DesktopMode = Desktop, DelaySeconds = 0 };
            s.Triggers.Add("VRChat.exe"); settings.Save(s); settings.Save(s); Settings read = settings.Load(); Equal(s.DisplayId, read.DisplayId); Equal(Game, read.GameMode); Equal(0, read.DelaySeconds);
            FileRecoveryStore recovery = new FileRecoveryStore(Path.Combine(folder, "recovery.ini")); recovery.Save(new RecoveryRecord(s.DisplayId, Desktop, Game)); RecoveryRecord r = recovery.Load(); Equal(Desktop, r.OriginalMode); Equal(Game, r.RestoreMode); Equal(s.DisplayId, r.DisplayId); recovery.Clear(); Assert(recovery.Load() == null, "Recovery did not clear");
        } finally { Directory.Delete(folder, true); }
    }
    private static void InvalidConfig()
    {
        string folder = TempFolder(); try { File.WriteAllText(Path.Combine(folder, "config.ini"), "MogWidth=wrong\n"); bool threw = false; try { new SettingsStore(folder).Load(); } catch (FormatException) { threw = true; } Assert(threw, "Bad settings silently ignored"); } finally { Directory.Delete(folder, true); }
    }
    private static void InvalidRecovery()
    {
        string folder = TempFolder(); try { string path = Path.Combine(folder, "recovery.ini"); File.WriteAllText(path, "Version=999\n"); bool threw = false; try { new FileRecoveryStore(path).Load(); } catch (FormatException) { threw = true; } Assert(threw, "Bad recovery silently ignored"); } finally { Directory.Delete(folder, true); }
    }
    private static void UiScreenshot()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Fixture f = new Fixture(); f.Settings.Triggers.Add("VRChat.exe"); f.Settings.Triggers.Add("Beat Saber.exe");
        using (SettingsForm form = new SettingsForm(f.Settings, f.Displays, delegate(Settings s) { return null; }, SystemIcons.Application))
        {
            form.Show(); Application.DoEvents();
            Assert(form.Width >= 600 && form.Height > 400, "Unexpectedly small settings window");
            CheckControls(form, form);
            string output = Environment.GetEnvironmentVariable("VRMOGGER_TEST_OUTPUT") ?? Path.GetDirectoryName(Application.ExecutablePath);
            Directory.CreateDirectory(output);
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save(Path.Combine(output, "settings-preview.png"), System.Drawing.Imaging.ImageFormat.Png); }
            form.Close();
        }
    }
    private static void CheckControls(Control root, Form form)
    {
        foreach (Control c in root.Controls)
        {
            if (!c.Visible) continue;
            Rectangle rect = form.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
            Assert(rect.Right <= form.ClientSize.Width + 2 && rect.Bottom <= form.ClientSize.Height + 2, "Control clipped: " + c.Text);
            CheckControls(c, form);
        }
    }
}
