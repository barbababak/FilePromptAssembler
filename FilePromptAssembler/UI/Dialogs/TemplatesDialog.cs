using FilePromptAssembler.Models;
using FilePromptAssembler.Services;
using FilePromptAssembler.UI.Controls;
using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.UI.Dialogs
{
    /// <summary>
    /// Lists the templates of one kind (sorted by name). Templates are read-only until "Edit" is clicked,
    /// and changes are stored only after "Save". "Use Template" returns the selected text to the caller.
    /// </summary>
    public sealed class TemplatesDialog : ThemedForm
    {
        private readonly TemplateKind _kind;
        private readonly TemplateRepository _repository;
        private readonly ListBox _list;
        private readonly UndoTextBox _text;
        private readonly SectionPanel _contentSection;
        private readonly Label _modeLabel;
        private readonly Button _new, _rename, _delete, _edit, _save, _cancel, _use, _close;
        private readonly CheckBox _rtl;
        private readonly ToolTip _tips;
        private TextTemplate? _shown;
        private bool _editing;
        private bool _suppressSelection;

        public TemplatesDialog(TemplateKind kind, TemplateRepository repository, string? selectName)
        {
            _kind = kind;
            _repository = repository;
            _tips = new ToolTip();

            SuspendLayout();
            Text = kind == TemplateKind.Prompt ? "Prompt Templates" : "Exclude Templates";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(940, 620);
            MinimumSize = new Size(640, 420);

            // Left: template list
            var listSection = new SectionPanel("Templates") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 8) };
            _list = new ListBox { BorderStyle = BorderStyle.None, IntegralHeight = false, ItemHeight = 22 };
            _list.SelectedIndexChanged += (_, _) => OnSelectionChanged();
            _list.DoubleClick += (_, _) => UseSelected();
            _new = Ui.Button("New…");
            _rename = Ui.Button("Rename…");
            _delete = Ui.Button("Delete");
            var listButtons = Ui.Table(3, 1);
            listButtons.AutoSize = false;
            listButtons.Dock = DockStyle.Fill;
            for (int i = 0; i < 3; i++)
                listButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            foreach (var (button, col) in new[] { (_new, 0), (_rename, 1), (_delete, 2) })
            {
                button.AutoSize = false;
                button.Padding = Padding.Empty;
                button.MinimumSize = new Size(0, 30);
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(col == 0 ? 0 : 3, 8, col == 2 ? 0 : 3, 0);
                listButtons.Controls.Add(button, col, 0);
            }
            var listLayout = Ui.Table(1, 2);
            listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            listLayout.Controls.Add(new BorderedHost(_list, 2) { Dock = DockStyle.Fill }, 0, 0);
            listLayout.Controls.Add(listButtons, 0, 1);
            listSection.Body.Controls.Add(listLayout);

            // Right: content
            _contentSection = new SectionPanel("Content") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            _rtl = Ui.Toggle("RTL", _tips, "Right-to-left text direction");
            _rtl.CheckedChanged += (_, _) => _text!.RightToLeft = _rtl.Checked ? RightToLeft.Yes : RightToLeft.No;
            _edit = Ui.ToolButton("Edit", _tips, "Allow changes to this template");
            _save = Ui.ToolButton("Save", _tips, "Store the changes (Ctrl+S)");
            _cancel = Ui.ToolButton("Cancel", _tips, "Discard the changes");
            _contentSection.AddTool(_rtl);
            _contentSection.AddTool(_edit);
            _contentSection.AddTool(_save);
            _contentSection.AddTool(_cancel);

            _modeLabel = Ui.Label(string.Empty, subtle: true);
            _modeLabel.Margin = new Padding(0, 0, 0, 6);
            _text = kind == TemplateKind.Prompt
                ? new UndoTextBox { Font = UiFonts.Prose, WordWrap = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true }
                : new UndoTextBox { Font = UiFonts.Code, ReadOnly = true };
            var contentLayout = Ui.Table(1, 2);
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            contentLayout.Controls.Add(_modeLabel, 0, 0);
            contentLayout.Controls.Add(new BorderedHost(_text) { Dock = DockStyle.Fill }, 0, 1);
            _contentSection.Body.Controls.Add(contentLayout);

            // Bottom buttons
            _use = Ui.Button("Use Template", primary: true, _tips, kind == TemplateKind.Prompt
                ? "Replace the prompt with this template (Ctrl+Z in the prompt box restores the previous text)"
                : "Replace the Exclude box with this template (Ctrl+Z in the box restores the previous text)");
            _close = Ui.Button("Close");
            _close.DialogResult = DialogResult.Cancel;
            _close.Margin = Padding.Empty;
            var bottom = Ui.ButtonRow();
            bottom.Controls.Add(_close);
            bottom.Controls.Add(_use);

            var root = Ui.Table(2, 2);
            root.Padding = new Padding(10);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(listSection, 0, 0);
            root.Controls.Add(_contentSection, 1, 0);
            root.Controls.Add(bottom, 0, 1);
            root.SetColumnSpan(bottom, 2);
            Controls.Add(root);
            CancelButton = _close;

            _new.Click += (_, _) => CreateTemplate();
            _rename.Click += (_, _) => RenameTemplate();
            _delete.Click += (_, _) => DeleteTemplate();
            _edit.Click += (_, _) => BeginEdit();
            _save.Click += (_, _) => SaveEdit();
            _cancel.Click += (_, _) => CancelEdit();
            _use.Click += (_, _) => UseSelected();

            ResumeLayout(true);
            RefreshList(selectName);
        }

        /// <summary>The text of the template chosen with "Use Template".</summary>
        public string? ChosenText { get; private set; }

        /// <summary>The template that was selected when the dialog closed.</summary>
        public string? LastSelectedName => _shown?.Name;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S) && _editing)
            {
                SaveEdit();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_editing && !ConfirmLeaveEdit())
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        private void RefreshList(string? selectName)
        {
            _suppressSelection = true;
            try
            {
                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (var template in _repository.Sorted(_kind))
                    _list.Items.Add(template);
                _list.EndUpdate();

                var target = _list.Items.Cast<TextTemplate>()
                    .FirstOrDefault(t => string.Equals(t.Name, selectName, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                    _list.SelectedItem = target;
                else if (_list.Items.Count > 0)
                    _list.SelectedIndex = 0;
            }
            finally
            {
                _suppressSelection = false;
            }
            ShowSelected();
        }

        private void OnSelectionChanged()
        {
            if (_suppressSelection)
                return;

            if (_editing && !ConfirmLeaveEdit())
            {
                _suppressSelection = true;
                _list.SelectedItem = _shown;
                _suppressSelection = false;
                return;
            }
            ShowSelected();
        }

        private void ShowSelected()
        {
            _shown = _list.SelectedItem as TextTemplate;
            string text = _shown?.Text ?? string.Empty;
            _text.SetTextWithoutUndo(text);
            _rtl.Checked = TextUtil.ContainsRtl(text);
            _contentSection.TitleLabel.Text = _shown?.Name ?? "Content";
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool has = _shown != null;
            _new.Enabled = !_editing;
            _rename.Enabled = has && !_editing;
            _delete.Enabled = has && !_editing;
            _edit.Enabled = has && !_editing;
            _save.Enabled = _editing;
            _cancel.Enabled = _editing;
            _use.Enabled = has && !_editing;
            _text.ReadOnly = !_editing;
            _modeLabel.Text = !has
                ? "No template selected. Click New… to create one."
                : _editing
                    ? "Editing. Click Save (Ctrl+S) to keep your changes or Cancel to discard them."
                    : "Read-only. Click Edit to change this template.";
        }

        private void BeginEdit()
        {
            if (_shown == null)
                return;
            _editing = true;
            _text.ResetUndoHistory();
            UpdateButtons();
            _text.Focus();
        }

        private void SaveEdit()
        {
            if (_shown == null || !_editing)
                return;
            _shown.Text = TextUtil.NormalizeNewLines(_text.Text);
            _repository.Save();
            _editing = false;
            UpdateButtons();
        }

        private void CancelEdit()
        {
            _editing = false;
            _text.SetTextWithoutUndo(_shown?.Text ?? string.Empty);
            UpdateButtons();
        }

        /// <summary>Returns false when the user wants to stay in edit mode.</summary>
        private bool ConfirmLeaveEdit()
        {
            if (!_editing)
                return true;
            if (_shown == null || TextUtil.NormalizeNewLines(_text.Text) == _shown.Text)
            {
                CancelEdit();
                return true;
            }

            var answer = MessageBox.Show(this, $"Save the changes to template '{_shown.Name}'?", Program.AppName,
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel)
                return false;
            if (answer == DialogResult.Yes)
                SaveEdit();
            else
                CancelEdit();
            return true;
        }

        private string? AskName(string title, string initial, TextTemplate? except)
        {
            string? name = initial;
            while (true)
            {
                name = InputDialog.Ask(this, title, "Template name:", name);
                if (string.IsNullOrWhiteSpace(name))
                    return null;
                var existing = _repository.Library.Find(_kind, name);
                if (existing == null || ReferenceEquals(existing, except))
                    return name;
                Ui.Info(this, $"A template named '{name}' already exists. Please choose another name.");
            }
        }

        private void CreateTemplate()
        {
            string? name = AskName("New Template", string.Empty, null);
            if (name == null)
                return;
            _repository.Library.Get(_kind).Add(new TextTemplate { Name = name, Text = string.Empty });
            _repository.Save();
            RefreshList(name);
            BeginEdit();
        }

        private void RenameTemplate()
        {
            if (_shown == null)
                return;
            string? name = AskName("Rename Template", _shown.Name, _shown);
            if (name == null || name == _shown.Name)
                return;
            _shown.Name = name;
            _repository.Save();
            RefreshList(name);
        }

        private void DeleteTemplate()
        {
            if (_shown == null || !Ui.Confirm(this, $"Delete template '{_shown.Name}'?"))
                return;
            int index = _list.SelectedIndex;
            _repository.Library.Get(_kind).Remove(_shown);
            _repository.Save();
            RefreshList(null);
            if (_list.Items.Count > 0)
                _list.SelectedIndex = Math.Min(index, _list.Items.Count - 1);
        }

        private void UseSelected()
        {
            if (_shown == null || _editing)
                return;
            ChosenText = _shown.Text;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
