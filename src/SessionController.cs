// No UI or native calls here: display-session behavior is tested with fake displays.
using System;

namespace VRMogger
{
    public sealed class SessionController
    {
        private readonly IDisplayService displays;
        private readonly IRecoveryStore recovery;
        private RecoveryRecord record;
        private bool active;
        private bool manualHold;
        private bool paused;
        private bool suppressUntilClosed;
        private bool triggerRunning;
        private long? restoreDue;
        private long retryAt;
        private bool recoveryBlocked;
        public const long RetryMilliseconds = 10000;
        public event Action<string> Error;
        public event Action Changed;
        public bool HasSession { get { return record != null; } }
        public bool IsActive { get { return active; } }
        public bool IsPaused { get { return paused; } }
        public bool RecoveryNeedsAttention { get { return recoveryBlocked; } }
        public string Status
        {
            get
            {
                if (recoveryBlocked) return "Recovery needs attention";
                if (record != null && !active) return "Waiting to restore display";
                if (active && manualHold) return "Manual Mog Mode";
                if (active) return restoreDue.HasValue ? "Waiting to restore desktop" : "Mog Mode active";
                if (paused) return "Automation paused";
                if (suppressUntilClosed) return "Desktop restored; waiting for games to close";
                return "Ready";
            }
        }

        public SessionController(IDisplayService displayService, IRecoveryStore recoveryStore)
        { displays = displayService; recovery = recoveryStore; }

        public RecoveryRecord LoadRecovery()
        {
            try { record = recovery.Load(); recoveryBlocked = false; }
            catch { recoveryBlocked = true; throw; }
            active = false;
            return record;
        }

        public bool DiscardRecovery(out string error)
        {
            error = null;
            try { recovery.Clear(); record = null; active = false; recoveryBlocked = false; Notify(); return true; }
            catch (Exception ex) { recoveryBlocked = true; error = "Could not clear the recovery record: " + ex.Message; return false; }
        }

        public void Tick(bool anyTrigger, Settings settings, long now)
        {
            triggerRunning = anyTrigger;
            if (!anyTrigger) suppressUntilClosed = false;
            if (recoveryBlocked) return;
            if (record != null)
            {
                if (!active)
                {
                    if (now >= retryAt) { string error; if (!TryRestore(now, out error)) Report(error); }
                    return;
                }
                if (manualHold) return;
                if (anyTrigger && !paused && !suppressUntilClosed) { restoreDue = null; return; }
                if (!restoreDue.HasValue) { restoreDue = now + settings.DelaySeconds * 1000L; Notify(); }
                if (now >= restoreDue.Value)
                { string error; if (!TryRestore(now, out error)) Report(error); }
                return;
            }
            if (anyTrigger && !paused && !suppressUntilClosed && now >= retryAt)
            { string error; if (!TryEnter(settings, false, now, out error)) Report(error); }
        }

        public bool TryEnter(Settings settings, bool manual, long now, out string error)
        {
            error = null;
            if (recoveryBlocked) { error = "Resolve the saved recovery record before changing displays."; return false; }
            if (record != null) { error = "Restore the current display session first."; return false; }
            DisplayMode original;
            if (string.IsNullOrEmpty(settings.DisplayId)) { error = "Choose a monitor in Settings first."; retryAt = now + RetryMilliseconds; return false; }
            if (!displays.TryGetCurrent(settings.DisplayId, out original, out error))
            { retryAt = now + RetryMilliseconds; return false; }
            if (!settings.RestoreCapturedMode && !displays.GetModes(settings.DisplayId).Contains(settings.DesktopMode))
            { error = "The custom desktop mode is unavailable on the selected monitor. Choose another mode in Settings."; retryAt = now + RetryMilliseconds; return false; }
            RecoveryRecord pending = new RecoveryRecord(settings.DisplayId, original,
                settings.RestoreCapturedMode ? original : settings.DesktopMode);
            // Save before applying, so a crash between these operations is recoverable.
            try { recovery.Save(pending); }
            catch (Exception ex) { error = "No display settings changed: could not save recovery data. " + ex.Message; retryAt = now + RetryMilliseconds; return false; }
            record = pending;
            if (!displays.TryApply(settings.DisplayId, settings.GameMode, out error))
            {
                string applyError = error;
                // Applying may have partly changed a driver. Restore the captured mode, not a custom preset.
                record.RestoreMode = original;
                string restoreError;
                bool restored = TryRestore(now, out restoreError);
                retryAt = now + RetryMilliseconds;
                error = applyError + (restored ? "" : "\n" + restoreError);
                return false;
            }
            active = true; manualHold = manual; restoreDue = null; retryAt = 0;
            Notify();
            return true;
        }

        public bool ManualRestore(long now, out string error)
        {
            // Keep the user's manual restoration until all matching games close.
            suppressUntilClosed = triggerRunning;
            manualHold = false;
            return TryRestore(now, out error);
        }

        public bool SetPaused(bool value, long now, out string error)
        {
            paused = value;
            if (!value) suppressUntilClosed = false;
            Notify();
            if (value && record != null) { manualHold = false; return TryRestore(now, out error); }
            error = null; return true;
        }

        public bool TryRestore(long now, out string error)
        {
            error = null;
            if (record == null) { if (recoveryBlocked) error = "Resolve the unreadable recovery record first."; return !recoveryBlocked; }
            active = false; manualHold = false; restoreDue = null;
            DisplayMode current;
            if (!displays.TryGetCurrent(record.DisplayId, out current, out error) ||
                (!current.Equals(record.RestoreMode) && !displays.TryApply(record.DisplayId, record.RestoreMode, out error)))
            {
                retryAt = now + RetryMilliseconds; Notify(); return false;
            }
            // Do not forget the snapshot if clearing it fails. Retry without switching again.
            try { recovery.Clear(); }
            catch (Exception ex) { error = "Display restored, but recovery data could not be cleared. " + ex.Message; retryAt = now + RetryMilliseconds; Notify(); return false; }
            record = null; retryAt = 0; recoveryBlocked = false; Notify();
            return true;
        }

        private void Report(string error) { if (Error != null && !string.IsNullOrEmpty(error)) Error(error); }
        private void Notify() { if (Changed != null) Changed(); }
    }
}
