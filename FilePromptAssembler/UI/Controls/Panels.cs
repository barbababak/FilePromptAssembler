using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.UI.Controls
{
    public static class UiFonts
    {
        public static readonly Font SectionTitle = new("Segoe UI Semibold", 9.75F);
        public static readonly Font Prose = new("Segoe UI", 10F);
        public static readonly Font Code = CreateCodeFont();

        private static Font CreateCodeFont()
        {
            var font = new Font("Cascadia Mono", 9.75F);
            if (font.Name == "Cascadia Mono")
                return font;
            font.Dispose();
            return new Font("Consolas", 10F);
        }
    }

    /// <summary>A bordered "card" with a title row (title on the left, tool buttons on the right) and a body.</summary>
    public sealed class SectionPanel : Panel, IThemeable
    {
        private readonly FlowLayoutPanel _tools;
        private Color _borderColor = Color.Silver;

        public SectionPanel(string title)
        {
            Padding = new Padding(10, 6, 10, 10);
            ResizeRedraw = true;
            DoubleBuffered = true;

            TitleLabel = new Label
            {
                Text = title,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Font = UiFonts.SectionTitle,
                Margin = new Padding(0, 4, 8, 4),
                UseMnemonic = false
            };

            _tools = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Anchor = AnchorStyles.Right
            };

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 0, 4)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.Controls.Add(TitleLabel, 0, 0);
            header.Controls.Add(_tools, 1, 0);

            Body = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty };

            Controls.Add(Body);
            Controls.Add(header);
        }

        public Label TitleLabel { get; }
        public Panel Body { get; }

        public void AddTool(Control control)
        {
            control.Margin = new Padding(4, 0, 0, 0);
            control.Anchor = AnchorStyles.None;
            _tools.Controls.Add(control);
        }

        public void ApplyTheme(Theme theme)
        {
            BackColor = theme.Surface;
            ForeColor = theme.Text;
            _borderColor = theme.Border;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(_borderColor);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    /// <summary>Hosts a border-less control and draws a themed 1px border (accent colored while focused).</summary>
    public sealed class BorderedHost : Panel, IThemeable
    {
        private Color _border = Color.Silver;
        private Color _focus = Color.SteelBlue;

        public BorderedHost(Control inner, int innerPadding = 4)
        {
            Inner = inner;
            Padding = new Padding(1 + innerPadding, 1 + Math.Max(0, innerPadding - 1), 1 + innerPadding, 1 + Math.Max(0, innerPadding - 1));
            Margin = Padding.Empty;
            ResizeRedraw = true;
            inner.Dock = DockStyle.Fill;
            inner.Enter += (_, _) => Invalidate();
            inner.Leave += (_, _) => Invalidate();
            Controls.Add(inner);
        }

        public Control Inner { get; }

        public void ApplyTheme(Theme theme)
        {
            BackColor = theme.Input;
            _border = theme.Border;
            _focus = theme.Accent;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Inner.ContainsFocus ? _focus : _border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
