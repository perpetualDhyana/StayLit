using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("亮屏助手")]
[assembly: AssemblyDescription("自定义亮屏时长，到点立即息屏。")]
[assembly: AssemblyCompany("StayLit")]
[assembly: AssemblyProduct("亮屏助手")]
[assembly: AssemblyVersion("1.4.2.0")]
[assembly: AssemblyFileVersion("1.4.2.0")]

namespace StayLit
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length >= 2 && args[0] == "--preview")
                return RenderPreview(args[1], args.Length > 2 ? args[2] : "idle", args.Length > 3 ? args[3] : Theme.DefaultName);

            bool first;
            using (Mutex instance = new Mutex(true, "Local\\StayLit.1.Instance", out first))
            {
                if (!first)
                {
                    try
                    {
                        using (EventWaitHandle existing = EventWaitHandle.OpenExisting("Local\\StayLit.1.Show")) existing.Set();
                    }
                    catch (WaitHandleCannotBeOpenedException) { }
                    return 0;
                }
                try
                {
                    using (EventWaitHandle activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\StayLit.1.Show"))
                    {
                        MainForm form = null;
                        Session session = new Session(new WindowsPower(delegate { return form.Handle; }), new MonotonicClock());
                        SettingsStore store = SettingsStore.Default();
                        try
                        {
                            using (form = new MainForm(session, store.Load(), store, false,
                                delegate { return activation.WaitOne(0); }))
                                Application.Run(form);
                        }
                        finally { session.Dispose(); }
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show("亮屏助手未能继续运行：\n" + exception.Message,
                        "亮屏助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { instance.ReleaseMutex(); }
            }
            return 0;
        }

        private sealed class PreviewPower : IPowerController
        {
            public void Acquire() { }
            public void Release() { }
            public void TurnOffDisplay() { }
        }

        private sealed class PreviewClock : IClock
        {
            private readonly DateTime origin;
            public PreviewClock(DateTime origin) { this.origin = origin; }
            public long Milliseconds { get; set; }
            public DateTime UtcNow { get { return origin.AddMilliseconds(Milliseconds); } }
        }

        private static int RenderPreview(string path, string state, string theme)
        {
            // This path uses a fake power controller and never reads or writes
            // user settings. UI verification cannot affect the real display.
            long sampleMilliseconds = state == "always-long" ? (long)TimeSpan.FromHours(120).TotalMilliseconds + 125000 :
                state == "always" || state == "always-stopped" || state == "always-off" ? 5025000 : 0;
            PreviewClock clock = new PreviewClock(DateTime.UtcNow.AddMilliseconds(-sampleMilliseconds));
            using (Session session = new Session(new PreviewPower(), clock))
            using (MainForm form = new MainForm(session, new AppSettings { ThemeName = theme }, null, true, null))
            {
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-30000, -30000);
                form.Show();
                form.PreparePreview(state);
                clock.Milliseconds = sampleMilliseconds;
                if (state == "always-stopped") session.Stop();
                if (state == "always-off") session.TurnOffNow();
                form.RefreshPreview();
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.GetFullPath(path), ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }
    }
}
