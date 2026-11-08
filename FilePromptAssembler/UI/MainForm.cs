using System.ComponentModel;
using FilePromptAssembler.Models;
using FilePromptAssembler.Services;
using FilePromptAssembler.UI.Controls;
using FilePromptAssembler.UI.Dialogs;
using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.UI
{
    public sealed partial class MainForm : ThemedForm
    {
        private readonly Container _components = new();
        private readonly AppPaths _paths;
        private readonly AppSettings _settings;
        private readonly ProjectRepository _projectRepository;
        private readonly HistoryRepository _historyRepository;
        private readonly TemplateRepository _templateRepository;
        private readonly List<ProjectInfo> _projects;

        private ProjectInfo? _current;
        private List<HistoryEntry> _history = new();
        private readonly HashSet<string> _unchecked = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, UndoHistory[]> _undoByProject = new();

        private readonly System.Windows.Forms.Timer _saveTimer;
        private readonly System.Windows.Forms.Timer _scanTimer;
        private CancellationTokenSource? _scanCts;
        private Task _scanTask = Task.CompletedTask;

        // All files found by the last scan; the list box may show only the search matches.
        private List<string> _allFiles = new();
        private bool _noFilePatterns;
        private readonly FileContentSearcher _searcher = new();
        private readonly System.Windows.Forms.Timer _searchTimer;
        private CancellationTokenSource? _searchCts;
        private HashSet<string>? _searchMatches;
        private bool _searching;

        private bool _loading;
        private bool _populatingFiles;
        private bool _suppressProjectSelection;
        private bool _stateDirty;
        private bool _generating;
        private bool _shown;

        public MainForm(AppPaths paths, AppSettings settings)
        {
            _paths = paths;
            _settings = settings;
            JsonStore.Error += OnStoreError;

            _projectRepository = new ProjectRepository(paths);
            _historyRepository = new HistoryRepository(paths);
            _templateRepository = new TemplateRepository(paths.TemplatesFile);
            _templateRepository.Load();
            _projects = _projectRepository.LoadProjects();

            _saveTimer = new System.Windows.Forms.Timer(_components) { Interval = 700 };
            _saveTimer.Tick += (_, _) => FlushState();
            _scanTimer = new System.Windows.Forms.Timer(_components) { Interval = 450 };
            _scanTimer.Tick += (_, _) => ScanNow();
            _searchTimer = new System.Windows.Forms.Timer(_components) { Interval = 400 };
            _searchTimer.Tick += (_, _) => StartSearchNow();

            SuspendLayout();
            Text = Program.AppName;
            MinimumSize = new Size(780, 640);
            KeyPreview = true;
            AllowDrop = true;
            BuildUi();
            WireEvents();
            ApplyWindowPlacement();
            ResumeLayout(true);

            _autoCopy.Checked = _settings.AutoCopy;
            _sortCombo.SelectedIndex = (int)_settings.ProjectSort;
            UpdateSortDirectionButton();
            ShowNoProject();
        }

        private ProjectInfo? SelectedProject => _projectList.SelectedItem as ProjectInfo;

        private UndoTextBox[] StateBoxes => new[] { _promptBox, _includeBox, _excludeBox, _wildcardBox, _finalBox, _contentsBox };

        // ================================================================ startup / shutdown

        private void ApplyWindowPlacement()
        {
            StartPosition = FormStartPosition.Manual;
            var saved = _settings.Window;
            if (saved != null && saved.Width > 200 && saved.Height > 200)
            {
                var rect = new Rectangle(saved.X, saved.Y, saved.Width, saved.Height);
                if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect)))
                {
                    Bounds = rect;
                    if (saved.Maximized)
                        WindowState = FormWindowState.Maximized;
                    return;
                }
            }

            // Default: left half of the screen.
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Bounds = new Rectangle(area.Left, area.Top, area.Width / 2, area.Height);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplySplitter(_mainSplit, _settings.MainSplitRatio, 200, 420);
            ApplySplitter(_leftSplit, _settings.LeftSplitRatio, 150, 150);
            ApplySplitter(_outputSplit, _settings.OutputSplitRatio, 150, 150);
            _shown = true;

            RefreshProjectList();
            var start = _projects.FirstOrDefault(p => p.Id == _settings.SelectedProjectId);
            if (start != null)
                _projectList.SelectedItem = start;
            else
                SetStatus(_projects.Count == 0 ? "Add a project folder to get started (File > Add Project… or drop a folder on the Projects list)." : "Select a project.");

            foreach (string error in ThemeManager.Instance.LoadErrors)
                SetStatus(error);

            if (_current != null)
            {
                ActiveControl = _promptBox;
                _promptBox.Select(_promptBox.TextLength, 0);
            }
            else
                ActiveControl = _projectList;
        }

        private static void ApplySplitter(SplitContainer split, double ratio, int min1, int min2)
        {
            int size = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
            min1 = split.LogicalToDeviceUnits(min1);
            min2 = split.LogicalToDeviceUnits(min2);
            if (size < min1 + min2 + split.SplitterWidth)
                return;
            try
            {
                split.Panel1MinSize = min1;
                split.Panel2MinSize = min2;
                int distance = (int)Math.Round(size * Math.Clamp(ratio, 0.05, 0.95));
                split.SplitterDistance = Math.Clamp(distance, min1, size - min2 - split.SplitterWidth);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static double Ratio(SplitContainer split, double fallback)
        {
            int size = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
            return size > 0 ? split.SplitterDistance / (double)size : fallback;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel)
                return;

            _scanCts?.Cancel();
            FlushState();

            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _settings.Window = new WindowPlacement
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                Maximized = WindowState == FormWindowState.Maximized
            };
            if (_shown && WindowState != FormWindowState.Minimized)
            {
                _settings.MainSplitRatio = Ratio(_mainSplit, _settings.MainSplitRatio);
                _settings.LeftSplitRatio = Ratio(_leftSplit, _settings.LeftSplitRatio);
                _settings.OutputSplitRatio = Ratio(_outputSplit, _settings.OutputSplitRatio);
            }
            SaveSettings();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                JsonStore.Error -= OnStoreError;
                _scanCts?.Cancel();
                _components.Dispose();
            }
            base.Dispose(disposing);
        }

        // ================================================================ events

        private void WireEvents()
        {
            _projectList.Formatter = FormatProject;
            _projectList.SelectedIndexChanged += (_, _) =>
            {
                if (_suppressProjectSelection)
                    return;
                var project = SelectedProject;
                if (project != null && !ReferenceEquals(project, _current))
                    SwitchToProject(project);
            };
            _projectList.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Right)
                    return;
                int index = _projectList.IndexFromPoint(e.Location);
                if (index >= 0 && index != _projectList.SelectedIndex)
                    _projectList.SelectedIndex = index;
            };
            _projectList.MouseMove += (_, e) =>
            {
                int index = _projectList.IndexFromPoint(e.Location);
                string tip = index >= 0 && index < _projectList.Items.Count && _projectList.Items[index] is ProjectInfo p
                    ? $"{p.FolderPath}\r\nOpened: {p.LastOpenedAt:yyyy-MM-dd HH:mm}   Modified: {p.LastModifiedAt:yyyy-MM-dd HH:mm}"
                    : string.Empty;
                if (_tips.GetToolTip(_projectList) != tip)
                    _tips.SetToolTip(_projectList, tip);
            };
            _projectList.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.F2) { RenameProject(SelectedProject); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { RemoveProject(SelectedProject); e.Handled = true; }
            };
            _projectList.DragEnter += OnDragEnter;
            _projectList.DragDrop += OnDragDrop;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            _addProject.Click += (_, _) => AddProjectInteractive(null);
            _removeProject.Click += (_, _) => RemoveProject(SelectedProject);
            _sortCombo.SelectedIndexChanged += (_, _) => OnSortModeChanged();
            _sortDirection.Click += (_, _) =>
            {
                _settings.ProjectSortDescending = !_settings.ProjectSortDescending;
                UpdateSortDirectionButton();
                RefreshProjectList();
                SaveSettings();
            };

            _historyList.Formatter = FormatHistory;
            _historyList.DoubleClick += (_, _) => RestoreSelectedHistory();
            _historyList.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { RestoreSelectedHistory(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { DeleteSelectedHistory(); e.Handled = true; }
            };
            _historyList.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Right)
                    return;
                int index = _historyList.IndexFromPoint(e.Location);
                if (index >= 0 && !_historyList.SelectedIndices.Contains(index))
                {
                    _historyList.ClearSelected();
                    _historyList.SelectedIndex = index;
                }
            };
            _historyList.SelectedIndexChanged += (_, _) => UpdateHistoryButtons();
            _historyRestore.Click += (_, _) => RestoreSelectedHistory();
            _historyDelete.Click += (_, _) => DeleteSelectedHistory();
            _historyClear.Click += (_, _) => _historyClearMenu.Show(_historyClear, new Point(0, _historyClear.Height));

            _promptBox.TextChanged += (_, _) => MarkDirty();
            _finalBox.TextChanged += (_, _) => { UpdateOutputInfo(); MarkDirty(); };
            _contentsBox.TextChanged += (_, _) => { UpdateOutputInfo(); MarkDirty(); };
            foreach (var box in new[] { _includeBox, _excludeBox, _wildcardBox })
            {
                box.TextChanged += (_, _) =>
                {
                    if (_loading)
                        return;
                    MarkDirty();
                    ScheduleScan();
                };
            }

            _promptRtl.CheckedChanged += (_, _) =>
            {
                var direction = _promptRtl.Checked ? RightToLeft.Yes : RightToLeft.No;
                _promptBox.RightToLeft = direction;
                _finalBox.RightToLeft = direction;
                MarkDirty();
            };
            _promptTemplates.Click += (_, _) => OpenTemplates(TemplateKind.Prompt);
            _excludeTemplates.Click += (_, _) => OpenTemplates(TemplateKind.Exclude);

            _fileList.ItemCheck += OnFileItemCheck;
            _checkAll.Click += (_, _) => ToggleAllFiles();
            _fileSearch.TextChanged += (_, _) => OnFileSearchChanged();
            _fileSearch.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (IsSearchActive)
                        StartSearchNow();
                    e.Handled = e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape && _fileSearch.TextLength > 0)
                {
                    _fileSearch.Text = string.Empty;
                    e.Handled = e.SuppressKeyPress = true;
                }
            };
            _clearSearch.Click += (_, _) =>
            {
                _fileSearch.Text = string.Empty;
                _fileSearch.Focus();
            };
            _refreshFiles.Click += (_, _) => ScanNow();

            _generate.Click += async (_, _) => await GenerateAsync();
            _copy.Click += (_, _) => CopyToClipboard(_finalBox.Text, quiet: false);
            _autoCopy.CheckedChanged += (_, _) =>
            {
                _settings.AutoCopy = _autoCopy.Checked;
                SaveSettings();
            };
            _dontAddHistory.CheckedChanged += (_, _) => MarkDirty();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Enter))
            {
                _ = GenerateAsync();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnStoreError(string message)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
                BeginInvoke(() => SetStatus("⚠ " + message));
            else
                SetStatus("⚠ " + message);
        }

        private void SetStatus(string message) => _statusText.Text = message;

        // ================================================================ projects

        private ListItemText FormatProject(object item)
        {
            var p = (ProjectInfo)item;
            bool missing = !Directory.Exists(p.FolderPath);
            string secondary = missing
                ? "Folder not found: " + p.FolderPath
                : _settings.ProjectSort switch
                {
                    ProjectSortMode.DateOpened => $"Opened {p.LastOpenedAt:yyyy-MM-dd HH:mm}",
                    ProjectSortMode.LastModified => $"Modified {p.LastModifiedAt:yyyy-MM-dd HH:mm}",
                    _ => p.FolderPath
                };
            return new ListItemText(p.Name, secondary, missing);
        }

        private void OnSortModeChanged()
        {
            var mode = (ProjectSortMode)Math.Max(0, _sortCombo.SelectedIndex);
            if (mode != _settings.ProjectSort)
            {
                _settings.ProjectSort = mode;
                _settings.ProjectSortDescending = mode != ProjectSortMode.Name;
                UpdateSortDirectionButton();
                SaveSettings();
            }
            RefreshProjectList();
        }

        private void UpdateSortDirectionButton()
        {
            _sortDirection.Text = _settings.ProjectSortDescending ? "▼" : "▲";
            _tips.SetToolTip(_sortDirection, _settings.ProjectSortDescending ? "Descending (click for ascending)" : "Ascending (click for descending)");
        }

        private IEnumerable<ProjectInfo> SortedProjects()
        {
            IOrderedEnumerable<ProjectInfo> ordered = _settings.ProjectSort switch
            {
                ProjectSortMode.DateOpened => _settings.ProjectSortDescending
                    ? _projects.OrderByDescending(p => p.LastOpenedAt)
                    : _projects.OrderBy(p => p.LastOpenedAt),
                ProjectSortMode.LastModified => _settings.ProjectSortDescending
                    ? _projects.OrderByDescending(p => p.LastModifiedAt)
                    : _projects.OrderBy(p => p.LastModifiedAt),
                _ => _settings.ProjectSortDescending
                    ? _projects.OrderByDescending(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                    : _projects.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            };
            return ordered.ThenBy(p => p.FolderPath, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Rebuilds the project list (sorted) and keeps the current project selected.</summary>
        private void RefreshProjectList()
        {
            _suppressProjectSelection = true;
            try
            {
                _projectList.BeginUpdate();
                _projectList.Items.Clear();
                foreach (var project in SortedProjects())
                    _projectList.Items.Add(project);
                if (_current != null)
                    _projectList.SelectedItem = _current;
            }
            finally
            {
                _projectList.EndUpdate();
                _suppressProjectSelection = false;
            }
            _removeProject.Enabled = _projects.Count > 0;
        }

        private void AddProjectInteractive(string? folder)
        {
            if (folder == null)
            {
                using var browser = new FolderBrowserDialog
                {
                    Description = "Select the project folder",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false
                };
                if (_current != null && Directory.Exists(_current.FolderPath))
                    browser.InitialDirectory = Directory.GetParent(_current.FolderPath)?.FullName ?? _current.FolderPath;
                if (browser.ShowDialog(this) != DialogResult.OK)
                    return;
                folder = browser.SelectedPath;
            }

            folder = PathUtil.Normalize(folder);
            var existing = _projects.FirstOrDefault(p => PathUtil.AreEqual(p.FolderPath, folder));
            if (existing != null)
            {
                _projectList.SelectedItem = existing;
                SetStatus($"'{existing.Name}' is already in the project list.");
                return;
            }

            var detection = ProjectTypeDetector.Detect(folder);
            using var dialog = new NewProjectDialog(folder, _templateRepository, detection, _settings);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            _settings.LastNewProjectPromptTemplate = dialog.PromptTemplateName;
            _settings.LastNewProjectExcludeTemplate = dialog.ExcludeTemplateName;
            _settings.LastNewProjectFilePatterns = TextUtil.NormalizeNewLines(dialog.FilePatterns);
            SaveSettings();

            var now = DateTime.Now;
            var project = new ProjectInfo
            {
                Id = Guid.NewGuid(),
                Name = dialog.ProjectName,
                FolderPath = folder,
                AddedAt = now,
                LastOpenedAt = now,
                LastModifiedAt = now
            };

            FlushState();
            _projectRepository.SaveState(project.Id, dialog.CreateInitialState());
            _projects.Add(project);
            _projectRepository.SaveProjects(_projects);
            RefreshProjectList();
            _projectList.SelectedItem = project;
            SetStatus($"Project '{project.Name}' added.");
        }

        private void RenameProject(ProjectInfo? project)
        {
            if (project == null)
                return;
            string? name = InputDialog.Ask(this, "Rename Project", "Project name:", project.Name);
            if (name == null)
                return;
            project.Name = name.Length > 0 ? name : PathUtil.DefaultProjectName(project.FolderPath);
            _projectRepository.SaveProjects(_projects);
            RefreshProjectList();
            UpdateTitle();
        }

        private void RemoveProject(ProjectInfo? project)
        {
            if (project == null)
                return;
            if (!Ui.Confirm(this, $"Remove '{project.Name}' from the project list?\r\n\r\n" +
                    "Its saved state and prompt history are deleted. The folder on disk is not touched."))
                return;

            int index = _projectList.SelectedIndex;
            if (ReferenceEquals(project, _current))
            {
                _saveTimer.Stop();
                _stateDirty = false;
                _current = null;
            }

            _undoByProject.Remove(project.Id);
            _projects.Remove(project);
            _projectRepository.DeleteState(project.Id);
            _historyRepository.Delete(project.Id);
            _projectRepository.SaveProjects(_projects);
            RefreshProjectList();

            if (_current == null)
            {
                if (_projectList.Items.Count > 0)
                    _projectList.SelectedIndex = Math.Clamp(index, 0, _projectList.Items.Count - 1);
                else
                    SwitchToProject(null);
            }
            SetStatus($"Project '{project.Name}' removed from the list.");
        }

        private void OpenProjectFolder(bool parent)
        {
            var project = SelectedProject;
            if (project == null)
                return;
            string? folder = parent ? Directory.GetParent(project.FolderPath)?.FullName : project.FolderPath;
            if (folder == null)
            {
                Ui.Info(this, "This folder has no parent folder.");
                return;
            }
            OpenFolderSafe(folder);
        }

        private void OpenFolderSafe(string folder)
        {
            if (!Directory.Exists(folder))
            {
                Ui.Info(this, $"The folder does not exist:\r\n{folder}");
                return;
            }
            try
            {
                ShellHelper.OpenFolder(folder);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                Ui.Error(this, $"The folder could not be opened:\r\n{folder}\r\n\r\n{ex.Message}");
            }
        }

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths)
                return;
            string path = paths[0];
            string? folder = Directory.Exists(path) ? path : File.Exists(path) ? Path.GetDirectoryName(path) : null;
            if (folder != null)
                BeginInvoke(() => AddProjectInteractive(folder)); // let the drag operation finish first
        }

        // ================================================================ project state

        private void SwitchToProject(ProjectInfo? project)
        {
            if (_current != null)
            {
                FlushState();
                _undoByProject[_current.Id] = StateBoxes.Select(b => b.DetachHistory()).ToArray();
            }

            _scanTimer.Stop();
            _scanCts?.Cancel();
            _current = project;

            if (project == null)
            {
                ShowNoProject();
                return;
            }

            project.LastOpenedAt = DateTime.Now;
            var state = _projectRepository.LoadState(project.Id) ?? new ProjectState();
            LoadStateIntoControls(state);

            if (_undoByProject.TryGetValue(project.Id, out var histories))
            {
                var boxes = StateBoxes;
                for (int i = 0; i < boxes.Length && i < histories.Length; i++)
                    boxes[i].AttachHistory(histories[i]);
            }

            _history = _historyRepository.Load(project.Id);
            RefreshHistoryList();

            _workArea.Enabled = true;
            _historySection.Enabled = true;
            UpdateTitle();
            _statusPath.Text = project.FolderPath;
            _settings.SelectedProjectId = project.Id;
            SaveSettings();
            _projectRepository.SaveProjects(_projects);
            _projectList.Invalidate();

            if (Directory.Exists(project.FolderPath))
                SetStatus($"Project '{project.Name}' loaded.");
            else
                SetStatus($"⚠ The project folder was not found: {project.FolderPath}");
            ScanNow();
        }

        private void LoadStateIntoControls(ProjectState state)
        {
            _loading = true;
            try
            {
                _promptRtl.Checked = state.PromptRightToLeft;
                _promptBox.SetTextWithoutUndo(TextUtil.NormalizeNewLines(state.UserPrompt));
                _includeBox.SetTextWithoutUndo(TextUtil.NormalizeNewLines(state.IncludePatterns));
                _excludeBox.SetTextWithoutUndo(TextUtil.NormalizeNewLines(state.ExcludePatterns));
                _wildcardBox.SetTextWithoutUndo(TextUtil.NormalizeNewLines(state.FilePatterns));
                _finalBox.SetTextWithoutUndo(state.FinalPrompt);
                _contentsBox.SetTextWithoutUndo(state.FileContents);
                _dontAddHistory.Checked = state.DontAddToHistory;

                _unchecked.Clear();
                foreach (string file in state.UncheckedFiles)
                    _unchecked.Add(file);
                _allFiles = new List<string>();
                _noFilePatterns = false;
                ClearFileSearch();
                _searcher.Clear();
                _fileList.Items.Clear();
                UpdateFileCount();
                UpdateOutputInfo();
            }
            finally
            {
                _loading = false;
            }
        }

        private ProjectState CaptureState() => new()
        {
            UserPrompt = _promptBox.Text,
            PromptRightToLeft = _promptRtl.Checked,
            IncludePatterns = _includeBox.Text,
            ExcludePatterns = _excludeBox.Text,
            FilePatterns = _wildcardBox.Text,
            UncheckedFiles = _unchecked.OrderBy(f => f, RelativePathComparer.Instance).ToList(),
            FinalPrompt = _finalBox.Text,
            FileContents = _contentsBox.Text,
            DontAddToHistory = _dontAddHistory.Checked
        };

        private void MarkDirty()
        {
            if (_loading || _current == null)
                return;
            _stateDirty = true;
            _current.LastModifiedAt = DateTime.Now;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        /// <summary>Writes the current project's state to disk if it changed.</summary>
        private void FlushState()
        {
            _saveTimer.Stop();
            if (_current == null || !_stateDirty)
                return;
            _projectRepository.SaveState(_current.Id, CaptureState());
            _projectRepository.SaveProjects(_projects);
            _stateDirty = false;
            if (_settings.ProjectSort == ProjectSortMode.LastModified)
                _projectList.Invalidate();
        }

        private void ShowNoProject()
        {
            LoadStateIntoControls(new ProjectState { IncludePatterns = string.Empty });
            _history = new List<HistoryEntry>();
            RefreshHistoryList();
            _workArea.Enabled = false;
            _historySection.Enabled = false;
            _statusPath.Text = string.Empty;
            _fileCount.Text = string.Empty;
            UpdateTitle();
        }

        private void UpdateTitle() => Text = _current == null ? Program.AppName : $"{_current.Name} — {Program.AppName}";

        private void SaveSettings()
        {
            _settings.ThemeName = ThemeManager.Instance.SelectedName;
            JsonStore.Save(_paths.SettingsFile, _settings);
        }

        // ================================================================ file list

        private void ScheduleScan()
        {
            _scanTimer.Stop();
            _scanTimer.Start();
            _fileCount.Text = "Waiting for input…";
        }

        private void ScanNow()
        {
            _scanTimer.Stop();
            _scanTask = ScanAndPopulateAsync();
        }

        private async Task ScanAndPopulateAsync()
        {
            _scanCts?.Cancel();
            var cts = new CancellationTokenSource();
            _scanCts = cts;

            var project = _current;
            if (project == null)
                return;

            if (!Directory.Exists(project.FolderPath))
            {
                PopulateFiles(new ScanResult());
                _fileCount.Text = "Project folder not found.";
                return;
            }

            var request = new ScanRequest(project.FolderPath,
                TextUtil.Lines(_includeBox.Text), TextUtil.Lines(_excludeBox.Text), TextUtil.Lines(_wildcardBox.Text));
            _fileCount.Text = "Scanning…";

            try
            {
                var result = await Task.Run(() => FileScanner.Scan(request, cts.Token), cts.Token);
                if (cts.IsCancellationRequested || !ReferenceEquals(project, _current))
                    return;
                PopulateFiles(result);
                if (result.Truncated)
                    SetStatus($"⚠ More than {FileScanner.MaxFiles:N0} files match. Only the first {FileScanner.MaxFiles:N0} are listed; narrow the filters.");
                else if (result.InaccessibleFolders > 0)
                    SetStatus($"{result.InaccessibleFolders} folder(s) could not be read and were skipped.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                if (ReferenceEquals(project, _current))
                {
                    PopulateFiles(new ScanResult());
                    SetStatus("⚠ Scanning failed: " + ex.Message);
                }
            }
        }

        /// <summary>Waits until the file list reflects the current filter boxes.</summary>
        private async Task EnsureScanCompletedAsync()
        {
            while (_scanTimer.Enabled || !_scanTask.IsCompleted)
            {
                if (_scanTimer.Enabled)
                    ScanNow();
                await _scanTask;
            }
        }

        private void PopulateFiles(ScanResult result)
        {
            _allFiles = result.Files;
            _noFilePatterns = result.NoFilePatterns;
            RebuildFileList();
            if (IsSearchActive)
                StartSearchNow(); // the found files may have changed
        }

        private bool IsSearchActive => _fileSearch.Text.Trim().Length > 0;

        /// <summary>Shows all found files, or only the files matching the search while a search is active.</summary>
        private void RebuildFileList()
        {
            IEnumerable<string> visible = !IsSearchActive
                ? _allFiles
                : _searchMatches == null ? Enumerable.Empty<string>() : _allFiles.Where(_searchMatches.Contains);

            _populatingFiles = true;
            try
            {
                _fileList.BeginUpdate();
                _fileList.Items.Clear();
                foreach (string file in visible)
                    _fileList.Items.Add(file, !_unchecked.Contains(file));
            }
            finally
            {
                _fileList.EndUpdate();
                _populatingFiles = false;
            }
            UpdateFileCount();
        }

        private void OnFileSearchChanged()
        {
            _searchTimer.Stop();
            _searchCts?.Cancel();
            if (!IsSearchActive)
            {
                _searchMatches = null;
                _searching = false;
                RebuildFileList();
                return;
            }
            _searching = true;
            UpdateFileCount();
            _searchTimer.Start();
        }

        private void StartSearchNow()
        {
            _searchTimer.Stop();
            _ = RunSearchAsync();
        }

        private async Task RunSearchAsync()
        {
            _searchCts?.Cancel();
            var project = _current;
            string phrase = _fileSearch.Text.Trim();
            if (project == null || phrase.Length == 0)
                return;

            var cts = new CancellationTokenSource();
            _searchCts = cts;
            _searching = true;
            UpdateFileCount();

            var files = _allFiles.ToList();
            string root = project.FolderPath;
            try
            {
                var matches = await Task.Run(() => _searcher.Search(root, files, phrase, cts.Token), cts.Token);
                if (cts.IsCancellationRequested || !ReferenceEquals(project, _current) || _fileSearch.Text.Trim() != phrase)
                    return;
                _searchMatches = matches;
                _searching = false;
                RebuildFileList();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ClearFileSearch()
        {
            _searchTimer.Stop();
            _searchCts?.Cancel();
            _searchMatches = null;
            _searching = false;
            _fileSearch.SetTextWithoutUndo(string.Empty);
        }

        private void OnFileItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (_populatingFiles || _loading)
                return;
            string file = (string)_fileList.Items[e.Index];
            if (e.NewValue == CheckState.Checked)
                _unchecked.Remove(file);
            else
                _unchecked.Add(file);
            MarkDirty();
            BeginInvoke(UpdateFileCount); // CheckedIndices is updated after this event
        }

        private void ToggleAllFiles()
        {
            if (_fileList.Items.Count == 0)
                return;
            bool check = _checkAll.CheckState != CheckState.Checked;

            _populatingFiles = true;
            try
            {
                _fileList.BeginUpdate();
                for (int i = 0; i < _fileList.Items.Count; i++)
                {
                    _fileList.SetItemChecked(i, check);
                    string file = (string)_fileList.Items[i];
                    if (check)
                        _unchecked.Remove(file);
                    else
                        _unchecked.Add(file);
                }
            }
            finally
            {
                _fileList.EndUpdate();
                _populatingFiles = false;
            }
            MarkDirty();
            UpdateFileCount();
        }

        private void UpdateFileCount()
        {
            int shown = _fileList.Items.Count;
            int shownChecked = _fileList.CheckedIndices.Count;
            _checkAll.CheckState = shown == 0 || shownChecked == 0 ? CheckState.Unchecked
                : shownChecked == shown ? CheckState.Checked
                : CheckState.Indeterminate;
            _checkAll.Enabled = shown > 0;

            int total = _allFiles.Count;
            int selectedTotal = _allFiles.Count(f => !_unchecked.Contains(f));

            if (_noFilePatterns && total == 0)
                _fileCount.Text = "Enter file wildcards (e.g. *.cs) to list files.";
            else if (!IsSearchActive)
                _fileCount.Text = total == 0 ? "No matching files." : $"{selectedTotal:N0} of {total:N0} files selected";
            else if (_searching)
                _fileCount.Text = "Searching…";
            else if (shown == 0)
                _fileCount.Text = $"No file contains this text · {selectedTotal:N0}/{total:N0} selected in total";
            else
                _fileCount.Text = $"{shown:N0} found, {shownChecked:N0} checked · {selectedTotal:N0}/{total:N0} in total";
        }

        // ================================================================ generate / copy

        private async Task GenerateAsync()
        {
            if (_current == null || _generating)
                return;

            _generating = true;
            _generate.Enabled = false;
            UseWaitCursor = true;
            try
            {
                SetStatus("Generating prompt…");
                await EnsureScanCompletedAsync();
                var project = _current;
                if (project == null)
                    return;

                // All checked files, including those hidden by an active search.
                var selected = _allFiles.Where(f => !_unchecked.Contains(f)).ToList();
                string root = project.FolderPath;
                var read = await Task.Run(() => FileContentReader.ReadFiles(root, selected));
                if (!ReferenceEquals(project, _current))
                    return;

                string userPrompt = _promptBox.Text;
                string contents = PromptBuilder.BuildFileContents(read.Files);
                string final = PromptBuilder.Compose(userPrompt, contents, _settings.BlankLinesAfterPrompt);

                if (final.Length == 0)
                {
                    SetStatus("Nothing to generate: the prompt is empty and no file is selected.");
                    return;
                }

                _contentsBox.Text = contents;
                _finalBox.Text = final;

                string historyNote = AddToHistory(project, selected, contents);
                bool copied = _autoCopy.Checked && CopyToClipboard(final, quiet: true);

                SetStatus($"Prompt generated: {read.Files.Count:N0} file(s), {final.Length:N0} characters" +
                    (copied ? ", copied to the clipboard." : ".") + historyNote);

                if (read.Warnings.Count > 0)
                {
                    var lines = read.Warnings.Take(25).ToList();
                    if (read.Warnings.Count > 25)
                        lines.Add($"… and {read.Warnings.Count - 25} more.");
                    MessageBox.Show(this, string.Join("\r\n", lines), "Some files were skipped",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                _generating = false;
                _generate.Enabled = true;
                UseWaitCursor = false;
            }
        }

        private bool CopyToClipboard(string text, bool quiet)
        {
            if (string.IsNullOrEmpty(text))
            {
                SetStatus("Nothing to copy: the final prompt is empty.");
                return false;
            }
            try
            {
                Clipboard.SetDataObject(text, true, 10, 100);
                if (!quiet)
                    SetStatus($"Final prompt copied to the clipboard ({text.Length:N0} characters).");
                return true;
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                SetStatus("⚠ The clipboard is in use by another program: " + ex.Message);
                return false;
            }
        }

        private void UpdateOutputInfo()
        {
            _finalInfo.Text = Describe(_finalBox.TextLength);
            _contentsInfo.Text = Describe(_contentsBox.TextLength);

            static string Describe(int length) =>
                length == 0 ? string.Empty : $"{length:N0} chars · ~{length / 4:N0} tokens";
        }

        // ================================================================ history

        private string AddToHistory(ProjectInfo project, List<string> selected, string contents)
        {
            if (_dontAddHistory.Checked)
                return " Not added to history.";

            var entry = new HistoryEntry
            {
                CreatedAt = DateTime.Now,
                ProjectName = project.Name,
                UserPrompt = _promptBox.Text,
                PromptRightToLeft = _promptRtl.Checked,
                IncludePatterns = _includeBox.Text,
                ExcludePatterns = _excludeBox.Text,
                FilePatterns = _wildcardBox.Text,
                SelectedFiles = selected,
                UncheckedFiles = _unchecked.ToList(),
                FileContents = contents
            };

            if (_history.Count > 0 && _history[0].HasSameContentAs(entry))
                return " Same as the latest history entry, not added again.";

            _history.Insert(0, entry);
            _historyRepository.Save(project.Id, _history);
            RefreshHistoryList();
            _historyList.ClearSelected();
            if (_historyList.Items.Count > 0)
                _historyList.SelectedIndex = 0;
            return " Added to history.";
        }

        private ListItemText FormatHistory(object item)
        {
            var h = (HistoryEntry)item;
            string request = TextUtil.LastNonEmptyLine(h.UserPrompt, 120);
            string files = h.SelectedFiles.Count == 1 ? "1 file" : $"{h.SelectedFiles.Count:N0} files";
            return new ListItemText($"{h.ProjectName} — {h.CreatedAt:yyyy-MM-dd HH:mm:ss}",
                request.Length > 0 ? $"{files} · {request}" : files);
        }

        private void RefreshHistoryList()
        {
            _historyList.BeginUpdate();
            _historyList.Items.Clear();
            foreach (var entry in _history)
                _historyList.Items.Add(entry);
            _historyList.EndUpdate();
            UpdateHistoryButtons();
        }

        private void UpdateHistoryButtons()
        {
            _historyRestore.Enabled = _historyList.SelectedIndices.Count == 1;
            _historyDelete.Enabled = _historyList.SelectedIndices.Count > 0;
            _historyClear.Enabled = _history.Count > 0;
        }

        private HistoryEntry? SingleSelectedHistory =>
            _historyList.SelectedIndices.Count == 1 ? _historyList.SelectedItem as HistoryEntry : null;

        private void RestoreSelectedHistory()
        {
            var entry = SingleSelectedHistory;
            if (entry == null || _current == null)
                return;

            _unchecked.Clear();
            foreach (string file in entry.UncheckedFiles)
                _unchecked.Add(file);

            _promptRtl.Checked = entry.PromptRightToLeft;
            _promptBox.Text = entry.UserPrompt;
            _includeBox.Text = entry.IncludePatterns;
            _excludeBox.Text = entry.ExcludePatterns;
            _wildcardBox.Text = entry.FilePatterns;
            _contentsBox.Text = entry.FileContents;
            _finalBox.Text = PromptBuilder.Compose(entry.UserPrompt, entry.FileContents, _settings.BlankLinesAfterPrompt);
            MarkDirty();
            ScanNow();
            SetStatus($"Restored the history entry of {entry.CreatedAt:yyyy-MM-dd HH:mm:ss}. Use Ctrl+Z in a box to go back.");
        }

        private void CopySelectedHistory()
        {
            var entry = SingleSelectedHistory;
            if (entry != null)
                CopyToClipboard(PromptBuilder.Compose(entry.UserPrompt, entry.FileContents, _settings.BlankLinesAfterPrompt), quiet: false);
        }

        private void DeleteSelectedHistory()
        {
            if (_current == null || _historyList.SelectedIndices.Count == 0)
                return;

            var toDelete = _historyList.SelectedItems.Cast<HistoryEntry>().ToHashSet();
            int firstIndex = _historyList.SelectedIndices.Cast<int>().Min();
            _history.RemoveAll(toDelete.Contains);
            _historyRepository.Save(_current.Id, _history);
            RefreshHistoryList();
            if (_historyList.Items.Count > 0)
                _historyList.SelectedIndex = Math.Min(firstIndex, _historyList.Items.Count - 1);
            SetStatus(toDelete.Count == 1 ? "1 history entry deleted." : $"{toDelete.Count} history entries deleted.");
        }

        private void ClearHistory(bool keepLatest)
        {
            if (_current == null || _history.Count == 0)
                return;
            int count = keepLatest ? _history.Count - 1 : _history.Count;
            if (count <= 0)
                return;
            string message = keepLatest
                ? $"Delete {count} history entr{(count == 1 ? "y" : "ies")} of '{_current.Name}' and keep only the latest one?"
                : $"Delete all {count} history entr{(count == 1 ? "y" : "ies")} of '{_current.Name}'?";
            if (!Ui.Confirm(this, message))
                return;

            _history = keepLatest ? _history.Take(1).ToList() : new List<HistoryEntry>();
            _historyRepository.Save(_current.Id, _history);
            RefreshHistoryList();
            SetStatus($"{count} history entr{(count == 1 ? "y" : "ies")} deleted.");
        }

        // ================================================================ templates / themes / about

        private void OpenTemplates(TemplateKind kind)
        {
            string? last = kind == TemplateKind.Prompt ? _settings.LastPromptTemplateName : _settings.LastExcludeTemplateName;
            using var dialog = new TemplatesDialog(kind, _templateRepository, last);
            var result = dialog.ShowDialog(this);

            if (kind == TemplateKind.Prompt)
                _settings.LastPromptTemplateName = dialog.LastSelectedName;
            else
                _settings.LastExcludeTemplateName = dialog.LastSelectedName;
            SaveSettings();

            if (result != DialogResult.OK || dialog.ChosenText == null || _current == null)
                return;

            if (kind == TemplateKind.Prompt)
            {
                _promptRtl.Checked = TextUtil.ContainsRtl(dialog.ChosenText);
                _promptBox.Text = dialog.ChosenText;
                _promptBox.Focus();
                _promptBox.Select(_promptBox.TextLength, 0);
                _promptBox.ScrollToCaret();
            }
            else
            {
                _excludeBox.Text = dialog.ChosenText;
            }
            SetStatus("Template applied. Press Ctrl+Z in the box to restore the previous text.");
        }

        protected override void ApplyTheme()
        {
            base.ApplyTheme();
            ThemeManager.Instance.ApplyToToolStrip(_historyClearMenu);
        }

        private void SelectTheme(string name)
        {
            ThemeManager.Instance.Select(name);
            RebuildThemeMenu();
            SaveSettings();
        }

        private void ReloadThemes()
        {
            ThemeManager.Instance.ReloadThemes();
            ThemeManager.Instance.Refresh();
            RebuildThemeMenu();
            SetStatus(ThemeManager.Instance.LoadErrors.Count > 0
                ? "⚠ " + string.Join(" ", ThemeManager.Instance.LoadErrors)
                : $"{ThemeManager.Instance.Themes.Count} themes loaded.");
        }

        private void ShowAbout()
        {
            string version = Application.ProductVersion.Split('+')[0];
            Ui.Info(this, $"{Program.AppName} {version}\r\n\r\n" +
                "Combines your prompt with selected source files of a project, ready to paste into an online AI assistant.\r\n\r\n" +
                $"Data folder:\r\n{_paths.Root}");
        }
    }
}
