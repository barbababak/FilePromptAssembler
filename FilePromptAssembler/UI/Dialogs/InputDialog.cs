using FilePromptAssembler.UI.Controls;
using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.UI.Dialogs
{
    /// <summary>Asks for a single line of text.</summary>
    public sealed class InputDialog : ThemedForm
    {
        private readonly UndoTextBox _box;

        private InputDialog(string title, string prompt, string initialValue)
        {
            SuspendLayout();
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 140);

            var layout = Ui.Table(1, 3);
            layout.Padding = new Padding(14);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var label = Ui.Label(prompt);
            label.Margin = new Padding(0, 0, 0, 6);

            _box = new UndoTextBox(multiline: false);
            _box.SetTextWithoutUndo(initialValue);
            var host = new BorderedHost(_box, 4)
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = _box.PreferredHeight + 10,
                Margin = new Padding(0, 0, 0, 12)
            };

            var ok = Ui.Button("OK", primary: true);
            ok.DialogResult = DialogResult.OK;
            var cancel = Ui.Button("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            var buttons = Ui.ButtonRow();
            buttons.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            buttons.Dock = DockStyle.None;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            cancel.Margin = Padding.Empty;

            layout.Controls.Add(label, 0, 0);
            layout.Controls.Add(host, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
            Controls.Add(layout);

            AcceptButton = ok;
            CancelButton = cancel;
            ResumeLayout(true);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _box.Focus();
            _box.SelectAll();
        }

        /// <summary>Returns the trimmed text, or null when cancelled.</summary>
        public static string? Ask(IWin32Window owner, string title, string prompt, string initialValue)
        {
            using var dialog = new InputDialog(title, prompt, initialValue);
            return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._box.Text.Trim() : null;
        }
    }
}
