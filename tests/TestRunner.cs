using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using StayLit;

namespace StayLit.Tests
{
    internal sealed class FakeClock : IClock
    {
        public long Now;
        public long Milliseconds { get { return Now; } }
        public TimeSpan WallOffset;
        public DateTime UtcNow { get { return new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc).AddMilliseconds(Now).Add(WallOffset); } }
    }

    internal sealed class FakePower : IPowerController
    {
        public readonly List<string> Calls = new List<string>();
        public bool FailAcquire;
        public bool FailRelease;
        public bool FailOff;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private void Record(string name)
        {
            if (Thread.CurrentThread.ManagedThreadId != thread) throw new Exception("Power calls changed thread.");
            Calls.Add(name);
        }
        public void Acquire() { Record("acquire"); if (FailAcquire) throw new InvalidOperationException("acquire failed"); }
        public void Release() { Record("release"); if (FailRelease) throw new InvalidOperationException("release failed"); }
        public void TurnOffDisplay() { Record("off"); if (FailOff) throw new InvalidOperationException("off failed"); }
    }

    internal static class TestRunner
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr window);
        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr window, IntPtr context);
        [DllImport("imm32.dll")]
        private static extern bool ImmGetOpenStatus(IntPtr context);
        private static int passed;
        private static int failed;
        private static readonly List<string> lines = new List<string>();

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Test("Countdown expires once, releases before screen-off", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartTimed(TimeSpan.FromMinutes(5));
                    Equal(300000L, session.RemainingMilliseconds);
                    clock.Now = 299999; session.Tick(); Equal(SessionMode.Timed, session.Mode);
                    Equal(1, power.Calls.Count);
                    clock.Now = 300000; session.Tick(); session.Tick();
                    Equal(SessionMode.Idle, session.Mode); Equal(SessionResult.Completed, session.Result);
                    Equal("acquire,release,off", String.Join(",", power.Calls));
                }
                Equal(3, power.Calls.Count);
            });
            Test("Cancel restores system control without screen-off", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartTimed(TimeSpan.FromHours(2)); session.Stop();
                    clock.Now = 99999999; session.Tick();
                    Equal("acquire,release", String.Join(",", power.Calls));
                    Equal(SessionResult.Stopped, session.Result);
                }
            });
            Test("Always-on has no expiry", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartAlways(); clock.Now = 1000000000; session.Tick();
                    Equal(SessionMode.Always, session.Mode); Equal(1, power.Calls.Count);
                }
                Equal("acquire,release", String.Join(",", power.Calls));
            });
            Test("Always-on counts monotonic elapsed time and freezes when stopped", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartAlways(); Equal(0L, session.AlwaysElapsedMilliseconds);
                    DateTime started = session.AlwaysStartedUtc.Value;
                    clock.Now = 5025123; Equal(5025123L, session.AlwaysElapsedMilliseconds);
                    clock.WallOffset = TimeSpan.FromHours(-12);
                    Equal(5025123L, session.AlwaysElapsedMilliseconds);
                    session.Stop(); DateTime ended = session.AlwaysEndedUtc.Value;
                    clock.Now += 60000;
                    Equal(5025123L, session.AlwaysElapsedMilliseconds);
                    Equal(started, session.AlwaysStartedUtc.Value); Equal(ended, session.AlwaysEndedUtc.Value);
                }
            });
            Test("Restarting always-on begins a fresh record", delegate
            {
                FakeClock clock = new FakeClock();
                using (Session session = new Session(new FakePower(), clock))
                {
                    session.StartAlways(); clock.Now = 60000; session.StartAlways();
                    Equal(0L, session.AlwaysElapsedMilliseconds);
                    Equal(false, session.AlwaysEndedUtc.HasValue);
                    Equal(clock.UtcNow, session.AlwaysStartedUtc.Value);
                    clock.Now += 2500; Equal(2500L, session.AlwaysElapsedMilliseconds);
                }
            });
            Test("Switching mode and immediate screen-off retain the always-on record", delegate
            {
                FakeClock clock = new FakeClock();
                using (Session session = new Session(new FakePower(), clock))
                {
                    session.StartAlways(); clock.Now = 90000;
                    session.StartTimed(TimeSpan.FromMinutes(1)); clock.Now += 1000;
                    Equal(90000L, session.AlwaysElapsedMilliseconds);
                    session.StartAlways(); clock.Now += 12345; session.TurnOffNow();
                    clock.Now += 60000; Equal(12345L, session.AlwaysElapsedMilliseconds);
                    Equal(SessionResult.DisplayOff, session.Result);
                }
            });
            Test("Always-on records survive settings round-trip without restarting power", delegate
            {
                FakeClock clock = new FakeClock(); FakePower restoredPower = new FakePower();
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-record-settings");
                string path = Path.Combine(folder, "settings.xml");
                SettingsStore store = new SettingsStore(path);
                using (Session session = new Session(new FakePower(), clock))
                {
                    session.StartAlways(); clock.Now = 3601000; session.Stop();
                    AppSettings settings = new AppSettings(); settings.CaptureRecord(session);
                    Equal(true, store.Save(settings));
                }
                using (Session restored = new Session(restoredPower, clock))
                {
                    store.Load().RestoreRecord(restored);
                    Equal(true, restored.HasAlwaysRecord);
                    Equal(3601000L, restored.AlwaysElapsedMilliseconds);
                    Equal(SessionMode.Idle, restored.Mode); Equal(0, restoredPower.Calls.Count);
                }
                File.Delete(path); Directory.Delete(folder);
            });
            Test("Invalid stored record is ignored", delegate
            {
                using (Session session = new Session(new FakePower(), new FakeClock()))
                {
                    AppSettings settings = new AppSettings { LastAlwaysMilliseconds = -1, LastAlwaysStartedUtc = "bad", LastAlwaysEndedUtc = "bad" };
                    settings.RestoreRecord(session); Equal(false, session.HasAlwaysRecord);
                }
            });
            Test("Restart replaces the old deadline", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartTimed(TimeSpan.FromMinutes(1)); clock.Now = 30000;
                    session.StartTimed(TimeSpan.FromMinutes(2)); clock.Now = 60000; session.Tick();
                    Equal(90000L, session.RemainingMilliseconds);
                    clock.Now = 150000; session.Tick();
                    Equal("acquire,acquire,release,off", String.Join(",", power.Calls));
                }
            });
            Test("Switching to always-on removes the old deadline", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartTimed(TimeSpan.FromSeconds(1)); session.StartAlways();
                    clock.Now = 60000; session.Tick(); Equal(SessionMode.Always, session.Mode);
                    Equal("acquire,acquire", String.Join(",", power.Calls));
                }
            });
            Test("Switching from always-on to countdown expires", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartAlways(); session.StartTimed(TimeSpan.FromSeconds(1));
                    clock.Now = 1000; session.Tick(); Equal(SessionResult.Completed, session.Result);
                }
            });
            Test("Invalid duration preserves an active session", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartAlways();
                    Throws(delegate { session.StartTimed(TimeSpan.Zero); });
                    Throws(delegate { session.StartTimed(TimeSpan.FromHours(101)); });
                    Equal(SessionMode.Always, session.Mode); Equal(1, power.Calls.Count);
                }
            });
            Test("Delayed timer tick still finishes exactly once", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartTimed(TimeSpan.FromSeconds(1)); clock.Now = 86400000;
                    Equal(0L, session.RemainingMilliseconds); session.Tick();
                    session.RefreshPowerRequest(); session.Tick();
                    Equal("acquire,release,off", String.Join(",", power.Calls));
                }
            });
            Test("A failed acquire never claims an active session", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower(); power.FailAcquire = true;
                using (Session session = new Session(power, clock))
                {
                    Throws(delegate { session.StartAlways(); });
                    Equal(false, session.IsActive); Equal(false, session.NeedsRelease);
                }
            });
            Test("Failed release can be retried without repeated screen-off", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartTimed(TimeSpan.FromSeconds(1)); power.FailRelease = true;
                    clock.Now = 1000; Throws(delegate { session.Tick(); }); session.Tick();
                    Equal(SessionResult.Failed, session.Result); Equal(true, session.NeedsRelease);
                    Equal("acquire,release", String.Join(",", power.Calls));
                    power.FailRelease = false; session.Stop(); Equal(false, session.NeedsRelease);
                }
            });
            Test("Failed screen-off is not retried after user resumes", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartTimed(TimeSpan.FromSeconds(1)); power.FailOff = true;
                    clock.Now = 1000; Throws(delegate { session.Tick(); }); session.Tick();
                    Equal(SessionResult.Failed, session.Result); Equal(false, session.NeedsRelease);
                    Equal("acquire,release,off", String.Join(",", power.Calls));
                }
            });
            Test("Exit releases its request without forcing screen-off", delegate
            {
                FakePower power = new FakePower(); Session session = new Session(power, new FakeClock());
                session.StartAlways(); session.Dispose(); session.Dispose();
                Equal("acquire,release", String.Join(",", power.Calls));
            });
            Test("Immediate screen-off works while idle", delegate
            {
                FakePower power = new FakePower();
                using (Session session = new Session(power, new FakeClock()))
                {
                    session.TurnOffNow();
                    Equal("off", String.Join(",", power.Calls));
                    Equal(SessionResult.DisplayOff, session.Result);
                    Equal(false, session.IsActive);
                }
            });
            Test("Immediate screen-off cancels the previous countdown", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartTimed(TimeSpan.FromMinutes(5));
                    session.TurnOffNow();
                    clock.Now = 300000; session.Tick(); session.RefreshPowerRequest();
                    Equal("acquire,release,off", String.Join(",", power.Calls));
                    Equal(SessionResult.DisplayOff, session.Result);
                    Equal(false, session.NeedsRelease);
                }
            });
            Test("Immediate screen-off releases always-on before powering off", delegate
            {
                FakePower power = new FakePower();
                using (Session session = new Session(power, new FakeClock()))
                {
                    session.StartAlways(); session.TurnOffNow(); session.RefreshPowerRequest();
                    Equal("acquire,release,off", String.Join(",", power.Calls));
                    Equal(false, session.IsActive);
                }
            });
            Test("A failed immediate screen-off never resumes the old countdown", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                {
                    session.StartTimed(TimeSpan.FromMinutes(5)); power.FailOff = true;
                    Throws(delegate { session.TurnOffNow(); });
                    clock.Now = 300000; session.Tick();
                    Equal(SessionResult.Failed, session.Result);
                    Equal("acquire,release,off", String.Join(",", power.Calls));
                }
            });
            Test("Immediate screen-off skips power-off if release fails", delegate
            {
                FakePower power = new FakePower();
                using (Session session = new Session(power, new FakeClock()))
                {
                    session.StartAlways(); power.FailRelease = true;
                    Throws(delegate { session.TurnOffNow(); });
                    Equal(false, session.IsActive); Equal(true, session.NeedsRelease);
                    Equal("acquire,release", String.Join(",", power.Calls));
                    power.FailRelease = false; session.Stop();
                }
            });
            Test("UI supports presets, apply, cancel, always-on and expiry", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                using (MainForm form = new MainForm(session, new AppSettings(), null, true, null))
                {
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-30000, -30000);
                    form.Show(); Application.DoEvents();
                    AssertCountdownFits(form);
                    FindButton(form, "5 分钟").PerformClick();
                    FindButton(form, "开始亮屏").PerformClick();
                    Equal(300000L, session.RemainingMilliseconds);
                    FindButton(form, "一直亮屏").PerformClick();
                    Equal(SessionMode.Timed, session.Mode);
                    FindButton(form, "开始亮屏").PerformClick();
                    Equal(SessionMode.Always, session.Mode);
                    FindButton(form, "停止").PerformClick();
                    Equal(false, session.IsActive); Equal(false, power.Calls.Contains("off"));
                    FindButton(form, "定时亮屏").PerformClick();
                    DurationNumber h = FindNumber(form, "小时数");
                    DurationNumber m = FindNumber(form, "分钟数");
                    h.Text = "2"; m.Text = "17";
                    FindButton(form, "开始亮屏").PerformClick();
                    Equal(8220000L, session.RemainingMilliseconds);
                    h.Value = 1; m.Value = 5;
                    FindButton(form, "开始亮屏").PerformClick();
                    Equal(3900000L, session.RemainingMilliseconds);
                    clock.Now = 3900000; session.Tick(); session.Tick();
                    Equal(1, power.Calls.FindAll(delegate(string call) { return call == "off"; }).Count);
                    form.Close();
                }
            });
            Test("UI rejects zero duration without a power request", delegate
            {
                FakePower power = new FakePower();
                using (Session session = new Session(power, new FakeClock()))
                using (MainForm form = new MainForm(session, new AppSettings(), null, true, null))
                {
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-30000, -30000);
                    form.Show(); Application.DoEvents();
                    FindNumber(form, "分钟数").Value = 0;
                    FindButton(form, "开始亮屏").PerformClick();
                    Equal(false, session.IsActive); Equal(0, power.Calls.Count);
                    form.Close();
                }
            });
            Test("Immediate screen-off button ends the active UI task", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                using (MainForm form = new MainForm(session, new AppSettings(), null, true, null))
                {
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-30000, -30000);
                    form.Show(); Application.DoEvents();
                    FindButton(form, "开始亮屏").PerformClick();
                    FindButton(form, "立即息屏").PerformClick();
                    Equal(SessionResult.DisplayOff, session.Result);
                    Equal(false, FindButton(form, "停止").Enabled);
                    Equal("acquire,release,off", String.Join(",", power.Calls));
                    clock.Now = 1800000; session.Tick();
                    Equal(3, power.Calls.Count);
                    form.Close();
                }
            });
            Test("Always-on UI shows elapsed time and keeps the stopped record", delegate
            {
                FakeClock clock = new FakeClock();
                using (Session session = new Session(new FakePower(), clock))
                using (MainForm form = new MainForm(session, new AppSettings(), null, true, null))
                {
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-30000, -30000);
                    form.Show(); Application.DoEvents();
                    FindButton(form, "一直亮屏").PerformClick();
                    Equal(false, FindNumber(form, "小时数").Visible);
                    Equal("00:00:00", FindControl(form, "sessionTime").Text);
                    FindButton(form, "开始亮屏").PerformClick(); clock.Now = 5025000;
                    form.RefreshPreview(); Equal("01:23:45", FindControl(form, "sessionTime").Text);
                    Equal("重新开始计时", FindButton(form, "开始亮屏").Text);
                    FindButton(form, "停止").PerformClick(); clock.Now += 60000;
                    form.RefreshPreview(); Equal("01:23:45", FindControl(form, "sessionTime").Text);
                    FindButton(form, "定时亮屏").PerformClick();
                    Equal(true, FindNumber(form, "小时数").Visible);
                    form.Close();
                }
            });
            Test("Theme switching preserves running task, inputs and elapsed record", delegate
            {
                FakeClock clock = new FakeClock(); FakePower power = new FakePower();
                using (Session session = new Session(power, clock))
                using (MainForm form = new MainForm(session, new AppSettings(), null, true, null))
                {
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-30000, -30000);
                    form.Show(); Application.DoEvents();
                    FindNumber(form, "小时数").Value = 2;
                    FindNumber(form, "分钟数").Value = 17;
                    FindButton(form, "一直亮屏").PerformClick();
                    FindButton(form, "开始亮屏").PerformClick(); clock.Now = 5025000;
                    foreach (string name in new string[] { "WindowsLight", "Paper", "Graphite" })
                    {
                        int calls = power.Calls.Count;
                        form.ChangeTheme(name);
                        Equal(name, Theme.Name); Equal(Theme.Background, form.BackColor);
                        Equal(Theme.Surface, FindNumber(form, "分钟数").BackColor);
                        Equal("01:23:45", FindControl(form, "sessionTime").Text);
                        Equal(SessionMode.Always, session.Mode); Equal(calls, power.Calls.Count);
                        Equal(2m, FindNumber(form, "小时数").Value);
                        Equal(17m, FindNumber(form, "分钟数").Value);
                    }
                    FindButton(form, "定时亮屏").PerformClick();
                    FindButton(form, "开始亮屏").PerformClick();
                    clock.Now += 5000;
                    long remaining = session.RemainingMilliseconds;
                    int before = power.Calls.Count;
                    form.ChangeTheme("Paper");
                    Equal(remaining, session.RemainingMilliseconds); Equal(before, power.Calls.Count);
                    Equal(5025000L, session.AlwaysElapsedMilliseconds);
                    form.Close();
                }
            });
            Test("Duration controls commit typed values and enforce step limits", delegate
            {
                using (Session session = new Session(new FakePower(), new FakeClock()))
                using (MainForm form = new MainForm(session, new AppSettings(), null, true, null))
                {
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-30000, -30000);
                    form.Show(); Application.DoEvents();
                    DurationNumber m = FindNumber(form, "分钟数");
                    DurationNumber h = FindNumber(form, "小时数");
                    Equal(false, FindButton(form, "减少小时").Enabled);
                    m.Text = "17";
                    FindButton(form, "增加分钟").PerformClick(); Equal(18m, m.Value);
                    FindButton(form, "减少分钟").PerformClick(); Equal(17m, m.Value);
                    m.Value = 59; Equal(false, FindButton(form, "增加分钟").Enabled);
                    m.UpButton(); Equal(59m, m.Value);
                    FindButton(form, "减少分钟").PerformClick(); Equal(58m, m.Value);
                    m.Value = 0; Equal(false, FindButton(form, "减少分钟").Enabled);
                    h.Value = 99; Equal(false, FindButton(form, "增加小时").Enabled);
                    FindButton(form, "减少小时").PerformClick(); Equal(98m, h.Value);
                    h.Text = "200";
                    FindButton(form, "开始亮屏").PerformClick(); Equal(99m, h.Value);
                    Equal(356400000L, session.RemainingMilliseconds);
                    form.Close();
                }
            });
            Test("Theme saves immediately and older settings retain the default", delegate
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-theme-settings");
                string path = Path.Combine(folder, "settings.xml");
                SettingsStore store = new SettingsStore(path);
                using (Session session = new Session(new FakePower(), new FakeClock()))
                using (MainForm form = new MainForm(session, new AppSettings(), store, true, null))
                {
                    form.ChangeTheme("Paper"); Equal("Paper", store.Load().ThemeName);
                    form.ChangeTheme("WindowsLight"); Equal("WindowsLight", store.Load().ThemeName);
                }
                File.WriteAllText(path, "<AppSettings><Hours>1</Hours><Minutes>15</Minutes></AppSettings>");
                Equal("WindowsLight", store.Load().ThemeName); Equal(1, store.Load().Hours);
                Equal(true, store.Save(new AppSettings { ThemeName = "unknown" }));
                Equal("WindowsLight", store.Load().ThemeName);
                Equal(true, store.Save(new AppSettings { ThemeName = "Graphite" }));
                Equal("Graphite", store.Load().ThemeName);
                File.Delete(path); Directory.Delete(folder);
            });
            Test("Numeric keyboard input bypasses IME composition and survives theme changes", delegate
            {
                using (Session session = new Session(new FakePower(), new FakeClock()))
                using (MainForm form = new MainForm(session, new AppSettings(), null, true, null))
                {
                    form.ImeMode = ImeMode.On;
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-30000, -30000);
                    form.Show(); Application.DoEvents();
                    Equal("WindowsLight", Theme.Name);
                    DurationNumber number = FindNumber(form, "小时数");
                    number.Focus(); number.Select(0, number.Text.Length);
                    TextBox editor = number;
                    Equal(ImeMode.Disable, editor.ImeMode);
                    IntPtr context = ImmGetContext(editor.Handle);
                    if (context != IntPtr.Zero)
                    {
                        // A closed native context may remain on Windows even
                        // though the control has disabled IME composition.
                        try { Equal(false, ImmGetOpenStatus(context)); }
                        finally { ImmReleaseContext(editor.Handle, context); }
                    }
                    SendMessage(editor.Handle, 0x0102, new IntPtr((int)'8'), IntPtr.Zero);
                    Application.DoEvents(); form.RefreshPreview();
                    Equal(8m, number.Value); Equal("8", editor.Text);
                    SendMessage(editor.Handle, 0x0100, new IntPtr((int)Keys.Up), IntPtr.Zero);
                    SendMessage(editor.Handle, 0x0101, new IntPtr((int)Keys.Up), IntPtr.Zero);
                    Equal(9m, number.Value);
                    form.ChangeTheme("Graphite"); form.ChangeTheme("WindowsLight");
                    Equal(9m, number.Value); Equal(ImeMode.Disable, editor.ImeMode);
                    FindButton(form, "减少小时").PerformClick(); Equal(8m, number.Value);
                    FindButton(form, "开始亮屏").PerformClick();
                    Equal(30600000L, session.RemainingMilliseconds);
                    form.Close();
                }
            });
            Test("Display recovery and repeated reopen repaint the entire numeric field", delegate
            {
                FakePower power = new FakePower();
                using (Session session = new Session(power, new FakeClock()))
                using (MainForm form = new MainForm(session, new AppSettings(), null, true, null))
                {
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-30000, -30000);
                    form.Show(); Application.DoEvents();
                    FindButton(form, "5 分钟").PerformClick();
                    for (int iteration = 0; iteration < 4; iteration++)
                    {
                        FindButton(form, "开始亮屏").PerformClick();
                        FindButton(form, "立即息屏").PerformClick();
                        form.Hide();
                        SendMessage(form.Handle, 0x007E, new IntPtr(32), IntPtr.Zero);
                        SendMessage(form.Handle, 0x031A, IntPtr.Zero, IntPtr.Zero);
                        form.ShowWindow(); Application.DoEvents();
                        foreach (DurationNumber number in new DurationNumber[] { FindNumber(form, "小时数"), FindNumber(form, "分钟数") })
                        {
                            typeof(Control).GetMethod("RecreateHandle", System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic).Invoke(number, null);
                            number.Refresh();
                            using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(number.Width, number.Height))
                            {
                                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                                    graphics.Clear(System.Drawing.Color.Magenta);
                                number.DrawToBitmap(bitmap, number.ClientRectangle);
                                // This is the strip occupied by the old hidden native arrows.
                                for (int x = bitmap.Width - 16; x < bitmap.Width - 1; x++)
                                    for (int y = 2; y < bitmap.Height - 2; y++)
                                        Equal(number.BackColor.ToArgb(), bitmap.GetPixel(x, y).ToArgb());
                            }
                        }
                        Equal(0m, FindNumber(form, "小时数").Value);
                        Equal(5m, FindNumber(form, "分钟数").Value);
                        FindNumber(form, "分钟数").UpButton();
                        Equal(6m, FindNumber(form, "分钟数").Value);
                        FindNumber(form, "分钟数").DownButton();
                    }
                    Equal(4, power.Calls.FindAll(delegate(string call) { return call == "off"; }).Count);
                    form.Close();
                }
            });
            Test("Numeric text validation handles pasted, empty and invalid values", delegate
            {
                using (DurationNumber number = new DurationNumber())
                {
                    number.Maximum = 59; number.Value = 15;
                    number.Text = " 42 "; Equal(42m, number.Value); Equal("42", number.Text);
                    number.Text = "invalid"; Equal(42m, number.Value); Equal("42", number.Text);
                    number.Text = ""; Equal(42m, number.Value);
                    number.Text = "-3"; Equal(0m, number.Value);
                    number.Text = "200"; Equal(59m, number.Value);
                    number.DownButton(); Equal(58m, number.Value);
                    number.Text = "999999999999999999999999999999999999999999";
                    Equal(58m, number.Value);
                }
            });
            Test("Settings round-trip and corruption fallback", delegate
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-settings");
                string path = Path.Combine(folder, "settings.xml");
                SettingsStore store = new SettingsStore(path);
                Equal(true, store.Save(new AppSettings { Hours = 2, Minutes = 45, AlwaysMode = true }));
                AppSettings loaded = store.Load();
                Equal(2, loaded.Hours); Equal(45, loaded.Minutes); Equal(true, loaded.AlwaysMode);
                File.WriteAllText(path, "broken");
                Equal(30, store.Load().Minutes);
                File.Delete(path); Directory.Delete(folder);
            });

            string summary = passed + " passed, " + failed + " failed. No real display actions performed.";
            Console.WriteLine(summary); lines.Add(summary);
            if (args.Length > 0) File.WriteAllLines(args[0], lines.ToArray());
            return failed == 0 ? 0 : 1;
        }

        private static RoundedButton FindButton(Control parent, string accessibleName)
        {
            foreach (Control control in parent.Controls)
            {
                RoundedButton button = control as RoundedButton;
                if (button != null && button.AccessibleName == accessibleName) return button;
                RoundedButton child = FindButton(control, accessibleName);
                if (child != null) return child;
            }
            return null;
        }

        private static Control FindControl(Control parent, string name)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Name == name) return control;
                Control child = FindControl(control, name);
                if (child != null) return child;
            }
            return null;
        }

        private static DurationNumber FindNumber(Control parent, string name)
        {
            foreach (Control control in parent.Controls)
            {
                DurationNumber number = control as DurationNumber;
                if (number != null && number.AccessibleName == name) return number;
                DurationNumber child = FindNumber(control, name);
                if (child != null) return child;
            }
            return null;
        }

        private static void AssertCountdownFits(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.AccessibleName == "剩余亮屏时间")
                {
                    if (control.Right > parent.ClientSize.Width || control.Bottom > parent.ClientSize.Height)
                        throw new Exception("Countdown overflows its card at the current Windows DPI.");
                    int centerDifference = Math.Abs(control.Left * 2 + control.Width - parent.ClientSize.Width);
                    if (centerDifference > 3) throw new Exception("Countdown is not centered at the current Windows DPI.");
                }
                AssertCountdownFits(control);
            }
        }

        private static void Test(string name, Action action)
        {
            try { action(); passed++; Console.WriteLine("PASS " + name); lines.Add("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); lines.Add("FAIL " + name + ": " + error); }
        }
        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception("Expected " + expected + ", got " + actual);
        }
        private static void Throws(Action action)
        {
            bool thrown = false;
            try { action(); } catch { thrown = true; }
            if (!thrown) throw new Exception("Expected an exception.");
        }
    }
}
