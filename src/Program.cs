using System;
using System.Threading;
using System.Windows.Forms;

namespace VRMogger
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "VRMogger_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("VR Mogger is already running. Open Settings from its icon near the Windows clock.", "VR Mogger", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                TrayContext context = null;
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) {
                        if (context != null) context.RestoreAfterUnexpectedError();
                        MessageBox.Show("VR Mogger encountered an unexpected error. Display restoration was attempted. Any unfinished recovery record is kept for next launch.\n\n" + e.Exception.Message, "VR Mogger", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.ExitThread();
                    };
                    bool minimized = Array.Exists(args, a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || a.Equals("/minimized", StringComparison.OrdinalIgnoreCase));
                    using (context = new TrayContext(minimized)) Application.Run(context);
                    return 0;
                }
                catch (Exception ex)
                {
                    if (context != null) context.RestoreAfterUnexpectedError();
                    MessageBox.Show("VR Mogger could not start.\n\n" + ex.Message, "VR Mogger", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}
