using FilePromptAssembler.Models;
using FilePromptAssembler.Services;
using FilePromptAssembler.UI.Controls;
using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.UI.Dialogs
{
    /// <summary>
    /// Shown when a project folder is added: sets the project name and the initial content of the
    /// prompt, exclude and file wildcard boxes (empty or from a template).
    /// </summary>
    public sealed class NewProjectDialog : ThemedForm
    {
        private const string NoneItem = "(Empty)";

        private readonly TemplateRepository _templates;
        private readonly string _folder;
        private readonly UndoTextBox _name;
        private readonly ComboBox _promptTemplate;
        private readonly ComboBox _excludeTemplate;
        private readonly UndoTextBox _filePatterns;

        public NewProjectDialog(string folder, TemplateRepository templates, ProjectDetection detection, AppSettings settings)
        {
            _folder = folder;
            _templates = templates;

            SuspendLayout();
            Text = "Add Project";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 400);

            var layout = Ui.Table(2, 7);
            layout.Padding = new Padding(16);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 5; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var folderValue = Ui.Label(folder, subtle: true);
            folderValue.AutoSize = false;
            folderValue.AutoEllipsis = true;
            folderValue.Dock = DockStyle.Fill;
            folderValue.TextAlign = ContentAlignment.MiddleLeft;
            AddRow(layout, 0, "Folder:", folderValue);

            _name = new UndoTextBox(multiline: false);
            _name.SetTextWithoutUndo(PathUtil.DefaultProjectName(folder));
            AddRow(layout, 1, "Name:", new BorderedHost(_name, 4) { Dock = DockStyle.Fill, Height = _name.PreferredHeight + 10 });

            AddRow(layout, 2, "Detected:", Ui.Label(detection.Description, subtle: true));

            _promptTemplate = CreateCombo(TemplateKind.Prompt,
                Existing(TemplateKind.Prompt, detection.PromptTemplate) ?? Existing(TemplateKind.Prompt, settings.LastNewProjectPromptTemplate));
            AddRow(layout, 3, "Prompt template:", _promptTemplate);

            _excludeTemplate = CreateCombo(TemplateKind.Exclude,
                Existing(TemplateKind.Exclude, detection.ExcludeTemplate) ?? Existing(TemplateKind.Exclude, settings.LastNewProjectExcludeTemplate));
            AddRow(layout, 4, "Exclude template:", _excludeTemplate);

            _filePatterns = Ui.CodeBox();
            _filePatterns.ScrollBars = ScrollBars.Vertical;
            _filePatterns.SetTextWithoutUndo(detection.FilePatterns.Length > 0 ? detection.FilePatterns : settings.LastNewProjectFilePatterns);
            var patternsLabel = Ui.Label("File wildcards:");
            patternsLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            patternsLabel.Margin = new Padding(0, 6, 12, 0);
            layout.Controls.Add(patternsLabel, 0, 5);
            layout.Controls.Add(new BorderedHost(_filePatterns, 4) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) }, 1, 5);

            var add = Ui.Button("Add Project", primary: true);
            add.DialogResult = DialogResult.OK;
            var cancel = Ui.Button("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Margin = Padding.Empty;
            var buttons = Ui.ButtonRow();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(add);
            layout.Controls.Add(buttons, 0, 6);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);
            AcceptButton = add;
            CancelButton = cancel;
            ResumeLayout(true);
        }

        public string ProjectName => string.IsNullOrWhiteSpace(_name.Text) ? PathUtil.DefaultProjectName(_folder) : _name.Text.Trim();
        public string? PromptTemplateName => _promptTemplate.SelectedItem as string is { } s && s != NoneItem ? s : null;
        public string? ExcludeTemplateName => _excludeTemplate.SelectedItem as string is { } s && s != NoneItem ? s : null;
        public string FilePatterns => _filePatterns.Text;

        public ProjectState CreateInitialState()
        {
            string prompt = _templates.Library.Find(TemplateKind.Prompt, PromptTemplateName)?.Text ?? string.Empty;
            return new ProjectState
            {
                UserPrompt = prompt,
                PromptRightToLeft = TextUtil.ContainsRtl(prompt),
                IncludePatterns = "*",
                ExcludePatterns = _templates.Library.Find(TemplateKind.Exclude, ExcludeTemplateName)?.Text ?? string.Empty,
                FilePatterns = TextUtil.NormalizeNewLines(_filePatterns.Text)
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _name.Focus();
            _name.SelectAll();
        }

        private string? Existing(TemplateKind kind, string? name) => _templates.Library.Find(kind, name)?.Name;

        private ComboBox CreateCombo(TemplateKind kind, string? selected)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            combo.Items.Add(NoneItem);
            foreach (var template in _templates.Sorted(kind))
                combo.Items.Add(template.Name);
            combo.SelectedItem = selected ?? NoneItem;
            if (combo.SelectedIndex < 0)
                combo.SelectedIndex = 0;
            return combo;
        }

        private static void AddRow(TableLayoutPanel layout, int row, string caption, Control field)
        {
            var label = Ui.Label(caption);
            label.Margin = new Padding(0, 0, 12, 10);
            field.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(label, 0, row);
            layout.Controls.Add(field, 1, row);
        }
    }
}
