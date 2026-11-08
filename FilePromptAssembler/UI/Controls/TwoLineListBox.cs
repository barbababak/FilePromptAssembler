using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.UI.Controls
{
    public readonly record struct ListItemText(string Primary, string Secondary, bool Dimmed = false);

    /// <summary>An owner-drawn list box showing a bold first line and a subtle second line per item.</summary>
    public sealed class TwoLineListBox : ListBox, IThemeable
    {
        private const int WM_PAINT = 0x000F;
        private Theme _theme = BuiltInThemes.Light;
        private Font? _boldFont;

        public TwoLineListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            UpdateItemHeight();
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Func<object, ListItemText>? Formatter { get; set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string EmptyText { get; set; } = string.Empty;

        private Font BoldFont => _boldFont ??= new Font(Font, FontStyle.Bold);

        public void ApplyTheme(Theme theme)
        {
            _theme = theme;
            BackColor = theme.Input;
            ForeColor = theme.Text;
            NativeMethods.SetControlTheme(this);
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _boldFont?.Dispose();
            _boldFont = null;
            UpdateItemHeight();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            UpdateItemHeight();
        }

        private void UpdateItemHeight()
        {
            ItemHeight = Math.Min(255, Font.Height * 2 + LogicalToDeviceUnits(10));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count)
                return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            var text = Formatter?.Invoke(Items[e.Index]) ?? new ListItemText(Items[e.Index]?.ToString() ?? string.Empty, string.Empty);

            Color back = selected ? _theme.Selection : _theme.Input;
            Color primary = selected ? _theme.SelectionText : (text.Dimmed ? _theme.SubtleText : _theme.Text);
            Color secondary = selected ? _theme.SelectionText : _theme.SubtleText;

            using (var brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);

            if (selected)
            {
                using var accent = new SolidBrush(_theme.Accent);
                e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top, LogicalToDeviceUnits(3), e.Bounds.Height);
            }

            int pad = LogicalToDeviceUnits(8);
            int lineHeight = Font.Height + LogicalToDeviceUnits(1);
            int top = e.Bounds.Top + (e.Bounds.Height - lineHeight * 2) / 2;
            var r1 = new Rectangle(e.Bounds.Left + pad, top, Math.Max(0, e.Bounds.Width - pad * 2), lineHeight);
            var r2 = new Rectangle(r1.Left, r1.Bottom, r1.Width, lineHeight);

            const TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine
                | TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(e.Graphics, text.Primary, BoldFont, r1, primary, flags);
            TextRenderer.DrawText(e.Graphics, text.Secondary, Font, r2, secondary, flags);

            using var pen = new Pen(_theme.Border);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && Items.Count == 0 && EmptyText.Length > 0)
            {
                using var g = CreateGraphics();
                var bounds = Rectangle.Inflate(ClientRectangle, -LogicalToDeviceUnits(12), -LogicalToDeviceUnits(12));
                TextRenderer.DrawText(g, EmptyText, Font, bounds, _theme.SubtleText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _boldFont?.Dispose();
            base.Dispose(disposing);
        }
    }
}
