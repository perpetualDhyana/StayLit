using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace StayLit
{
    // Use a complete native edit window. Hiding NumericUpDown's buttons
    // left an unpainted strip that could turn black after display recovery.
    internal sealed class DurationNumber : TextBox
    {
        private decimal current;
        private decimal minimum;
        private decimal maximum = 100;
        private bool updating;
        private bool editing;
        public event EventHandler ValueChanged;

        public DurationNumber()
        {
            ImeMode = ImeMode.Disable;
            BorderStyle = BorderStyle.None;
            TextAlign = HorizontalAlignment.Center;
            SetValue(0);
        }

        public decimal Minimum
        {
            get { return minimum; }
            set
            {
                if (value > maximum) throw new ArgumentOutOfRangeException("value");
                minimum = value;
                if (current < minimum) SetValue(minimum);
            }
        }

        public decimal Maximum
        {
            get { return maximum; }
            set
            {
                if (value < minimum) throw new ArgumentOutOfRangeException("value");
                maximum = value;
                if (current > maximum) SetValue(maximum);
            }
        }

        public decimal Value
        {
            get { CommitEdit(); return current; }
            set
            {
                if (value < minimum || value > maximum) throw new ArgumentOutOfRangeException("value");
                SetValue(value);
            }
        }

        private void CommitEdit()
        {
            if (!editing) return;
            decimal parsed;
            if (!Decimal.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) parsed = current;
            SetValue(Math.Max(minimum, Math.Min(maximum, parsed)));
        }

        private void SetValue(decimal value)
        {
            bool changed = current != value;
            current = value;
            editing = false;
            updating = true;
            try
            {
                string formatted = value.ToString("0", CultureInfo.InvariantCulture);
                if (Text != formatted)
                {
                    Text = formatted;
                    if (Focused) Select(TextLength, 0);
                }
            }
            finally { updating = false; }
            if (changed && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        public void UpButton() { CommitEdit(); SetValue(Math.Min(maximum, current + 1)); }
        public void DownButton() { CommitEdit(); SetValue(Math.Max(minimum, current - 1)); }

        protected override void OnTextChanged(EventArgs e)
        {
            if (!updating) editing = true;
            base.OnTextChanged(e);
        }

        protected override void OnValidating(CancelEventArgs e)
        {
            CommitEdit();
            base.OnValidating(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || (e.KeyCode != Keys.Up && e.KeyCode != Keys.Down)) return;
            if (e.KeyCode == Keys.Up) UpButton(); else DownButton();
            e.Handled = e.SuppressKeyPress = true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!Char.IsControl(e.KeyChar) && (e.KeyChar < '0' || e.KeyChar > '9')) e.Handled = true;
            base.OnKeyPress(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Focused && e.Delta != 0)
            {
                if (e.Delta > 0) UpButton(); else DownButton();
                HandledMouseEventArgs handled = e as HandledMouseEventArgs;
                if (handled != null) handled.Handled = true;
            }
            base.OnMouseWheel(e);
        }
    }

    internal sealed class DurationField : Panel
    {
        public readonly DurationNumber Number;
        private readonly RoundedButton decrease;
        private readonly RoundedButton increase;
        private readonly Label unitLabel;

        public DurationField(string unit, string accessibleName, int maximum)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(248, 64);
            TabStop = false;
            AccessibleName = accessibleName + "设置";
            decrease = StepButton(-1, "减少" + unit, 12);
            Number = new DurationNumber();
            Number.Bounds = new Rectangle(52, 12, 78, 40);
            Number.Font = new Font("Segoe UI", 22f);
            Number.BorderStyle = BorderStyle.None;
            Number.TextAlign = HorizontalAlignment.Center;
            Number.Minimum = 0;
            Number.Maximum = maximum;
            Number.AccessibleName = accessibleName;
            Number.AccessibleDescription = "输入 0 到 " + maximum + " 的整数，使用上下方向键调整。";
            Number.TabIndex = 0;
            Controls.Add(Number);
            unitLabel = new Label();
            unitLabel.Text = unit;
            unitLabel.Bounds = new Rectangle(134, 19, 54, 26);
            unitLabel.TextAlign = ContentAlignment.MiddleLeft;
            unitLabel.Font = new Font("Microsoft YaHei UI", 10f);
            Controls.Add(unitLabel);
            increase = StepButton(1, "增加" + unit, 204);
            decrease.TabIndex = 1;
            increase.TabIndex = 2;
            decrease.Click += delegate { Number.DownButton(); };
            increase.Click += delegate { Number.UpButton(); };
            Number.ValueChanged += delegate { UpdateButtons(); };
            Enter += delegate { Invalidate(); };
            Leave += delegate { Invalidate(); };
            MouseDown += delegate { Number.Focus(); };
            unitLabel.Click += delegate { Number.Focus(); };
            ApplyTheme();
            UpdateButtons();
        }

        private RoundedButton StepButton(int direction, string name, int x)
        {
            RoundedButton button = new RoundedButton();
            button.Bounds = new Rectangle(x, 18, 32, 28);
            button.StepDirection = direction;
            button.AccessibleName = name;
            button.AccessibleRole = AccessibleRole.PushButton;
            Controls.Add(button);
            return button;
        }

        private void UpdateButtons()
        {
            decrease.Enabled = Number.Value > Number.Minimum;
            increase.Enabled = Number.Value < Number.Maximum;
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            Number.BackColor = unitLabel.BackColor = Theme.Surface;
            Number.ForeColor = Theme.Ink;
            unitLabel.ForeColor = Theme.Muted;
            foreach (RoundedButton button in new RoundedButton[] { decrease, increase })
            {
                button.FillColor = Theme.Raised;
                button.LineColor = Theme.Border;
                button.InkColor = Theme.Ink;
            }
            Invalidate(true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = e.Graphics.DpiX / 96f;
            using (GraphicsPath path = Branding.Rounded(new RectangleF(0.75f, 0.75f, Width - 1.5f, Height - 1.5f), 12 * scale))
            using (Brush brush = new SolidBrush(Theme.Surface))
            using (Pen pen = new Pen(ContainsFocus ? Theme.Accent : Theme.Border, ContainsFocus ? 1.5f * scale : 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }
}
