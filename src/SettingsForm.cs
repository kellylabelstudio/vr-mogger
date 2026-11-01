using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace VRMogger
{
    public sealed class SettingsForm : Form
    {
        private readonly Settings original;
        private readonly IDisplayService displays;
        private readonly Func<Settings, string> save;
        private ComboBox monitorBox, gameBox, desktopBox;
        private ListBox triggers;
        private TextBox processInput;
        private NumericUpDown delay;
        private CheckBox captured, steamOnly, autostart, startMinimized, logging;
        private Label monitorStatus;
        private Button saveButton;
        private bool updating;

        public SettingsForm(Settings settings, IDisplayService service, Func<Settings, string> onSave, Icon icon)
        {
            original = settings.Copy(); displays = service; save = onSave;
            Text = "VR Mogger - Settings"; Icon = icon;
            Font = new Font("Segoe UI", 9F);
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BuildUi(); RefreshMonitors();
        }

        private static TableLayoutPanel Table(int columns)
        {
            TableLayoutPanel t = new TableLayoutPanel { ColumnCount = columns, AutoSize = true, Dock = DockStyle.Top, GrowStyle = TableLayoutPanelGrowStyle.AddRows, Padding = new Padding(6) };
            for (int i = 0; i < columns; i++) t.ColumnStyles.Add(new ColumnStyle(i == 0 && columns > 1 ? SizeType.AutoSize : SizeType.Percent, 100F));
            return t;
        }
        private static ComboBox ModeBox()
        { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, IntegralHeight = false, DropDownHeight = 260 }; }
        private static Label TextLabel(string text)
        { return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 9, 7) }; }
        private static GroupBox Group(string text, Control child)
        {
            GroupBox group = new GroupBox { Text = text, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(8), Margin = new Padding(0, 4, 0, 6) };
            group.Controls.Add(child); return group;
        }
        private static void Row(TableLayoutPanel table, string label, Control control)
        { int row = table.RowCount++; table.Controls.Add(TextLabel(label), 0, row); table.Controls.Add(control, 1, row); }
        private static CheckBox Check(string text, bool value)
        { return new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(3, 5, 3, 5) }; }

        private void BuildUi()
        {
            TableLayoutPanel main = Table(1); main.Width = 610; main.MinimumSize = new Size(610, 0); main.Padding = new Padding(16);
            main.Controls.Add(new Label { Text = "VR MOGGER", AutoSize = true, Font = new Font("Segoe UI", 17F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 2) });
            main.Controls.Add(new Label { Text = "Switch your desktop display for the games you choose.", AutoSize = true, Margin = new Padding(0, 0, 0, 10) });

            TableLayoutPanel target = Table(2);
            target.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100F); target.ColumnStyles[1] = new ColumnStyle(SizeType.AutoSize);
            monitorBox = ModeBox(); monitorBox.MinimumSize = new Size(430, 0);
            Button refresh = new Button { Text = "Refresh", AutoSize = true };
            refresh.Click += delegate { RefreshMonitors(); };
            target.Controls.Add(monitorBox, 0, 0); target.Controls.Add(refresh, 1, 0);
            monitorStatus = new Label { AutoSize = true, MaximumSize = new Size(530, 0), Margin = new Padding(3, 6, 3, 3) };
            target.Controls.Add(monitorStatus, 0, 1); target.SetColumnSpan(monitorStatus, 2);
            main.Controls.Add(Group("Monitor to change", target));

            TableLayoutPanel programs = Table(2);
            programs.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100F); programs.ColumnStyles[1] = new ColumnStyle(SizeType.AutoSize);
            triggers = new ListBox { Height = 90, Dock = DockStyle.Fill };
            foreach (string name in original.Triggers) triggers.Items.Add(name);
            programs.Controls.Add(triggers, 0, 0);
            FlowLayoutPanel tools = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Top };
            Button browse = new Button { Text = "Browse...", AutoSize = true };
            browse.Click += delegate {
                using (OpenFileDialog dialog = new OpenFileDialog { Filter = "Programs (*.exe)|*.exe", CheckFileExists = true })
                    if (dialog.ShowDialog(this) == DialogResult.OK) AddTrigger(Path.GetFileName(dialog.FileName));
            };
            Button remove = new Button { Text = "Remove", AutoSize = true };
            remove.Click += delegate { if (triggers.SelectedIndex >= 0) triggers.Items.RemoveAt(triggers.SelectedIndex); };
            tools.Controls.Add(browse); tools.Controls.Add(remove); programs.Controls.Add(tools, 1, 0);
            processInput = new TextBox { Dock = DockStyle.Fill };
            Button add = new Button { Text = "Add name", AutoSize = true };
            add.Click += delegate { AddTrigger(processInput.Text); };
            processInput.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { AddTrigger(processInput.Text); e.SuppressKeyPress = true; } };
            programs.Controls.Add(processInput, 0, 1); programs.Controls.Add(add, 1, 1);
            main.Controls.Add(Group("Trigger programs - for example VRChat.exe or Beat Saber.exe", programs));

            TableLayoutPanel profiles = Table(2);
            gameBox = ModeBox(); desktopBox = ModeBox();
            Row(profiles, "Mog Mode", gameBox);
            captured = Check("Restore the mode captured before gaming", original.RestoreCapturedMode);
            profiles.Controls.Add(captured, 0, profiles.RowCount++); profiles.SetColumnSpan(captured, 2);
            Row(profiles, "Custom desktop mode", desktopBox);
            delay = new NumericUpDown { Minimum = 0, Maximum = 300, Value = original.DelaySeconds, Width = 80 };
            Row(profiles, "Restore delay (seconds)", delay);
            steamOnly = Check("Only trigger when SteamVR is running", original.SteamVROnly);
            profiles.Controls.Add(steamOnly, 0, profiles.RowCount++); profiles.SetColumnSpan(steamOnly, 2);
            captured.CheckedChanged += delegate { desktopBox.Enabled = !captured.Checked; };
            desktopBox.Enabled = !captured.Checked;
            main.Controls.Add(Group("Display modes", profiles));

            FlowLayoutPanel options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
            autostart = Check("Start with Windows", original.Autostart);
            startMinimized = Check("Start in tray", original.StartMinimized);
            logging = Check("Debug logging", original.DebugLogging);
            options.Controls.Add(autostart); options.Controls.Add(startMinimized); options.Controls.Add(logging);
            main.Controls.Add(Group("Startup and diagnostics", options));

            main.Controls.Add(new Label { Text = "Changes apply to the next display session. Ctrl+Alt+F9 toggles Mog Mode.\nThis changes a Windows desktop display, not your headset's render resolution.", AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(0, 5, 0, 8) });
            FlowLayoutPanel actions = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Top };
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            saveButton = new Button { Text = "Save", AutoSize = true };
            saveButton.Click += delegate { SaveAndClose(); };
            actions.Controls.Add(cancel); actions.Controls.Add(saveButton); main.Controls.Add(actions);
            AcceptButton = saveButton; CancelButton = cancel;
            monitorBox.SelectedIndexChanged += delegate { if (!updating) LoadModes(); };
            Controls.Add(main);
        }

        private void AddTrigger(string input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input)) return;
                string name = Settings.NormalizeProcess(input);
                foreach (string existing in triggers.Items) if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) { processInput.Clear(); return; }
                triggers.Items.Add(name); processInput.Clear();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Invalid program name", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void RefreshMonitors()
        {
            DisplayTarget selected = monitorBox.SelectedItem as DisplayTarget;
            string wanted = selected != null ? selected.Id : original.DisplayId;
            try
            {
                updating = true; monitorBox.Items.Clear();
                IList<DisplayTarget> targets = displays.GetDisplays();
                foreach (DisplayTarget target in targets) monitorBox.Items.Add(target);
                int chosen = -1;
                for (int i = 0; i < targets.Count; i++)
                    if (string.Equals(targets[i].Id, wanted, StringComparison.OrdinalIgnoreCase)) chosen = i;
                if (chosen < 0 && string.IsNullOrEmpty(wanted))
                    for (int i = 0; i < targets.Count; i++) if (targets[i].IsPrimary) { chosen = i; break; }
                if (chosen < 0 && !string.IsNullOrEmpty(wanted))
                { monitorBox.Items.Add(new DisplayTarget(wanted, "Missing", "Saved monitor is disconnected", false)); chosen = monitorBox.Items.Count - 1; }
                monitorBox.SelectedIndex = chosen;
            }
            catch (Exception ex) { monitorStatus.Text = "Could not list monitors: " + ex.Message; saveButton.Enabled = false; }
            finally { updating = false; }
            LoadModes();
        }

        private void LoadModes()
        {
            DisplayMode game = gameBox.SelectedItem as DisplayMode ?? original.GameMode;
            DisplayMode desktop = desktopBox.SelectedItem as DisplayMode ?? original.DesktopMode;
            gameBox.Items.Clear(); desktopBox.Items.Clear();
            DisplayTarget target = monitorBox.SelectedItem as DisplayTarget;
            saveButton.Enabled = false;
            if (target == null) { monitorStatus.Text = "No desktop display found. Connect a monitor and click Refresh."; return; }
            try
            {
                DisplayMode current; string error;
                if (!displays.TryGetCurrent(target.Id, out current, out error)) { monitorStatus.Text = error; return; }
                IList<DisplayMode> modes = displays.GetModes(target.Id);
                foreach (DisplayMode mode in modes) { gameBox.Items.Add(mode); desktopBox.Items.Add(mode); }
                SelectMode(gameBox, game, current); SelectMode(desktopBox, desktop, current);
                monitorStatus.Text = "Current mode: " + current + ". Other displays are left alone.";
                saveButton.Enabled = modes.Count > 0;
            }
            catch (Exception ex) { monitorStatus.Text = "Could not read display modes: " + ex.Message; }
        }

        private static void SelectMode(ComboBox box, DisplayMode preferred, DisplayMode fallback)
        {
            int chosen = box.Items.IndexOf(preferred);
            if (chosen < 0) chosen = box.Items.IndexOf(fallback);
            box.SelectedIndex = chosen >= 0 ? chosen : (box.Items.Count > 0 ? 0 : -1);
        }
        private void SaveAndClose()
        {
            DisplayTarget target = monitorBox.SelectedItem as DisplayTarget;
            DisplayMode game = gameBox.SelectedItem as DisplayMode, desktop = desktopBox.SelectedItem as DisplayMode;
            if (target == null || game == null || desktop == null) return;
            try
            {
                IList<DisplayMode> supported = displays.GetModes(target.Id);
                if (!supported.Contains(game) || (!captured.Checked && !supported.Contains(desktop)))
                    throw new InvalidOperationException("The monitor's available modes changed. Click Refresh and choose the modes again.");
                Settings candidate = original.Copy(); candidate.DisplayId = target.Id;
                candidate.GameMode = game; candidate.DesktopMode = desktop; candidate.RestoreCapturedMode = captured.Checked;
                candidate.DelaySeconds = (int)delay.Value; candidate.SteamVROnly = steamOnly.Checked;
                candidate.Autostart = autostart.Checked; candidate.StartMinimized = startMinimized.Checked; candidate.DebugLogging = logging.Checked;
                candidate.Triggers.Clear(); foreach (string name in triggers.Items) candidate.Triggers.Add(name);
                string error = save(candidate);
                if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { MessageBox.Show(this, "Settings were not saved.\n\n" + ex.Message, "VR Mogger", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
