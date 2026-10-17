using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace StayLit
{
    internal sealed class RoundedPanel : Panel
    {
        public Color BorderColor = Theme.Border;
        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Branding.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 14))
            using (Brush brush = new SolidBrush(BackColor))
            using (Pen border = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }

    internal sealed class RoundedButton : Button
    {
        private Color fillColor = Theme.Surface;
        private Color inkColor = Theme.Ink;
        private Color lineColor = Theme.Border;
        public Color FillColor { get { return fillColor; } set { if (fillColor != value) { fillColor = value; Invalidate(); } } }
        public Color InkColor { get { return inkColor; } set { if (inkColor != value) { inkColor = value; Invalidate(); } } }
        public Color LineColor { get { return lineColor; } set { if (lineColor != value) { lineColor = value; Invalidate(); } } }
        private bool hovered;
        private bool pressed;
        public int StepDirection;
        public bool ShowChevron;
        public RoundedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Enabled ? FillColor : Theme.DisabledFill;
            if (Enabled && (hovered || pressed)) fill = Theme.Hover(fill, pressed);
            using (GraphicsPath path = Branding.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 9))
            using (Brush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(Enabled ? LineColor : Theme.Border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
            if (StepDirection != 0)
            {
                float scale = e.Graphics.DpiX / 96f;
                float x = Width / 2f, y = Height / 2f, radius = 4 * scale;
                using (Pen glyph = new Pen(Enabled ? InkColor : Theme.DisabledInk, 1.5f * scale))
                {
                    glyph.StartCap = glyph.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(glyph, x - radius, y, x + radius, y);
                    if (StepDirection > 0) e.Graphics.DrawLine(glyph, x, y - radius, x, y + radius);
                }
            }
            else TextRenderer.DrawText(e.Graphics, Text, Font,
                ShowChevron ? new Rectangle(0, 0, Width - (int)(24 * e.Graphics.DpiX / 96f), Height) : ClientRectangle,
                Enabled ? InkColor : Theme.DisabledInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (ShowChevron)
            {
                float scale = e.Graphics.DpiX / 96f;
                float x = Width - 15 * scale, y = Height / 2f;
                using (Pen glyph = new Pen(Enabled ? InkColor : Theme.DisabledInk, 1.25f * scale))
                    e.Graphics.DrawLines(glyph, new PointF[] { new PointF(x - 3 * scale, y - scale),
                        new PointF(x, y + 2 * scale), new PointF(x + 3 * scale, y - scale) });
            }
            if (Focused && ShowFocusCues)
            {
                using (GraphicsPath focus = Branding.Rounded(new RectangleF(4, 4, Width - 9, Height - 9), 6))
                using (Pen focusPen = new Pen(InkColor, 2f))
                    e.Graphics.DrawPath(focusPen, focus);
            }
        }
    }

    internal sealed class ProgressLine : Control
    {
        private double fraction;
        public double Fraction
        {
            get { return fraction; }
            set
            {
                double next = Math.Max(0, Math.Min(1, value));
                if ((int)(Width * fraction) == (int)(Width * next)) { fraction = next; return; }
                fraction = next;
                Invalidate();
            }
        }
        public ProgressLine()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint, true);
            TabStop = false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Border);
            using (Brush brush = new SolidBrush(Theme.Accent))
                e.Graphics.FillRectangle(brush, 0, 0, (float)(Width * fraction), Height);
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Session session;
        private readonly SettingsStore store;
        private readonly bool preview;
        private readonly Func<bool> activationRequested;
        private readonly Timer timer;
        private NotifyIcon tray;
        private ToolStripMenuItem trayStatus;
        private ToolStripMenuItem trayStop;
        private RoundedButton timedMode;
        private RoundedButton alwaysMode;
        private RoundedButton start;
        private RoundedButton stop;
        private RoundedButton screenOff;
        private readonly ToolTip toolTip = new ToolTip();
        private RoundedButton[] presets;
        private DurationNumber hours;
        private DurationNumber minutes;
        private Label status;
        private Label countdown;
        private Label detail;
        private Label notice;
        private Label durationHeading;
        private Label endBehavior;
        private Panel durationPanel;
        private RoundedButton themeButton;
        private ContextMenuStrip themeMenu;
        private PictureBox logo;
        private RoundedPanel recordPanel;
        private Label recordStarted;
        private Label recordEnded;
        private ProgressLine progress;
        private long lastRecordCheckpoint;
        private bool selectedAlways;
        private bool building = true;
        private bool exiting;
        private string error;
        private bool settingsWarning;

        public MainForm(Session session, AppSettings settings, SettingsStore store,
            bool preview, Func<bool> activationRequested)
        {
            this.session = session;
            this.store = store;
            this.preview = preview;
            this.activationRequested = activationRequested;
            Theme.Select(settings.ThemeName);
            settings.RestoreRecord(session);
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 10f);
            BackColor = Theme.Background;
            ForeColor = Theme.Ink;
            Text = "亮屏助手";
            ClientSize = new Size(560, 652);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            using (System.IO.Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("StayLit.ico"))
                if (stream != null) using (Icon embedded = new Icon(stream)) Icon = (Icon)embedded.Clone();

            logo = new PictureBox();
            logo.Bounds = new Rectangle(24, 22, 40, 40);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Image = Branding.DrawIcon(88, Theme.Accent);
            logo.AccessibleName = "亮屏助手图标";
            Controls.Add(logo);
            Label title = AddLabel(this, "亮屏助手", 76, 17, 300, 34, 18f, FontStyle.Bold, TextTone.Ink);
            title.AccessibleRole = AccessibleRole.StaticText;
            AddLabel(this, "常亮 · 定时 · 一键息屏", 77, 53, 360, 22, 9f, FontStyle.Regular, TextTone.Muted);
            themeButton = AddButton(this, "主题 · " + Theme.DisplayName, 380, 22, 156, 32);
            themeButton.Font = new Font("Microsoft YaHei UI", 9f);
            themeButton.AccessibleName = "切换主题";
            themeButton.ShowChevron = true;
            themeButton.Click += delegate { themeMenu.Show(themeButton, new Point(0, themeButton.Height + 4)); };
            CreateThemeMenu();
            Label version = AddLabel(this, "v1.4.2", 460, 58, 76, 22, 8.5f, FontStyle.Regular, TextTone.Muted);
            version.TextAlign = ContentAlignment.MiddleRight;

            timedMode = AddButton(this, "定时亮屏", 24, 94, 250, 40);
            alwaysMode = AddButton(this, "一直亮屏", 286, 94, 250, 40);
            timedMode.Click += delegate { SelectMode(false); };
            alwaysMode.Click += delegate { SelectMode(true); };

            RoundedPanel card = new RoundedPanel();
            card.Name = "sessionCard";
            card.Bounds = new Rectangle(24, 150, 512, 176);
            card.BackColor = card.BorderColor = Theme.Hero;
            Controls.Add(card);
            status = AddLabel(card, "准备就绪", 20, 14, 472, 22, 9.5f, FontStyle.Bold, TextTone.Highlight);
            status.TextAlign = ContentAlignment.MiddleCenter;
            countdown = AddLabel(card, "00:30:00", 12, 42, 488, 74, 42f, FontStyle.Regular, TextTone.Ink);
            countdown.Font = new Font("Segoe UI", 42f, FontStyle.Regular);
            // Changing font families after parenting can DPI-scale a Label's
            // size once before its parent scales. Restore design bounds first.
            countdown.Bounds = new Rectangle(12, 42, 488, 74);
            countdown.TextAlign = ContentAlignment.MiddleCenter;
            countdown.AccessibleName = "剩余亮屏时间";
            countdown.Name = "sessionTime";
            detail = AddLabel(card, "开始后持续亮屏，到点立即息屏", 16, 120, 480, 24, 9f, FontStyle.Regular, TextTone.HeroMuted);
            detail.TextAlign = ContentAlignment.MiddleCenter;
            progress = new ProgressLine();
            progress.Bounds = new Rectangle(24, 156, 464, 4);
            card.Controls.Add(progress);

            durationHeading = AddLabel(this, "亮屏时长", 24, 344, 200, 24, 10.5f, FontStyle.Bold, TextTone.Ink);
            endBehavior = AddLabel(this, "到点自动息屏", 310, 344, 226, 24, 9f, FontStyle.Regular, TextTone.Muted);
            endBehavior.TextAlign = ContentAlignment.MiddleRight;
            durationPanel = new Panel();
            durationPanel.Bounds = new Rectangle(24, 378, 512, 64);
            durationPanel.BackColor = Theme.Background;
            Controls.Add(durationPanel);
            DurationField hourField = new DurationField("小时", "小时数", 99);
            hourField.Location = new Point(0, 0);
            durationPanel.Controls.Add(hourField);
            DurationField minuteField = new DurationField("分钟", "分钟数", 59);
            minuteField.Location = new Point(264, 0);
            durationPanel.Controls.Add(minuteField);
            hours = hourField.Number;
            minutes = minuteField.Number;
            hours.Value = settings.Hours;
            minutes.Value = settings.Minutes;
            hours.ValueChanged += delegate { if (!building) RefreshView(); };
            minutes.ValueChanged += delegate { if (!building) RefreshView(); };

            int[] presetMinutes = { 5, 15, 30, 60, 120 };
            string[] presetNames = { "5 分钟", "15 分钟", "30 分钟", "1 小时", "2 小时" };
            presets = new RoundedButton[presetMinutes.Length];
            for (int i = 0; i < presetMinutes.Length; i++)
            {
                int durationMinutes = presetMinutes[i];
                presets[i] = AddButton(this, presetNames[i], 24 + i * 104, 454, 96, 32);
                presets[i].Font = new Font(Font.FontFamily, 9f);
                presets[i].Click += delegate { SetDuration(durationMinutes); };
            }

            recordPanel = new RoundedPanel();
            recordPanel.Bounds = new Rectangle(24, 378, 512, 108);
            recordPanel.AccessibleName = "常亮记录";
            Controls.Add(recordPanel);
            AddLabel(recordPanel, "开始时间", 20, 14, 226, 20, 9f, FontStyle.Regular, TextTone.Muted);
            AddLabel(recordPanel, "结束时间", 272, 14, 220, 20, 9f, FontStyle.Regular, TextTone.Muted);
            recordStarted = AddLabel(recordPanel, "尚未开始", 20, 38, 226, 28, 12f, FontStyle.Bold, TextTone.Ink);
            recordStarted.AccessibleName = "常亮开始时间";
            recordEnded = AddLabel(recordPanel, "尚无记录", 272, 38, 220, 28, 12f, FontStyle.Bold, TextTone.Ink);
            recordEnded.AccessibleName = "常亮结束时间";
            AddLabel(recordPanel, "记录仅保存在本机，每次开始会更新最近记录。", 20, 78, 472, 20, 8.5f, FontStyle.Regular, TextTone.Muted);

            start = AddButton(this, "开始亮屏", 24, 506, 330, 44);
            start.FillColor = start.LineColor = Theme.Accent;
            start.InkColor = Color.White;
            stop = AddButton(this, "停止", 366, 506, 170, 44);
            start.Click += delegate { StartSelected(); };
            stop.Click += delegate { RunAction(delegate { session.Stop(); }); };
            screenOff = AddButton(this, "立即息屏", 24, 564, 154, 40);
            screenOff.FillColor = Theme.Raised;
            screenOff.LineColor = Theme.Border;
            screenOff.InkColor = Theme.Ink;
            screenOff.Click += delegate { TurnOffImmediately(); };
            AddLabel(this, "结束当前亮屏任务\n并立即关闭屏幕", 194, 565, 342, 40, 9f, FontStyle.Regular, TextTone.Muted);
            notice = AddLabel(this, "关闭窗口后在托盘运行 · 右键图标可退出", 24, 620, 512, 20, 8.5f, FontStyle.Regular, TextTone.Muted);
            toolTip.SetToolTip(stop, "提前结束亮屏任务，恢复 Windows 原有规则。");
            toolTip.SetToolTip(screenOff, "立即结束当前任务并息屏；按键或移动鼠标可以重新亮屏。");
            toolTip.OwnerDraw = true;
            toolTip.Draw += delegate(object sender, DrawToolTipEventArgs e)
            {
                using (Brush background = new SolidBrush(Theme.Raised)) e.Graphics.FillRectangle(background, e.Bounds);
                using (Pen border = new Pen(Theme.Border))
                    e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, e.Font, e.Bounds, Theme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            selectedAlways = settings.AlwaysMode;
            building = false;
            if (!preview) CreateTray();
            timer = new Timer();
            timer.Interval = 250;
            timer.Tick += delegate
            {
                try { session.Tick(); }
                catch (Exception exception) { error = exception.Message; }
                if (session.Mode == SessionMode.Always && session.AlwaysElapsedMilliseconds - lastRecordCheckpoint >= 60000)
                    SaveSettings();
                if (activationRequested != null && activationRequested()) ShowWindow();
                RefreshView();
            };
            if (!preview) timer.Start();
            Resize += delegate { if (!preview && WindowState == FormWindowState.Minimized) Hide(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape && !preview) { Hide(); e.Handled = true; }
            };
            ResumeLayout(true);
            ApplyTheme();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (preview) return;
            Rectangle available = Screen.FromControl(this).WorkingArea;
            if (Height > available.Height - 24)
            {
                AutoScroll = true;
                Height = Math.Max(360, available.Height - 24);
                Top = available.Top + 12;
            }
        }

        private void CreateThemeMenu()
        {
            themeMenu = new ContextMenuStrip();
            themeMenu.Font = new Font("Microsoft YaHei UI", 9.5f);
            themeMenu.Renderer = new AppMenuRenderer();
            string[] names = { "WindowsLight", "Paper", "Graphite" };
            string[] labels = { "Windows 浅色", "纸白", "石墨黑" };
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                ToolStripMenuItem item = new ToolStripMenuItem(labels[i]);
                item.Tag = name;
                item.Click += delegate { ChangeTheme(name); };
                themeMenu.Items.Add(item);
            }
        }

        internal void ChangeTheme(string name)
        {
            Theme.Select(name);
            ApplyTheme();
            SaveSettings();
            RefreshView();
        }

        private void ApplyTheme()
        {
            SuspendLayout();
            BackColor = Theme.Background;
            ForeColor = Theme.Ink;
            ApplyControlTheme(this);
            start.FillColor = start.LineColor = Theme.Accent;
            start.InkColor = Color.White;
            screenOff.FillColor = Theme.Raised;
            themeButton.Text = "主题 · " + Theme.DisplayName;
            foreach (ToolStripMenuItem item in themeMenu.Items)
                item.Checked = (string)item.Tag == Theme.Name;
            ApplyMenuTheme(themeMenu);
            if (tray != null) ApplyMenuTheme(tray.ContextMenuStrip);
            Image old = logo.Image;
            logo.Image = Branding.DrawIcon(88, Theme.Accent);
            if (old != null) old.Dispose();
            if (IsHandleCreated) NativeAppearance.Apply(Handle);
            ResumeLayout(true);
            RefreshView();
            Invalidate(true);
        }

        private static void ApplyControlTheme(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                DurationField field = control as DurationField;
                if (field != null) { field.ApplyTheme(); continue; }
                RoundedPanel panel = control as RoundedPanel;
                RoundedButton button = control as RoundedButton;
                ThemedLabel label = control as ThemedLabel;
                if (panel != null)
                {
                    panel.BackColor = panel.Name == "sessionCard" ? Theme.Hero : Theme.Surface;
                    panel.BorderColor = panel.Name == "sessionCard" ? Theme.Hero : Theme.Border;
                }
                else if (button != null)
                {
                    button.FillColor = Theme.Surface;
                    button.InkColor = Theme.Ink;
                    button.LineColor = Theme.Border;
                }
                else control.BackColor = parent.BackColor;
                if (label != null) label.ForeColor = Theme.TextColor(label.Tone);
                ApplyControlTheme(control);
            }
        }

        private static void ApplyMenuTheme(ToolStripDropDown menu)
        {
            menu.BackColor = Theme.Surface;
            menu.ForeColor = Theme.Ink;
            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = Theme.Surface;
                item.ForeColor = Theme.Ink;
                ToolStripMenuItem submenu = item as ToolStripMenuItem;
                if (submenu != null && submenu.HasDropDownItems)
                {
                    submenu.DropDown.Renderer = menu.Renderer;
                    ApplyMenuTheme(submenu.DropDown);
                }
            }
            menu.Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeAppearance.Apply(Handle);
        }

        private Label AddLabel(Control parent, string text, int x, int y, int width, int height,
            float size, FontStyle style, TextTone tone)
        {
            ThemedLabel label = new ThemedLabel();
            label.Tone = tone;
            label.Text = text;
            label.Bounds = new Rectangle(x, y, width, height);
            label.Font = new Font("Microsoft YaHei UI", size, style);
            label.ForeColor = Theme.TextColor(tone);
            label.BackColor = parent.BackColor;
            parent.Controls.Add(label);
            return label;
        }

        private RoundedButton AddButton(Control parent, string text, int x, int y, int width, int height)
        {
            RoundedButton button = new RoundedButton();
            button.Text = text;
            button.Bounds = new Rectangle(x, y, width, height);
            button.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            button.AccessibleName = text;
            parent.Controls.Add(button);
            return button;
        }

        private void SelectMode(bool always)
        {
            selectedAlways = always;
            error = null;
            RefreshView();
        }

        private void SetDuration(int totalMinutes)
        {
            hours.Value = totalMinutes / 60;
            minutes.Value = totalMinutes % 60;
            SelectMode(false);
        }

        private TimeSpan SelectedDuration()
        {
            // Include any value just typed before applying a duration.
            ValidateChildren();
            return TimeSpan.FromHours((double)hours.Value) + TimeSpan.FromMinutes((double)minutes.Value);
        }

        private void StartSelected()
        {
            RunAction(delegate
            {
                if (selectedAlways) session.StartAlways();
                else
                {
                    TimeSpan duration = SelectedDuration();
                    if (duration.TotalSeconds < 1)
                        throw new InvalidOperationException("请先设置大于 0 的亮屏时长。");
                    session.StartTimed(duration);
                }
            });
        }

        private void TurnOffImmediately()
        {
            RunAction(delegate { session.TurnOffNow(); });
        }

        private void RunAction(Action action)
        {
            error = null;
            try { action(); }
            catch (Exception exception) { error = exception.Message; }
            SaveSettings();
            RefreshView();
        }

        private void SaveSettings()
        {
            if (store == null) return;
            AppSettings settings = new AppSettings
            {
                Hours = (int)hours.Value,
                Minutes = (int)minutes.Value,
                AlwaysMode = selectedAlways,
                ThemeName = Theme.Name
            };
            settings.CaptureRecord(session);
            settingsWarning = !store.Save(settings);
            lastRecordCheckpoint = session.AlwaysElapsedMilliseconds;
        }

        private static string FormatDuration(long milliseconds, bool elapsed = false)
        {
            long seconds = elapsed ? milliseconds / 1000 : (long)Math.Ceiling(milliseconds / 1000d);
            return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                seconds / 3600, seconds / 60 % 60, seconds % 60);
        }

        private static string FormatRecordTime(DateTime? utc)
        {
            if (!utc.HasValue) return "尚未开始";
            DateTime local = utc.Value.ToLocalTime();
            return local.ToString(local.Date == DateTime.Today ? "今天 HH:mm:ss" : "MM/dd HH:mm:ss");
        }

        private void RefreshView()
        {
            if (building) return;
            timedMode.FillColor = !selectedAlways ? Theme.Soft : Theme.Surface;
            timedMode.LineColor = !selectedAlways ? Theme.Accent : Theme.Border;
            timedMode.InkColor = !selectedAlways ? Theme.AccentText : Theme.Muted;
            alwaysMode.FillColor = selectedAlways ? Theme.Soft : Theme.Surface;
            alwaysMode.LineColor = selectedAlways ? Theme.Accent : Theme.Border;
            alwaysMode.InkColor = selectedAlways ? Theme.AccentText : Theme.Muted;
            durationPanel.Visible = !selectedAlways;
            recordPanel.Visible = selectedAlways;
            durationHeading.Text = selectedAlways ? "常亮记录" : "亮屏时长";
            endBehavior.Text = selectedAlways ? "保留最近一次" : "到点自动息屏";
            int selectedMinutes = (int)hours.Value * 60 + (int)minutes.Value;
            int[] amounts = { 5, 15, 30, 60, 120 };
            for (int i = 0; i < presets.Length; i++)
            {
                presets[i].Visible = !selectedAlways;
                presets[i].FillColor = !selectedAlways && selectedMinutes == amounts[i] ? Theme.Soft : Theme.Surface;
                presets[i].LineColor = !selectedAlways && selectedMinutes == amounts[i] ? Theme.Accent : Theme.Border;
                presets[i].InkColor = !selectedAlways && selectedMinutes == amounts[i] ? Theme.AccentText : Theme.Muted;
            }
            recordStarted.Text = FormatRecordTime(session.AlwaysStartedUtc);
            recordEnded.Text = session.Mode == SessionMode.Always ? "记录中" :
                session.HasAlwaysRecord ? FormatRecordTime(session.AlwaysEndedUtc) : "尚无记录";
            recordEnded.ForeColor = Theme.Ink;
            stop.Enabled = session.IsActive || session.NeedsRelease;
            start.Text = session.IsActive ? (selectedAlways ?
                session.Mode == SessionMode.Always ? "重新开始计时" : "切换为一直亮屏" : "应用并重新计时") : "开始亮屏";
            progress.Visible = session.Mode == SessionMode.Timed || (session.Mode == SessionMode.Idle && !selectedAlways);
            countdown.AccessibleName = session.Mode == SessionMode.Always || (!session.IsActive && selectedAlways) ?
                "本次常亮时长" : "剩余亮屏时间";
            string trayText;
            if (session.Mode == SessionMode.Timed)
            {
                long remaining = session.RemainingMilliseconds;
                status.Text = "正在保持亮屏";
                countdown.Text = FormatDuration(remaining);
                DateTime endsAt = DateTime.Now.AddMilliseconds(remaining);
                detail.Text = "预计 " + endsAt.ToString(endsAt.Date == DateTime.Today ? "HH:mm:ss" : "MM/dd HH:mm:ss") + " 息屏";
                progress.Fraction = remaining / (double)session.DurationMilliseconds;
                trayText = "亮屏助手 · 剩余 " + countdown.Text;
            }
            else if (session.Mode == SessionMode.Always)
            {
                status.Text = "本次已常亮";
                countdown.Text = FormatDuration(session.AlwaysElapsedMilliseconds, true);
                detail.Text = "正向计时，直到你停止或息屏";
                trayText = "亮屏助手 · 已常亮 " + countdown.Text;
            }
            else if (selectedAlways)
            {
                status.Text = session.HasAlwaysRecord ? "上次常亮时长" : "准备开始常亮";
                countdown.Text = FormatDuration(session.AlwaysElapsedMilliseconds, true);
                detail.Text = session.HasAlwaysRecord ? "本次任务已结束，可以开始新的常亮任务" : "点击开始，自动记录本次常亮时长";
                trayText = "亮屏助手 · " + (session.HasAlwaysRecord ? "上次常亮 " + countdown.Text : "准备就绪");
            }
            else
            {
                bool completed = session.Result == SessionResult.Completed;
                bool manuallyOff = session.Result == SessionResult.DisplayOff;
                status.Text = manuallyOff ? "已立即息屏" : completed ? "倒计时已结束" : session.Result == SessionResult.Stopped ? "已停止" : "准备就绪";
                countdown.Text = completed || manuallyOff ? "00:00:00" : FormatDuration(selectedMinutes * 60000L);
                detail.Text = completed || manuallyOff ? "按键或移动鼠标，即可重新亮屏" : "开始后持续亮屏，到点立即息屏";
                progress.Fraction = 0;
                trayText = "亮屏助手 · " + status.Text;
            }
            if (error != null)
            {
                status.Text = "操作未完成";
                status.ForeColor = Theme.Error;
            }
            else status.ForeColor = Theme.Highlight;
            notice.Text = error ?? (settingsWarning ? "记录未能保存，请检查本地设置目录是否可写。" :
                session.Mode == SessionMode.Always ? "关闭窗口后继续计时 · 停止或息屏会保留本次记录" :
                session.IsActive ? "关闭窗口后在托盘运行 · 修改模式或时长后需点击应用" :
                session.Result == SessionResult.Stopped && !selectedAlways ? "已交还系统控制；停止按钮不会强制息屏。" :
                "关闭窗口后在托盘运行 · 右键图标可退出");
            notice.ForeColor = error != null ? Theme.Error : Theme.Muted;
            if (tray != null)
            {
                if (tray.Text != trayText) tray.Text = trayText;
                trayStatus.Text = trayText;
                trayStop.Enabled = stop.Enabled;
            }
        }

        private void CreateTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            menu.Renderer = new AppMenuRenderer();
            menu.BackColor = Theme.Surface;
            menu.ForeColor = Theme.Ink;
            menu.ShowImageMargin = false;
            trayStatus = new ToolStripMenuItem("亮屏助手");
            trayStatus.Enabled = false;
            menu.Items.Add(trayStatus);
            menu.Items.Add("打开亮屏助手", null, delegate { ShowWindow(); });
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem quick = new ToolStripMenuItem("定时亮屏");
            int[] durations = { 5, 15, 30, 60, 120 };
            string[] names = { "5 分钟", "15 分钟", "30 分钟", "1 小时", "2 小时" };
            for (int i = 0; i < durations.Length; i++)
            {
                int duration = durations[i];
                quick.DropDownItems.Add(names[i], null, delegate { SetDuration(duration); StartSelected(); });
            }
            menu.Items.Add(quick);
            menu.Items.Add("一直亮屏", null, delegate { SelectMode(true); StartSelected(); });
            trayStop = new ToolStripMenuItem("停止亮屏", null, delegate { RunAction(delegate { session.Stop(); }); });
            menu.Items.Add(trayStop);
            menu.Items.Add("立即息屏", null, delegate { TurnOffImmediately(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitApplication(); });
            tray = new NotifyIcon();
            tray.Icon = Icon;
            tray.Text = "亮屏助手";
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowWindow();
            };
            tray.Visible = true;
        }

        public void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            RefreshView();
            Refresh();
        }

        private void ExitApplication()
        {
            RunAction(delegate { session.Stop(); });
            if (session.NeedsRelease) { ShowWindow(); return; }
            exiting = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!preview && !exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                SaveSettings();
                Hide();
                return;
            }
            timer.Stop();
            if (tray != null) tray.Visible = false;
            try { session.Dispose(); } catch { /* The owning thread is exiting, clearing its request. */ }
            SaveSettings();
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message message)
        {
            const int PowerBroadcast = 0x0218;
            const int ResumeAutomatic = 0x0012;
            const int ResumeSuspend = 0x0007;
            if (!preview && message.Msg == PowerBroadcast && session != null &&
                (message.WParam.ToInt32() == ResumeAutomatic || message.WParam.ToInt32() == ResumeSuspend))
            {
                // Defer until after Windows has completed its resume message.
                BeginInvoke((Action)delegate { RunAction(delegate { session.RefreshPowerRequest(); }); });
            }
            base.WndProc(ref message);
        }

        internal void PreparePreview(string state)
        {
            if (state == "always" || state == "always-stopped" || state == "always-off" || state == "always-long")
            {
                SelectMode(true);
                session.StartAlways();
            }
            else if (state == "always-ready") SelectMode(true);
            else if (state == "timed") session.StartTimed(TimeSpan.FromMinutes(30));
            else if (state == "off") session.TurnOffNow();
            else if (state == "focus") minutes.Focus();
            RefreshView();
        }

        internal void RefreshPreview() { RefreshView(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null) timer.Dispose();
                toolTip.Dispose();
                if (themeMenu != null) themeMenu.Dispose();
                if (tray != null)
                {
                    tray.Visible = false;
                    if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose();
                    tray.Dispose();
                }
                foreach (Control control in Controls)
                {
                    PictureBox picture = control as PictureBox;
                    if (picture != null && picture.Image != null) picture.Image.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
