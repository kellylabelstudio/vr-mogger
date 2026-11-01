// VR Mogger v1.0 - single-file build
// (c) Kelly Label Studio x MK2XX2
// Compile:
//   csc /target:winexe /optimize+ /out:VRMogger.exe /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll VRMogger.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VRMogger
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    public static class Win32
    {
        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int DISP_CHANGE_SUCCESSFUL = 0;
        public const int DM_PELSWIDTH = 0x00080000;
        public const int DM_PELSHEIGHT = 0x00100000;
        public const int DM_DISPLAYFREQUENCY = 0x00400000;
        public const int CDS_TEST = 0x00000002;
        public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
        public const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const int VK_F9 = 0x78;
        public const int HOTKEY_ID = 9000;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplaySettingsEx(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, uint dwFlags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwFlags, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    public class DisplayMode
    {
        public int Width;
        public int Height;
        public int Refresh;
        public DisplayMode() { }
        public DisplayMode(int w, int h, int r) { Width = w; Height = h; Refresh = r; }
    }

    public class Config
    {
        public List<string> Triggers = new List<string>();
        public DisplayMode Mog = new DisplayMode(1280, 720, 60);
        public DisplayMode Desk = new DisplayMode(1920, 1080, 60);
        public int DelaySeconds = 10;
        public bool SteamVROnly = false;
        public bool Autostart = false;
        public bool StartMinimized = true;
        public bool DebugLogging = false;
    }

    public static class ConfigStore
    {
        public static string DirPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRMogger");
        }
        public static string ConfigPath() { return Path.Combine(DirPath(), "config.ini"); }
        public static string LogPath() { return Path.Combine(DirPath(), "debug.log"); }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (File.Exists(ConfigPath()))
                {
                    Dictionary<string, string> kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    string[] lines = File.ReadAllLines(ConfigPath());
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        int idx = line.IndexOf('=');
                        if (line.Length == 0 || idx <= 0) continue;
                        kv[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                    }
                    c.Mog.Width = GetInt(kv, "MogWidth", c.Mog.Width);
                    c.Mog.Height = GetInt(kv, "MogHeight", c.Mog.Height);
                    c.Mog.Refresh = GetInt(kv, "MogHz", c.Mog.Refresh);
                    c.Desk.Width = GetInt(kv, "DeskWidth", c.Desk.Width);
                    c.Desk.Height = GetInt(kv, "DeskHeight", c.Desk.Height);
                    c.Desk.Refresh = GetInt(kv, "DeskHz", c.Desk.Refresh);
                    c.DelaySeconds = GetInt(kv, "Delay", c.DelaySeconds);
                    c.SteamVROnly = GetBool(kv, "SteamVROnly", c.SteamVROnly);
                    c.Autostart = GetBool(kv, "Autostart", c.Autostart);
                    c.StartMinimized = GetBool(kv, "StartMinimized", c.StartMinimized);
                    c.DebugLogging = GetBool(kv, "DebugLog", c.DebugLogging);
                    string trig = GetString(kv, "Triggers", "");
                    if (trig.Length > 0)
                    {
                        string[] parts = trig.Split('|');
                        for (int i = 0; i < parts.Length; i++)
                            if (parts[i].Trim().Length > 0) c.Triggers.Add(parts[i].Trim());
                    }
                }
            }
            catch { }
            return c;
        }

        public static void Save(Config c)
        {
            try
            {
                if (!Directory.Exists(DirPath())) Directory.CreateDirectory(DirPath());
                StringBuilder sb = new StringBuilder();
                sb.Append("MogWidth=").Append(c.Mog.Width).AppendLine();
                sb.Append("MogHeight=").Append(c.Mog.Height).AppendLine();
                sb.Append("MogHz=").Append(c.Mog.Refresh).AppendLine();
                sb.Append("DeskWidth=").Append(c.Desk.Width).AppendLine();
                sb.Append("DeskHeight=").Append(c.Desk.Height).AppendLine();
                sb.Append("DeskHz=").Append(c.Desk.Refresh).AppendLine();
                sb.Append("Delay=").Append(c.DelaySeconds).AppendLine();
                sb.Append("SteamVROnly=").Append(c.SteamVROnly ? 1 : 0).AppendLine();
                sb.Append("Autostart=").Append(c.Autostart ? 1 : 0).AppendLine();
                sb.Append("StartMinimized=").Append(c.StartMinimized ? 1 : 0).AppendLine();
                sb.Append("DebugLog=").Append(c.DebugLogging ? 1 : 0).AppendLine();
                sb.Append("Triggers=").Append(string.Join("|", c.Triggers.ToArray())).AppendLine();
                File.WriteAllText(ConfigPath(), sb.ToString());
            }
            catch { }
        }

        public static void Log(Config c, string msg)
        {
            if (!c.DebugLogging) return;
            try
            {
                if (!Directory.Exists(DirPath())) Directory.CreateDirectory(DirPath());
                File.AppendAllText(LogPath(), "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg + Environment.NewLine);
            }
            catch { }
        }

        private static int GetInt(Dictionary<string, string> kv, string key, int def)
        {
            string v;
            if (kv.TryGetValue(key, out v))
            {
                int parsed;
                if (int.TryParse(v, out parsed)) return parsed;
            }
            return def;
        }
        private static bool GetBool(Dictionary<string, string> kv, string key, bool def)
        {
            return GetInt(kv, key, def ? 1 : 0) != 0;
        }
        private static string GetString(Dictionary<string, string> kv, string key, string def)
        {
            string v;
            if (kv.TryGetValue(key, out v)) return v;
            return def;
        }
    }

    public static class DisplayManager
    {
        public static string GetPrimaryDevice()
        {
            DISPLAY_DEVICE d = new DISPLAY_DEVICE();
            d.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
            uint i = 0;
            while (Win32.EnumDisplayDevices(null, i, ref d, 0))
            {
                if ((d.StateFlags & Win32.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0 &&
                    (d.StateFlags & Win32.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                    return d.DeviceName;
                i++;
                d.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
            }
            return null;
        }

        public static bool IsModeSupported(string device, int w, int h, int hz)
        {
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            int i = 0;
            while (Win32.EnumDisplaySettingsEx(device, i, ref dm, 0))
            {
                if (dm.dmPelsWidth == w && dm.dmPelsHeight == h && dm.dmDisplayFrequency == hz)
                    return true;
                i++;
            }
            return false;
        }

        public static bool ChangeMode(string device, DisplayMode mode, out string error)
        {
            error = "";
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (!Win32.EnumDisplaySettingsEx(device, Win32.ENUM_CURRENT_SETTINGS, ref dm, 0))
            {
                error = "Could not read current display settings.";
                return false;
            }
            dm.dmFields = Win32.DM_PELSWIDTH | Win32.DM_PELSHEIGHT | Win32.DM_DISPLAYFREQUENCY;
            dm.dmPelsWidth = mode.Width;
            dm.dmPelsHeight = mode.Height;
            dm.dmDisplayFrequency = mode.Refresh;
            int test = Win32.ChangeDisplaySettingsEx(device, ref dm, IntPtr.Zero, Win32.CDS_TEST, IntPtr.Zero);
            if (test != Win32.DISP_CHANGE_SUCCESSFUL)
            {
                error = "Mode not supported by this display (code " + test + ").";
                return false;
            }
            int apply = Win32.ChangeDisplaySettingsEx(device, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
            if (apply != Win32.DISP_CHANGE_SUCCESSFUL)
            {
                error = "Display driver refused the change (code " + apply + ").";
                return false;
            }
            return true;
        }
    }

    public class HotkeyWindow : NativeWindow
    {
        public event Action HotkeyPressed;
        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams();
            CreateHandle(cp);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32.WM_HOTKEY && m.WParam.ToInt32() == Win32.HOTKEY_ID)
            {
                Action handler = HotkeyPressed;
                if (handler != null) handler();
            }
            base.WndProc(ref m);
        }
    }

    public class SettingsForm : Form
    {
        private Config config;
        private ListBox triggerList;
        private TextBox triggerInput;
        private NumericUpDown mogW, mogH, mogHz, deskW, deskH, deskHz, delayNum;
        private CheckBox steamVrOnly, autostart, startMin, debugLog;

        public SettingsForm(Config cfg)
        {
            config = cfg;
            Text = "VR Mogger Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(470, 425);
            BuildUi();
            LoadFromConfig();
        }

        private void BuildUi()
        {
            GroupBox gTrig = new GroupBox();
            gTrig.Text = "Trigger Processes (.exe names)";
            gTrig.Location = new Point(10, 10);
            gTrig.Size = new Size(450, 110);
            Controls.Add(gTrig);

            triggerList = new ListBox();
            triggerList.Location = new Point(10, 22);
            triggerList.Size = new Size(190, 78);
            gTrig.Controls.Add(triggerList);

            triggerInput = new TextBox();
            triggerInput.Location = new Point(210, 24);
            triggerInput.Size = new Size(150, 24);
            gTrig.Controls.Add(triggerInput);

            Button addBtn = new Button();
            addBtn.Text = "Add";
            addBtn.Location = new Point(370, 22);
            addBtn.Size = new Size(70, 25);
            addBtn.Click += delegate { AddTrigger(); };
            gTrig.Controls.Add(addBtn);

            Button removeBtn = new Button();
            removeBtn.Text = "Remove";
            removeBtn.Location = new Point(370, 52);
            removeBtn.Size = new Size(70, 25);
            removeBtn.Click += delegate { RemoveTrigger(); };
            gTrig.Controls.Add(removeBtn);

            GroupBox gMog = new GroupBox();
            gMog.Text = "Mog Mode (in-game)";
            gMog.Location = new Point(10, 125);
            gMog.Size = new Size(450, 60);
            Controls.Add(gMog);
            AddModeRow(gMog, out mogW, out mogH, out mogHz, delegate { ValidateMode(mogW, mogH, mogHz, "Mog Mode"); });

            GroupBox gDesk = new GroupBox();
            gDesk.Text = "Desktop Mode (restore)";
            gDesk.Location = new Point(10, 190);
            gDesk.Size = new Size(450, 60);
            Controls.Add(gDesk);
            AddModeRow(gDesk, out deskW, out deskH, out deskHz, delegate { ValidateMode(deskW, deskH, deskHz, "Desktop Mode"); });

            GroupBox gOpt = new GroupBox();
            gOpt.Text = "Options";
            gOpt.Location = new Point(10, 255);
            gOpt.Size = new Size(450, 115);
            Controls.Add(gOpt);

            Label delayLabel = new Label();
            delayLabel.Text = "Restore delay (seconds):";
            delayLabel.Location = new Point(10, 24);
            delayLabel.Size = new Size(150, 23);
            gOpt.Controls.Add(delayLabel);

            delayNum = new NumericUpDown();
            delayNum.Location = new Point(165, 22);
            delayNum.Size = new Size(60, 24);
            delayNum.Minimum = 1;
            delayNum.Maximum = 300;
            gOpt.Controls.Add(delayNum);

            steamVrOnly = new CheckBox();
            steamVrOnly.Text = "Only trigger when SteamVR is running";
            steamVrOnly.Location = new Point(10, 50);
            steamVrOnly.Size = new Size(280, 24);
            gOpt.Controls.Add(steamVrOnly);

            autostart = new CheckBox();
            autostart.Text = "Start with Windows";
            autostart.Location = new Point(10, 74);
            autostart.Size = new Size(160, 24);
            gOpt.Controls.Add(autostart);

            startMin = new CheckBox();
            startMin.Text = "Start minimized to tray";
            startMin.Location = new Point(200, 74);
            startMin.Size = new Size(180, 24);
            gOpt.Controls.Add(startMin);

            debugLog = new CheckBox();
            debugLog.Text = "Enable debug logging";
            debugLog.Location = new Point(10, 98);
            debugLog.Size = new Size(180, 24);
            gOpt.Controls.Add(debugLog);

            Button saveBtn = new Button();
            saveBtn.Text = "Save";
            saveBtn.Location = new Point(280, 382);
            saveBtn.Size = new Size(85, 30);
            saveBtn.Click += delegate { SaveAndClose(); };
            Controls.Add(saveBtn);

            Button cancelBtn = new Button();
            cancelBtn.Text = "Cancel";
            cancelBtn.Location = new Point(375, 382);
            cancelBtn.Size = new Size(85, 30);
            cancelBtn.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancelBtn);

            AcceptButton = saveBtn;
            CancelButton = cancelBtn;
        }

        private void AddModeRow(GroupBox group, out NumericUpDown w, out NumericUpDown h, out NumericUpDown hz, EventHandler validate)
        {
            Label label = new Label();
            label.Text = "W x H @ Hz:";
            label.Location = new Point(10, 24);
            label.Size = new Size(80, 23);
            group.Controls.Add(label);

            w = new NumericUpDown();
            w.Location = new Point(95, 22);
            w.Size = new Size(70, 24);
            w.Minimum = 640;
            w.Maximum = 7680;
            group.Controls.Add(w);

            Label xLabel = new Label();
            xLabel.Text = "x";
            xLabel.Location = new Point(170, 24);
            xLabel.Size = new Size(12, 23);
            group.Controls.Add(xLabel);

            h = new NumericUpDown();
            h.Location = new Point(185, 22);
            h.Size = new Size(70, 24);
            h.Minimum = 480;
            h.Maximum = 4320;
            group.Controls.Add(h);

            Label atLabel = new Label();
            atLabel.Text = "@";
            atLabel.Location = new Point(260, 24);
            atLabel.Size = new Size(14, 23);
            group.Controls.Add(atLabel);

            hz = new NumericUpDown();
            hz.Location = new Point(277, 22);
            hz.Size = new Size(70, 24);
            hz.Minimum = 30;
            hz.Maximum = 360;
            group.Controls.Add(hz);

            Button validateBtn = new Button();
            validateBtn.Text = "Validate";
            validateBtn.Location = new Point(355, 20);
            validateBtn.Size = new Size(85, 26);
            validateBtn.Click += validate;
            group.Controls.Add(validateBtn);
        }

        private void LoadFromConfig()
        {
            for (int i = 0; i < config.Triggers.Count; i++)
                triggerList.Items.Add(config.Triggers[i]);
            SetNum(mogW, config.Mog.Width);
            SetNum(mogH, config.Mog.Height);
            SetNum(mogHz, config.Mog.Refresh);
            SetNum(deskW, config.Desk.Width);
            SetNum(deskH, config.Desk.Height);
            SetNum(deskHz, config.Desk.Refresh);
            SetNum(delayNum, config.DelaySeconds);
            steamVrOnly.Checked = config.SteamVROnly;
            autostart.Checked = config.Autostart;
            startMin.Checked = config.StartMinimized;
            debugLog.Checked = config.DebugLogging;
        }

        private static void SetNum(NumericUpDown num, int value)
        {
            if (value < (int)num.Minimum) value = (int)num.Minimum;
            if (value > (int)num.Maximum) value = (int)num.Maximum;
            num.Value = value;
        }

        private void AddTrigger()
        {
            string text = triggerInput.Text.Trim();
            if (text.Length == 0) return;
            if (!triggerList.Items.Contains(text))
                triggerList.Items.Add(text);
            triggerInput.Clear();
            triggerInput.Focus();
        }

        private void RemoveTrigger()
        {
            if (triggerList.SelectedIndex >= 0)
                triggerList.Items.RemoveAt(triggerList.SelectedIndex);
        }

        private void ValidateMode(NumericUpDown w, NumericUpDown h, NumericUpDown hz, string name)
        {
            string device = DisplayManager.GetPrimaryDevice();
            if (device == null)
            {
                MessageBox.Show("No primary display found.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (DisplayManager.IsModeSupported(device, (int)w.Value, (int)h.Value, (int)hz.Value))
            {
                MessageBox.Show(name + " is supported by your display.", "Validation Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(name + " is NOT supported by your display.\nPick different values.", "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveAndClose()
        {
            config.Triggers.Clear();
            for (int i = 0; i < triggerList.Items.Count; i++)
                config.Triggers.Add((string)triggerList.Items[i]);
            config.Mog = new DisplayMode((int)mogW.Value, (int)mogH.Value, (int)mogHz.Value);
            config.Desk = new DisplayMode((int)deskW.Value, (int)deskH.Value, (int)deskHz.Value);
            config.DelaySeconds = (int)delayNum.Value;
            config.SteamVROnly = steamVrOnly.Checked;
            config.Autostart = autostart.Checked;
            config.StartMinimized = startMin.Checked;
            config.DebugLogging = debugLog.Checked;
            ConfigStore.Save(config);
            SetAutostart(config.Autostart);
            DialogResult = DialogResult.OK;
            Close();
        }

        private static void SetAutostart(bool enable)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                if (enable)
                    key.SetValue("VRMogger", "\"" + Application.ExecutablePath + "\" /minimized");
                else
                    key.DeleteValue("VRMogger", false);
                key.Close();
            }
            catch { }
        }
    }

    public class MoggerContext : ApplicationContext
    {
        private Config config;
        private NotifyIcon tray;
        private System.Windows.Forms.Timer pollTimer;
        private HotkeyWindow hotkeyWindow;
        private bool inMogMode = false;
        private bool triggerWasRunning = false;
        private DateTime restoreAt = DateTime.MinValue;

        public MoggerContext()
        {
            bool firstRun = !File.Exists(ConfigStore.ConfigPath());
            config = ConfigStore.Load();

            if (firstRun)
            {
                // Capture the user's current desktop mode as the restore default
                string dev = DisplayManager.GetPrimaryDevice();
                if (dev != null)
                {
                    DEVMODE dm = new DEVMODE();
                    dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                    if (Win32.EnumDisplaySettingsEx(dev, Win32.ENUM_CURRENT_SETTINGS, ref dm, 0))
                        config.Desk = new DisplayMode(dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency);
                }
            }

            BuildTray();

            hotkeyWindow = new HotkeyWindow();
            hotkeyWindow.HotkeyPressed += new Action(ToggleMode);
            Win32.RegisterHotKey(hotkeyWindow.Handle, Win32.HOTKEY_ID, Win32.MOD_CONTROL | Win32.MOD_ALT, Win32.VK_F9);

            pollTimer = new System.Windows.Forms.Timer();
            pollTimer.Interval = 2000;
            pollTimer.Tick += new EventHandler(PollTick);
            pollTimer.Start();

            if (firstRun || (!config.StartMinimized && !HasMinimizedArg()))
                ShowSettings();
        }

        private static bool HasMinimizedArg()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i].ToLowerInvariant() == "/minimized") return true;
            return false;
        }

        private void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Show Settings", null, delegate { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Switch to Mog Mode Now", null, delegate { SwitchTo(config.Mog, true); });
            menu.Items.Add("Restore Desktop Mode Now", null, delegate { SwitchTo(config.Desk, false); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("About", null, delegate { ShowAbout(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitApp(); });

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "VR Mogger";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowSettings();
            };
        }

        private void PollTick(object sender, EventArgs e)
        {
            bool anyTrigger = AnyTriggerRunning();
            bool should = anyTrigger;
            if (should && config.SteamVROnly)
                should = IsRunning("vrserver") || IsRunning("vrcompositor");

            if (should)
            {
                if (!triggerWasRunning)
                {
                    triggerWasRunning = true;
                    ConfigStore.Log(config, "Trigger detected - switching to Mog Mode.");
                    SwitchTo(config.Mog, true);
                }
                restoreAt = DateTime.MinValue;
            }
            else if (triggerWasRunning)
            {
                if (restoreAt == DateTime.MinValue)
                {
                    restoreAt = DateTime.Now.AddSeconds(config.DelaySeconds);
                    ConfigStore.Log(config, "Triggers closed - restore in " + config.DelaySeconds + "s.");
                }
                else if (DateTime.Now >= restoreAt)
                {
                    triggerWasRunning = false;
                    restoreAt = DateTime.MinValue;
                    ConfigStore.Log(config, "Restoring Desktop Mode.");
                    SwitchTo(config.Desk, false);
                }
            }
        }

        private bool AnyTriggerRunning()
        {
            for (int i = 0; i < config.Triggers.Count; i++)
                if (IsRunning(config.Triggers[i])) return true;
            return false;
        }

        private bool IsRunning(string name)
        {
            try
            {
                string bare = Path.GetFileNameWithoutExtension(name);
                return Process.GetProcessesByName(bare).Length > 0;
            }
            catch { return false; }
        }

        private void SwitchTo(DisplayMode mode, bool enteringMog)
        {
            string device = DisplayManager.GetPrimaryDevice();
            if (device == null)
            {
                tray.ShowBalloonTip(3000, "VR Mogger", "No primary display found.", ToolTipIcon.Warning);
                return;
            }
            string error;
            if (DisplayManager.ChangeMode(device, mode, out error))
            {
                inMogMode = enteringMog;
            }
            else
            {
                tray.ShowBalloonTip(4000, "VR Mogger - mode change failed", error, ToolTipIcon.Warning);
                ConfigStore.Log(config, "Switch failed: " + error);
            }
        }

        private void ToggleMode()
        {
            if (inMogMode)
                SwitchTo(config.Desk, false);
            else
                SwitchTo(config.Mog, true);
        }

        private void ShowSettings()
        {
            SettingsForm form = new SettingsForm(config);
            if (form.ShowDialog() == DialogResult.OK)
            {
                config = ConfigStore.Load();
                ConfigStore.Log(config, "Settings saved and reloaded.");
            }
            form.Dispose();
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "VR Mogger v1.0\n" +
                "Per-process display mode switcher for VR gaming.\n" +
                "(c) Kelly Label Studio x MK2XX2\n\n" +
                "Hotkey: Ctrl+Alt+F9",
                "About VR Mogger",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExitApp()
        {
            pollTimer.Stop();
            Win32.UnregisterHotKey(hotkeyWindow.Handle, Win32.HOTKEY_ID);
            tray.Visible = false;
            tray.Dispose();
            Application.Exit();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            Mutex mutex = new Mutex(true, "VRMogger_SingleInstance", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("VR Mogger is already running.", "VR Mogger", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MoggerContext());
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}