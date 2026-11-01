using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VRMogger
{
    internal static class StartupManager
    {
        private const string Key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        public static string Read()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Key, false))
                return key == null ? null : key.GetValue("VRMogger") as string;
        }
        public static void Write(string command)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Key))
            {
                if (key == null) throw new IOException("Could not open your Windows startup settings.");
                if (command == null) key.DeleteValue("VRMogger", false);
                else key.SetValue("VRMogger", command, RegistryValueKind.String);
            }
        }
        public static string Command { get { return "\"" + Application.ExecutablePath + "\" --minimized"; } }
    }

    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        public Action Pressed;
        public Action SessionEnding;
        public HotkeyWindow() { CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0312 && message.WParam.ToInt32() == 9000 && Pressed != null) Pressed();
            if (message.Msg == 0x0016 && message.WParam != IntPtr.Zero && SessionEnding != null) SessionEnding();
            base.WndProc(ref message);
        }
        public void Dispose() { DestroyHandle(); }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NativeDisplayService displays = new NativeDisplayService();
        private readonly SettingsStore store;
        private readonly FileRecoveryStore recovery;
        private readonly SessionController controller;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Icon icon;
        private NotifyIcon tray;
        private Timer timer;
        private HotkeyWindow hotkey;
        private Settings settings;
        private ToolStripMenuItem statusItem, enterItem, restoreItem, pauseItem, recoveryItem;
        private bool settingsOpen, closing, ending, setupNeeded;
        private string invalidConfigBackup, lastError;

        public TrayContext(bool minimized)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRMogger");
            store = new SettingsStore(folder);
            recovery = new FileRecoveryStore(Path.Combine(folder, "recovery.ini"));
            controller = new SessionController(displays, recovery);
            icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();
            bool firstRun = !File.Exists(store.ConfigPath);
            string loadError = null;
            try { settings = store.Load(); }
            catch (Exception ex)
            {
                settings = new Settings(); loadError = ex.Message;
                invalidConfigBackup = store.ConfigPath + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            }
            setupNeeded = string.IsNullOrEmpty(settings.DisplayId) || loadError != null;
            if (string.IsNullOrEmpty(settings.DisplayId))
                foreach (DisplayTarget target in displays.GetDisplays()) if (target.IsPrimary) { settings.DisplayId = target.Id; break; }
            BuildTray();
            controller.Error += Report;
            controller.Changed += UpdateStatus;
            if (setupNeeded) { string ignored; controller.SetPaused(true, clock.ElapsedMilliseconds, out ignored); }
            RecoverOnStartup();
            if (loadError != null) MessageBox.Show("Your saved settings could not be read, so automation is paused.\n\n" + loadError + "\n\nReview and save Settings to continue. The original file will be backed up before it is replaced.", "VR Mogger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            hotkey = new HotkeyWindow { Pressed = ToggleMode, SessionEnding = OnSessionEnding };
            if (!NativeMethods.RegisterHotKey(hotkey.Handle, 9000, 0x4000 | 0x0002 | 0x0001, 0x78))
                Report("Ctrl+Alt+F9 is already in use. The tray's Switch and Restore controls still work.");
            timer = new Timer { Interval = 2000 };
            timer.Tick += delegate { Poll(); }; timer.Start();
            UpdateStatus();
            if (firstRun || setupNeeded || (!settings.StartMinimized && !minimized)) ShowSettings();
            Poll();
        }

        private void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            statusItem = new ToolStripMenuItem("Ready") { Enabled = false }; menu.Items.Add(statusItem);
            menu.Items.Add("Settings...", null, delegate { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            enterItem = new ToolStripMenuItem("Switch to Mog Mode", null, delegate { EnterManually(); }); menu.Items.Add(enterItem);
            restoreItem = new ToolStripMenuItem("Restore desktop now", null, delegate { RestoreManually(); }); menu.Items.Add(restoreItem);
            pauseItem = new ToolStripMenuItem("Pause automation", null, delegate {
                lock (controller) { string error; if (!controller.SetPaused(!controller.IsPaused, clock.ElapsedMilliseconds, out error)) Report(error); }
                Poll();
            }); menu.Items.Add(pauseItem);
            recoveryItem = new ToolStripMenuItem("Resolve saved recovery...", null, delegate { RecoverOnStartup(); }); menu.Items.Add(recoveryItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("About", null, delegate {
                MessageBox.Show("VR Mogger 1.1.0-beta.1\nKelly Label Studio x MK2XX2\n\nOpen source under the MIT license.\nNo accounts, telemetry, or runtime downloads.\n\nCtrl+Alt+F9: switch / restore\n\nA lower desktop mode does not guarantee higher VR FPS.", "About VR Mogger", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Items.Add("Exit", null, delegate { ExitSafely(); });
            tray = new NotifyIcon { Icon = icon, Visible = true, Text = "VR Mogger", ContextMenuStrip = menu };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowSettings(); };
        }

        private void RecoverOnStartup()
        {
            lock (controller)
            {
                if (controller.IsActive) return;
                try
                {
                    RecoveryRecord pending = controller.LoadRecovery();
                    if (pending == null) { UpdateStatus(); return; }
                    DialogResult answer = MessageBox.Show("VR Mogger saved an unfinished display session.\n\nRestore " + pending.RestoreMode + " on that same monitor?\n\nYes: attempt restoration.\nNo: keep the current display mode and discard the recovery record.", "Restore previous display session", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                    string error;
                    if (answer == DialogResult.Yes) { if (!controller.TryRestore(clock.ElapsedMilliseconds, out error)) Report(error); }
                    else if (!controller.DiscardRecovery(out error)) Report(error);
                }
                catch (Exception ex)
                {
                    if (MessageBox.Show("The recovery record could not be read. No display changes have been made.\n\n" + ex.Message + "\n\nDiscard the unreadable recovery record? Choose No to leave automation blocked.", "Recovery needs attention", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    { string error; if (!controller.DiscardRecovery(out error)) Report(error); }
                }
            }
            UpdateStatus();
        }

        private bool AnyTriggerRunning()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Process[] processes = Process.GetProcesses();
            foreach (Process process in processes)
                using (process)
                {
                    try { names.Add(process.ProcessName); }
                    catch (InvalidOperationException) { /* Process exited during enumeration. */ }
                    catch (System.ComponentModel.Win32Exception) { /* An inaccessible system process cannot be a normal user game. */ }
                }
            bool matched = false;
            foreach (string name in settings.Triggers)
                if (names.Contains(Path.GetFileNameWithoutExtension(name))) { matched = true; break; }
            return matched && (!settings.SteamVROnly || names.Contains("vrserver") || names.Contains("vrcompositor"));
        }

        private void Poll()
        {
            if (closing || ending) return;
            try { bool any = settings.Triggers.Count > 0 && (!controller.IsPaused || controller.HasSession) && AnyTriggerRunning(); lock (controller) controller.Tick(any, settings, clock.ElapsedMilliseconds); }
            catch (Exception ex) { Report("Could not check the current session. " + ex.Message); }
            UpdateStatus();
        }
        private void EnterManually()
        {
            lock (controller)
            { string error; if (!controller.TryEnter(settings, true, clock.ElapsedMilliseconds, out error)) Report(error); }
            UpdateStatus();
        }
        private void RestoreManually()
        {
            Poll();
            lock (controller)
            { string error; if (!controller.ManualRestore(clock.ElapsedMilliseconds, out error)) Report(error); }
            UpdateStatus();
        }
        private void ToggleMode()
        { if (controller.HasSession) RestoreManually(); else EnterManually(); }

        private string SaveSettings(Settings candidate)
        {
            string previousStartup = null; bool startupRead = false, startupWritten = false;
            try
            {
                if (invalidConfigBackup != null)
                {
                    if (File.Exists(store.ConfigPath)) File.Copy(store.ConfigPath, invalidConfigBackup, false);
                    invalidConfigBackup = null;
                }
                if (candidate.Autostart != settings.Autostart || candidate.Autostart)
                {
                    previousStartup = StartupManager.Read(); startupRead = true;
                    StartupManager.Write(candidate.Autostart ? StartupManager.Command : null); startupWritten = true;
                }
                store.Save(candidate);
            }
            catch (Exception ex)
            {
                string error = ex.Message;
                if (startupRead && startupWritten)
                    try { StartupManager.Write(previousStartup); }
                    catch (Exception rollback) { error += "\nThe previous startup setting could not be restored: " + rollback.Message; }
                return error;
            }
            settings = candidate;
            if (setupNeeded)
            {
                setupNeeded = false;
                lock (controller) { string error; if (!controller.SetPaused(false, clock.ElapsedMilliseconds, out error)) Report(error); }
            }
            lastError = null; store.Log(settings, "Settings saved."); return null;
        }
        private void ShowSettings()
        {
            if (settingsOpen || closing || ending) return;
            settingsOpen = true;
            try { using (SettingsForm form = new SettingsForm(settings, displays, SaveSettings, icon)) form.ShowDialog(); }
            finally { settingsOpen = false; }
            UpdateStatus();
        }
        private void Report(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            store.Log(settings, message);
            if (message == lastError || ending || closing) return;
            lastError = message;
            if (tray != null) tray.ShowBalloonTip(6000, "VR Mogger", message, ToolTipIcon.Warning);
        }
        private void UpdateStatus()
        {
            if (tray == null || closing || ending) return;
            string status = controller.Status;
            statusItem.Text = status;
            tray.Text = ("VR Mogger - " + status).Substring(0, Math.Min(63, ("VR Mogger - " + status).Length));
            enterItem.Enabled = !controller.HasSession && !controller.RecoveryNeedsAttention && !setupNeeded;
            restoreItem.Enabled = controller.HasSession;
            recoveryItem.Visible = controller.RecoveryNeedsAttention;
            pauseItem.Text = controller.IsPaused ? "Resume automation" : "Pause automation";
        }
        private void ExitSafely()
        {
            lock (controller)
            {
                string error;
                if (!controller.TryRestore(clock.ElapsedMilliseconds, out error) &&
                    MessageBox.Show("The display could not be fully restored.\n\n" + error + "\n\nExit anyway? The recovery record will be kept for next launch. Choose No to keep VR Mogger running and retry.", "VR Mogger", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            }
            closing = true; if (timer != null) timer.Stop(); ExitThread();
        }
        private void OnSessionEnding()
        {
            ending = true;
            lock (controller) { string error; if (!controller.TryRestore(clock.ElapsedMilliseconds, out error)) store.Log(settings, "Session ending: " + error); }
        }
        internal void RestoreAfterUnexpectedError()
        {
            try { lock (controller) { string ignored; controller.TryRestore(clock.ElapsedMilliseconds, out ignored); } }
            catch { /* The pre-change recovery record remains on disk. */ }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                closing = true;
                if (timer != null) { timer.Stop(); timer.Dispose(); }
                if (hotkey != null) { NativeMethods.UnregisterHotKey(hotkey.Handle, 9000); hotkey.Dispose(); }
                if (tray != null) { tray.Visible = false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); }
                icon.Dispose(); clock.Stop();
            }
            base.Dispose(disposing);
        }
    }
}
