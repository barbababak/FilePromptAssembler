using System.Drawing.Drawing2D;

namespace FilePromptAssembler.UI.Theming
{
    internal sealed class ThemedToolStripRenderer : ToolStripProfessionalRenderer
    {
        private readonly Theme _theme;

        public ThemedToolStripRenderer(Theme theme) : base(new ThemedColorTable(theme))
        {
            _theme = theme;
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _theme.Text : _theme.SubtleText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled == false ? _theme.SubtleText : _theme.Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            if (r.Width <= 0 || r.Height <= 0)
                return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(_theme.Accent, Math.Max(1.6f, r.Height / 9f));
            g.DrawLines(pen, new[]
            {
                new PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.52f),
                new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.74f),
                new PointF(r.Left + r.Width * 0.8f, r.Top + r.Height * 0.28f)
            });
            g.SmoothingMode = SmoothingMode.Default;
        }
    }

    internal sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly Theme _t;

        public ThemedColorTable(Theme theme)
        {
            _t = theme;
            UseSystemColors = false;
        }

        public override Color MenuStripGradientBegin => _t.Background;
        public override Color MenuStripGradientEnd => _t.Background;
        public override Color StatusStripGradientBegin => _t.Background;
        public override Color StatusStripGradientEnd => _t.Background;
        public override Color ToolStripGradientBegin => _t.Background;
        public override Color ToolStripGradientMiddle => _t.Background;
        public override Color ToolStripGradientEnd => _t.Background;
        public override Color ToolStripBorder => _t.Background;
        public override Color ToolStripDropDownBackground => _t.Surface;
        public override Color ImageMarginGradientBegin => _t.Surface;
        public override Color ImageMarginGradientMiddle => _t.Surface;
        public override Color ImageMarginGradientEnd => _t.Surface;
        public override Color MenuBorder => _t.Border;
        public override Color MenuItemBorder => _t.Selection;
        public override Color MenuItemSelected => _t.Selection;
        public override Color MenuItemSelectedGradientBegin => _t.Selection;
        public override Color MenuItemSelectedGradientEnd => _t.Selection;
        public override Color MenuItemPressedGradientBegin => _t.Surface;
        public override Color MenuItemPressedGradientMiddle => _t.Surface;
        public override Color MenuItemPressedGradientEnd => _t.Surface;
        public override Color ButtonSelectedHighlight => _t.Selection;
        public override Color ButtonSelectedGradientBegin => _t.Selection;
        public override Color ButtonSelectedGradientEnd => _t.Selection;
        public override Color CheckBackground => _t.Surface;
        public override Color CheckSelectedBackground => _t.Selection;
        public override Color CheckPressedBackground => _t.Selection;
        public override Color SeparatorDark => _t.Border;
        public override Color SeparatorLight => _t.Surface;
        public override Color GripDark => _t.Border;
        public override Color GripLight => _t.Background;
    }
}
