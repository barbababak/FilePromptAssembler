using FilePromptAssembler.Models;
using FilePromptAssembler.UI.Controls;
using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.UI
{
    public sealed partial class MainForm
    {
        private const int Gap = 8;

        private ToolTip _tips = null!;
        private MenuStrip _menu = null!;
        private ToolStripMenuItem _themeMenu = null!;
        private ToolStripMenuItem _removeProjectMenuItem = null!;
        private StatusStrip _status = null!;
        private ToolStripStatusLabel _statusText = null!;
        private ToolStripStatusLabel _statusPath = null!;

        private SplitContainer _mainSplit = null!;
        private SplitContainer _leftSplit = null!;
        private SplitContainer _outputSplit = null!;
        private TableLayoutPanel _workArea = null!;
        private SectionPanel _historySection = null!;

        private TwoLineListBox _projectList = null!;
        private ComboBox _sortCombo = null!;
        private Button _sortDirection = null!;
        private Button _addProject = null!;
        private Button _removeProject = null!;
        private ContextMenuStrip _projectMenu = null!;

        private TwoLineListBox _historyList = null!;
        private Button _historyRestore = null!;
        private Button _historyDelete = null!;
        private Button _historyClear = null!;
        private ContextMenuStrip _historyMenu = null!;
        private ContextMenuStrip _historyClearMenu = null!;

        private UndoTextBox _promptBox = null!;
        private CheckBox _promptRtl = null!;
        private Button _promptTemplates = null!;
        private UndoTextBox _includeBox = null!;
        private UndoTextBox _excludeBox = null!;
        private Button _excludeTemplates = null!;
        private UndoTextBox _wildcardBox = null!;

        private CheckBox _checkAll = null!;
        private Label _fileCount = null!;
        private CheckedListBox _fileList = null!;
        private UndoTextBox _fileSearch = null!;
        private Button _clearSearch = null!;
        private Button _refreshFiles = null!;

        private Button _generate = null!;
        private Button _copy = null!;
        private CheckBox _autoCopy = null!;
        private CheckBox _dontAddHistory = null!;

        private UndoTextBox _finalBox = null!;
        private UndoTextBox _contentsBox = null!;
        private Label _finalInfo = null!;
        private Label _contentsInfo = null!;

        private void BuildUi()
        {
            _tips = new ToolTip(_components) { AutoPopDelay = 20000, InitialDelay = 400, ReshowDelay = 200 };

            _menu = BuildMenu();
            MainMenuStrip = _menu;

            _statusText = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft, Text = "Ready" };
            _statusPath = new ToolStripStatusLabel { TextAlign = ContentAlignment.MiddleRight };
            _status = new StatusStrip { SizingGrip = true };
            _status.Items.AddRange(new ToolStripItem[] { _statusText, _statusPath });

            _leftSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = Gap };
            _leftSplit.Panel1.Controls.Add(BuildProjectsSection());
            _leftSplit.Panel2.Controls.Add(BuildHistorySection());

            _mainSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = Gap };
            _mainSplit.Panel1.Controls.Add(_leftSplit);
            _mainSplit.Panel2.Controls.Add(BuildWorkArea());

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Gap) };
            body.Controls.Add(_mainSplit);

            Controls.Add(body);
            Controls.Add(_status);
            Controls.Add(_menu);
        }

        private MenuStrip BuildMenu()
        {
            var menu = new MenuStrip { Padding = new Padding(6, 2, 0, 2) };

            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add(new ToolStripMenuItem("&Add Project…", null, (_, _) => AddProjectInteractive(null)) { ShortcutKeys = Keys.Control | Keys.O });
            _removeProjectMenuItem = new ToolStripMenuItem("&Remove Project", null, (_, _) => RemoveProject(SelectedProject));
            file.DropDownItems.Add(_removeProjectMenuItem);
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(new ToolStripMenuItem("Open &Data Folder", null, (_, _) => OpenFolderSafe(_paths.Root)));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (_, _) => Close()));
            file.DropDownOpening += (_, _) => _removeProjectMenuItem.Enabled = SelectedProject != null;

            var prompt = new ToolStripMenuItem("&Prompt");
            prompt.DropDownItems.Add(new ToolStripMenuItem("&Generate Prompt", null, async (_, _) => await GenerateAsync()) { ShortcutKeys = Keys.F5 });
            prompt.DropDownItems.Add(new ToolStripMenuItem("&Copy Final Prompt", null, (_, _) => CopyToClipboard(_finalBox.Text, quiet: false)) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.C });
            prompt.DropDownItems.Add(new ToolStripSeparator());
            prompt.DropDownItems.Add(new ToolStripMenuItem("&Refresh File List", null, (_, _) => ScanNow()) { ShortcutKeys = Keys.Control | Keys.R });

            var templates = new ToolStripMenuItem("&Templates");
            templates.DropDownItems.Add(new ToolStripMenuItem("&Prompt Templates…", null, (_, _) => OpenTemplates(TemplateKind.Prompt)));
            templates.DropDownItems.Add(new ToolStripMenuItem("&Exclude Templates…", null, (_, _) => OpenTemplates(TemplateKind.Exclude)));

            var view = new ToolStripMenuItem("&View");
            _themeMenu = new ToolStripMenuItem("&Theme");
            view.DropDownItems.Add(_themeMenu);
            RebuildThemeMenu();

            var help = new ToolStripMenuItem("&Help");
            help.DropDownItems.Add(new ToolStripMenuItem("&About", null, (_, _) => ShowAbout()));

            menu.Items.AddRange(new ToolStripItem[] { file, prompt, templates, view, help });
            return menu;
        }

        private void RebuildThemeMenu()
        {
            var manager = ThemeManager.Instance;
            _themeMenu.DropDownItems.Clear();

            void AddThemeItem(string name, string caption)
            {
                var item = new ToolStripMenuItem(caption, null, (_, _) => SelectTheme(name))
                {
                    Checked = manager.SelectedName.Equals(name, StringComparison.OrdinalIgnoreCase)
                };
                _themeMenu.DropDownItems.Add(item);
            }

            AddThemeItem(ThemeManager.SystemThemeName, "System (follow Windows)");
            _themeMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (var theme in manager.Themes)
                AddThemeItem(theme.Name, theme.Name);
            _themeMenu.DropDownItems.Add(new ToolStripSeparator());
            _themeMenu.DropDownItems.Add(new ToolStripMenuItem("Open Themes Folder", null, (_, _) => OpenFolderSafe(_paths.ThemesDirectory)));
            _themeMenu.DropDownItems.Add(new ToolStripMenuItem("Reload Themes", null, (_, _) => ReloadThemes()));

            if (IsHandleCreated)
                manager.ApplyToToolStrip(_menu);
        }

        private Control BuildProjectsSection()
        {
            var section = new SectionPanel("Projects") { Dock = DockStyle.Fill };
            _addProject = Ui.ToolButton("Add…", _tips, "Add a project folder (Ctrl+O). You can also drop a folder on this list.");
            _removeProject = Ui.ToolButton("Remove", _tips, "Remove the selected project from the list (Del)");
            section.AddTool(_addProject);
            section.AddTool(_removeProject);

            var sortLabel = Ui.Label("Sort by", subtle: true);
            _sortCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 6, 0) };
            _sortCombo.Items.AddRange(new object[] { "Name", "Date Opened", "Last Modified" });
            _sortDirection = Ui.ToolButton("▲", _tips, "Toggle ascending / descending order");
            _sortDirection.MinimumSize = new Size(30, 26);
            _sortDirection.Margin = Padding.Empty;

            var sortRow = Ui.Table(3, 1);
            sortRow.AutoSize = true;
            sortRow.Margin = new Padding(0, 0, 0, 6);
            sortRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sortRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            sortRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sortRow.Controls.Add(sortLabel, 0, 0);
            sortRow.Controls.Add(_sortCombo, 1, 0);
            sortRow.Controls.Add(_sortDirection, 2, 0);

            _projectList = new TwoLineListBox
            {
                EmptyText = "No projects yet.\r\nClick Add… or drop a project folder here.",
                AllowDrop = true
            };
            _projectMenu = new ContextMenuStrip();
            _projectMenu.Items.Add(new ToolStripMenuItem("Open Folder", null, (_, _) => OpenProjectFolder(parent: false)));
            _projectMenu.Items.Add(new ToolStripMenuItem("Open Parent Folder", null, (_, _) => OpenProjectFolder(parent: true)));
            _projectMenu.Items.Add(new ToolStripSeparator());
            _projectMenu.Items.Add(new ToolStripMenuItem("Rename…", null, (_, _) => RenameProject(SelectedProject)) { ShortcutKeyDisplayString = "F2" });
            _projectMenu.Items.Add(new ToolStripMenuItem("Remove from List", null, (_, _) => RemoveProject(SelectedProject)) { ShortcutKeyDisplayString = "Del" });
            _projectMenu.Opening += (_, e) => e.Cancel = SelectedProject == null;
            _projectList.ContextMenuStrip = _projectMenu;

            var layout = Ui.Table(1, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(sortRow, 0, 0);
            layout.Controls.Add(new BorderedHost(_projectList, 0) { Dock = DockStyle.Fill }, 0, 1);
            section.Body.Controls.Add(layout);
            return section;
        }

        private Control BuildHistorySection()
        {
            _historySection = new SectionPanel("History") { Dock = DockStyle.Fill };
            _historyList = new TwoLineListBox
            {
                SelectionMode = SelectionMode.MultiExtended,
                EmptyText = "No history yet.\r\nEvery Generate Prompt adds an entry."
            };
            _tips.SetToolTip(_historyList, "Double-click or Enter: restore. Del: delete the selected entries. Ctrl/Shift+click: select several.");

            _historyMenu = new ContextMenuStrip();
            _historyMenu.Items.Add(new ToolStripMenuItem("Restore", null, (_, _) => RestoreSelectedHistory()) { ShortcutKeyDisplayString = "Enter" });
            _historyMenu.Items.Add(new ToolStripMenuItem("Copy Final Prompt", null, (_, _) => CopySelectedHistory()));
            _historyMenu.Items.Add(new ToolStripSeparator());
            _historyMenu.Items.Add(new ToolStripMenuItem("Delete Selected", null, (_, _) => DeleteSelectedHistory()) { ShortcutKeyDisplayString = "Del" });
            _historyMenu.Items.Add(new ToolStripSeparator());
            _historyMenu.Items.Add(new ToolStripMenuItem("Delete All Except Latest…", null, (_, _) => ClearHistory(keepLatest: true)));
            _historyMenu.Items.Add(new ToolStripMenuItem("Delete All…", null, (_, _) => ClearHistory(keepLatest: false)));
            _historyMenu.Opening += (_, e) =>
            {
                bool any = _historyList.SelectedIndices.Count > 0;
                _historyMenu.Items[0].Enabled = _historyList.SelectedIndices.Count == 1;
                _historyMenu.Items[1].Enabled = _historyList.SelectedIndices.Count == 1;
                _historyMenu.Items[3].Enabled = any;
                _historyMenu.Items[5].Enabled = _history.Count > 1;
                _historyMenu.Items[6].Enabled = _history.Count > 0;
            };
            _historyList.ContextMenuStrip = _historyMenu;

            _historyClearMenu = new ContextMenuStrip();
            _historyClearMenu.Items.Add(new ToolStripMenuItem("Delete All Except Latest…", null, (_, _) => ClearHistory(keepLatest: true)));
            _historyClearMenu.Items.Add(new ToolStripMenuItem("Delete All…", null, (_, _) => ClearHistory(keepLatest: false)));
            _historyClearMenu.Opening += (_, _) =>
            {
                _historyClearMenu.Items[0].Enabled = _history.Count > 1;
                _historyClearMenu.Items[1].Enabled = _history.Count > 0;
            };

            _historyRestore = Ui.Button("Restore", false, _tips, "Load the selected entry into the form (undo with Ctrl+Z in each box)");
            _historyDelete = Ui.Button("Delete", false, _tips, "Delete the selected entries (Del)");
            _historyClear = Ui.Button("Clear ▾", false, _tips, "Delete all entries of this project, with or without the latest one");

            var buttons = Ui.Table(3, 1);
            buttons.AutoSize = false;
            for (int i = 0; i < 3; i++)
                buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            int col = 0;
            foreach (var button in new[] { _historyRestore, _historyDelete, _historyClear })
            {
                button.AutoSize = false;
                button.MinimumSize = new Size(0, 30);
                button.Padding = Padding.Empty;
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(col == 0 ? 0 : 3, Gap, col == 2 ? 0 : 3, 0);
                buttons.Controls.Add(button, col++, 0);
            }

            var layout = Ui.Table(1, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layout.Controls.Add(new BorderedHost(_historyList, 0) { Dock = DockStyle.Fill }, 0, 0);
            layout.Controls.Add(buttons, 0, 1);
            _historySection.Body.Controls.Add(layout);
            return _historySection;
        }

        private Control BuildWorkArea()
        {
            _workArea = Ui.Table(1, 5);
            _workArea.RowStyles.Add(new RowStyle(SizeType.Percent, 26F));
            _workArea.RowStyles.Add(new RowStyle(SizeType.Percent, 15F));
            _workArea.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            _workArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _workArea.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));

            _workArea.Controls.Add(BuildPromptSection(), 0, 0);
            _workArea.Controls.Add(BuildFiltersRow(), 0, 1);
            _workArea.Controls.Add(BuildFilesSection(), 0, 2);
            _workArea.Controls.Add(BuildActionBar(), 0, 3);
            _workArea.Controls.Add(BuildOutputs(), 0, 4);
            return _workArea;
        }

        private Control BuildPromptSection()
        {
            var section = new SectionPanel("Prompt") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Gap) };
            _promptRtl = Ui.Toggle("RTL", _tips, "Right-to-left text direction of the Prompt and Final Prompt boxes (for Persian/Arabic text)");
            _promptTemplates = Ui.ToolButton("Open Template…", _tips, "Choose, view or edit prompt templates");
            section.AddTool(_promptRtl);
            section.AddTool(_promptTemplates);

            _promptBox = new UndoTextBox { Font = UiFonts.Prose, WordWrap = true, ScrollBars = ScrollBars.Vertical };
            section.Body.Controls.Add(new BorderedHost(_promptBox) { Dock = DockStyle.Fill });
            return section;
        }

        private Control BuildFiltersRow()
        {
            var row = Ui.Table(3, 1);
            row.Margin = new Padding(0, 0, 0, Gap);
            for (int i = 0; i < 3; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _includeBox = Ui.CodeBox();
            _excludeBox = Ui.CodeBox();
            _wildcardBox = Ui.CodeBox();
            foreach (var box in new[] { _includeBox, _excludeBox, _wildcardBox })
                box.ScrollBars = ScrollBars.Vertical;

            var include = new SectionPanel("Include Folders") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
            include.Body.Controls.Add(new BorderedHost(_includeBox) { Dock = DockStyle.Fill });
            const string includeTip = "Folders to search, one wildcard per line.\r\n" +
                "*  = all folders (default)\r\n" +
                ".  = files directly in the project folder\r\n" +
                "A line without '/' matches a folder name at any depth; its sub-folders are included too.\r\n" +
                "A line with '/' matches a path relative to the project folder (e.g. src/Views).";
            _tips.SetToolTip(include.TitleLabel, includeTip);
            _tips.SetToolTip(_includeBox, includeTip);

            var exclude = new SectionPanel("Exclude") { Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 0) };
            _excludeTemplates = Ui.ToolButton("Templates…", _tips, "Choose, add, edit or delete exclude templates");
            exclude.AddTool(_excludeTemplates);
            exclude.Body.Controls.Add(new BorderedHost(_excludeBox) { Dock = DockStyle.Fill });
            const string excludeTip = "Files and folders to skip, one wildcard per line (e.g. bin, obj, *.Designer.cs).\r\n" +
                "Matched against file and folder names, or against the relative path when the line contains '/'.\r\n" +
                "An excluded folder is skipped with everything inside it.";
            _tips.SetToolTip(exclude.TitleLabel, excludeTip);
            _tips.SetToolTip(_excludeBox, excludeTip);

            var wildcards = new SectionPanel("Files Wildcard") { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            wildcards.Body.Controls.Add(new BorderedHost(_wildcardBox) { Dock = DockStyle.Fill });
            const string wildcardTip = "File names to collect, one wildcard per line (e.g. *.cs, *.csproj, *.xaml.cs).";
            _tips.SetToolTip(wildcards.TitleLabel, wildcardTip);
            _tips.SetToolTip(_wildcardBox, wildcardTip);

            row.Controls.Add(include, 0, 0);
            row.Controls.Add(exclude, 1, 0);
            row.Controls.Add(wildcards, 2, 0);
            return row;
        }

        private Control BuildFilesSection()
        {
            var section = new SectionPanel("Files") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Gap) };
            _refreshFiles = Ui.ToolButton("Refresh", _tips, "Scan the project folder again (Ctrl+R)");
            section.AddTool(_refreshFiles);

            _checkAll = new CheckBox { Text = "Select all", AutoSize = true, AutoCheck = false, ThreeState = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 14, 0) };
            _tips.SetToolTip(_checkAll, "Check or uncheck all files shown in the list (only the matching files while a search is active)");
            _fileCount = Ui.Label(string.Empty, subtle: true);
            _fileCount.AutoSize = false;
            _fileCount.AutoEllipsis = true;
            _fileCount.Dock = DockStyle.Fill;
            _fileCount.TextAlign = ContentAlignment.MiddleLeft;

            _fileSearch = new UndoTextBox(multiline: false) { PlaceholderText = "Find text in files…" };
            _tips.SetToolTip(_fileSearch, "Show only the listed files (checked or not) that contain this text. Not case-sensitive.\r\n" +
                "Enter: search now. Esc: clear the search and show all files again.");
            var searchHost = new BorderedHost(_fileSearch, 3)
            {
                Dock = DockStyle.Fill,
                Height = _fileSearch.PreferredHeight + 8,
                Margin = new Padding(8, 0, 4, 0)
            };
            _clearSearch = Ui.ToolButton("✕", _tips, "Clear the search (Esc)");
            _clearSearch.MinimumSize = new Size(30, 26);
            _clearSearch.Margin = Padding.Empty;
            _clearSearch.Anchor = AnchorStyles.Right;

            var top = Ui.Table(4, 1);
            top.AutoSize = true;
            top.Margin = new Padding(0, 0, 0, 6);
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.Controls.Add(_checkAll, 0, 0);
            top.Controls.Add(_fileCount, 1, 0);
            top.Controls.Add(searchHost, 2, 0);
            top.Controls.Add(_clearSearch, 3, 0);

            _fileList = new CheckedListBox
            {
                BorderStyle = BorderStyle.None,
                CheckOnClick = true,
                IntegralHeight = false,
                HorizontalScrollbar = true
            };

            var layout = Ui.Table(1, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(top, 0, 0);
            layout.Controls.Add(new BorderedHost(_fileList, 2) { Dock = DockStyle.Fill }, 0, 1);
            section.Body.Controls.Add(layout);
            return section;
        }

        private Control BuildActionBar()
        {
            var bar = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 0, Gap),
                Padding = Padding.Empty
            };

            _generate = Ui.Button("Generate Prompt", primary: true, _tips, "Build the final prompt from the checked files (F5 or Ctrl+Enter)");
            _generate.MinimumSize = new Size(150, 34);
            _copy = Ui.Button("Copy to Clipboard", false, _tips, "Copy the final prompt (Ctrl+Shift+C)");
            _copy.MinimumSize = new Size(150, 34);
            _autoCopy = new CheckBox { Text = "Auto Copy", AutoSize = true, Margin = new Padding(10, 9, 0, 0) };
            _tips.SetToolTip(_autoCopy, "Copy the final prompt to the clipboard after each Generate Prompt");
            _dontAddHistory = new CheckBox { Text = "Don't add to history", AutoSize = true, Margin = new Padding(14, 9, 0, 0) };
            _tips.SetToolTip(_dontAddHistory, "When checked, Generate Prompt does not add an entry to this project's history");

            bar.Controls.AddRange(new Control[] { _generate, _copy, _autoCopy, _dontAddHistory });
            return bar;
        }

        private Control BuildOutputs()
        {
            _outputSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = Gap, Margin = Padding.Empty };

            var final = new SectionPanel("Final Prompt") { Dock = DockStyle.Fill };
            _finalInfo = Ui.Label(string.Empty, subtle: true);
            final.AddTool(_finalInfo);
            _finalBox = Ui.CodeBox();
            final.Body.Controls.Add(new BorderedHost(_finalBox) { Dock = DockStyle.Fill });

            var contents = new SectionPanel("File Contents") { Dock = DockStyle.Fill };
            _contentsInfo = Ui.Label(string.Empty, subtle: true);
            contents.AddTool(_contentsInfo);
            _contentsBox = Ui.CodeBox();
            contents.Body.Controls.Add(new BorderedHost(_contentsBox) { Dock = DockStyle.Fill });

            _outputSplit.Panel1.Controls.Add(final);
            _outputSplit.Panel2.Controls.Add(contents);
            return _outputSplit;
        }
    }
}
