using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.UI.Controls
{
    /// <summary>Small factory helpers so all windows use the same control styles.</summary>
    public static class Ui
    {
        public static Button Button(string text, bool primary = false, ToolTip? tips = null, string? tip = null)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(96, 30),
                Padding = new Padding(10, 0, 10, 0),
                Margin = new Padding(0, 0, 6, 0),
                UseMnemonic = false,
                Tag = primary ? ThemeManager.PrimaryTag : null
            };
            if (tips != null && tip != null)
                tips.SetToolTip(button, tip);
            return button;
        }

        public static Button ToolButton(string text, ToolTip? tips = null, string? tip = null)
        {
            var button = Button(text, false, tips, tip);
            button.MinimumSize = new Size(0, 26);
            button.Padding = new Padding(6, 0, 6, 0);
            return button;
        }

        public static CheckBox Toggle(string text, ToolTip? tips = null, string? tip = null)
        {
            var toggle = new CheckBox
            {
                Appearance = Appearance.Button,
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(48, 26),
                Padding = new Padding(6, 0, 6, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false
            };
            if (tips != null && tip != null)
                tips.SetToolTip(toggle, tip);
            return toggle;
        }

        public static Label Label(string text, bool subtle = false) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            UseMnemonic = false,
            Tag = subtle ? ThemeManager.SubtleTag : null,
            Margin = new Padding(0, 0, 6, 0)
        };

        public static TableLayoutPanel Table(int columns, int rows)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columns,
                RowCount = rows,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            return table;
        }

        public static FlowLayoutPanel ButtonRow(FlowDirection direction = FlowDirection.RightToLeft) => new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = direction,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        public static UndoTextBox CodeBox() => new() { Font = UiFonts.Code, WordWrap = false, ScrollBars = ScrollBars.Both };

        public static void Error(IWin32Window owner, string message) =>
            MessageBox.Show(owner, message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public static void Info(IWin32Window owner, string message) =>
            MessageBox.Show(owner, message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);

        public static bool Confirm(IWin32Window owner, string message) =>
            MessageBox.Show(owner, message, Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }
}
