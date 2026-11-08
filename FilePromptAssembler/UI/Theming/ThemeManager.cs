using FilePromptAssembler.Services;
using Microsoft.Win32;

namespace FilePromptAssembler.UI.Theming
{
    /// <summary>Implemented by custom controls that paint themselves.</summary>
    public interface IThemeable
    {
        void ApplyTheme(Theme theme);
    }

    /// <summary>
    /// Holds the available themes, resolves the "System" choice from the Windows app theme and applies
    /// colors to a control tree.
    /// </summary>
    public sealed class ThemeManager : IDisposable
    {
        public const string SystemThemeName = "System";
        public const string PrimaryTag = "primary";
        public const string SubtleTag = "subtle";

        private static ThemeManager? _instance;
        private readonly string _themesDirectory;
        private readonly List<Theme> _themes = new();

        private ThemeManager(string themesDirectory, string selectedName)
        {
            _themesDirectory = themesDirectory;
            ReloadThemes();
            SelectedName = IsKnown(selectedName) ? selectedName : SystemThemeName;
            Current = Resolve(SelectedName);
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        public static ThemeManager Instance => _instance ?? throw new InvalidOperationException("ThemeManager is not initialized.");

        public IReadOnlyList<Theme> Themes => _themes;
        public string SelectedName { get; private set; }
        public Theme Current { get; private set; }
        public List<string> LoadErrors { get; } = new();

        /// <summary>Raised when <see cref="Current"/> changes. May be raised on a non-UI thread.</summary>
        public event EventHandler? ThemeChanged;

        public static ThemeManager Initialize(string themesDirectory, string selectedName)
        {
            _instance = new ThemeManager(themesDirectory, selectedName);
            return _instance;
        }

        public void ReloadThemes()
        {
            _themes.Clear();
            LoadErrors.Clear();
            _themes.Add(BuiltInThemes.Light);
            _themes.Add(BuiltInThemes.Dark);

            WriteReadme();
            if (!Directory.Exists(_themesDirectory))
                return;

            foreach (string file in Directory.EnumerateFiles(_themesDirectory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var theme = JsonStore.Load<Theme>(file);
                if (theme == null || string.IsNullOrWhiteSpace(theme.Name))
                {
                    LoadErrors.Add($"Theme file '{Path.GetFileName(file)}' could not be loaded.");
                    continue;
                }
                if (theme.Name.Equals(SystemThemeName, StringComparison.OrdinalIgnoreCase) || _themes.Any(t => t.Name.Equals(theme.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    LoadErrors.Add($"Theme '{theme.Name}' in '{Path.GetFileName(file)}' uses a name that already exists.");
                    continue;
                }
                _themes.Add(theme);
            }
        }

        public void Select(string name)
        {
            SelectedName = IsKnown(name) ? name : SystemThemeName;
            Current = Resolve(SelectedName);
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Re-resolves the current theme (e.g. after <see cref="ReloadThemes"/>).</summary>
        public void Refresh() => Select(SelectedName);

        public static bool IsWindowsInDarkMode()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }

        private bool IsKnown(string name) =>
            name.Equals(SystemThemeName, StringComparison.OrdinalIgnoreCase)
            || _themes.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        private Theme Resolve(string name)
        {
            if (name.Equals(SystemThemeName, StringComparison.OrdinalIgnoreCase))
                return IsWindowsInDarkMode() ? BuiltInThemes.Dark : BuiltInThemes.Light;
            return _themes.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? BuiltInThemes.Light;
        }

        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General || !SelectedName.Equals(SystemThemeName, StringComparison.OrdinalIgnoreCase))
                return;

            var resolved = Resolve(SelectedName);
            if (!ReferenceEquals(resolved, Current))
            {
                Current = resolved;
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void WriteReadme()
        {
            try
            {
                Directory.CreateDirectory(_themesDirectory);
                string readme = Path.Combine(_themesDirectory, "README.txt");
                if (File.Exists(readme))
                    return;

                var sample = new Theme
                {
                    Name = "My Theme",
                    IsDark = BuiltInThemes.Light.IsDark,
                    Background = BuiltInThemes.Light.Background,
                    Surface = BuiltInThemes.Light.Surface,
                    Input = BuiltInThemes.Light.Input,
                    Border = BuiltInThemes.Light.Border,
                    Text = BuiltInThemes.Light.Text,
                    SubtleText = BuiltInThemes.Light.SubtleText,
                    Accent = BuiltInThemes.Light.Accent,
                    AccentHover = BuiltInThemes.Light.AccentHover,
                    AccentText = BuiltInThemes.Light.AccentText,
                    Button = BuiltInThemes.Light.Button,
                    ButtonHover = BuiltInThemes.Light.ButtonHover,
                    Selection = BuiltInThemes.Light.Selection,
                    SelectionText = BuiltInThemes.Light.SelectionText
                };
                string json = System.Text.Json.JsonSerializer.Serialize(sample, JsonStore.Options);
                File.WriteAllText(readme,
                    "Custom themes\r\n=============\r\n\r\n" +
                    "Put one *.json file per theme in this folder, then choose View > Theme > Reload Themes.\r\n" +
                    "Colors are written as \"#RRGGBB\". Set \"IsDark\" to true for dark themes (dark title bar and scroll bars).\r\n\r\n" +
                    "Example (the built-in Light theme):\r\n\r\n" + json + "\r\n");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        // ---------------------------------------------------------------- applying

        public void Apply(Control root) => ApplyTo(root, Current);

        public void ApplyToToolStrip(ToolStrip strip) => ApplyToToolStrip(strip, Current);

        private static void ApplyTo(Control control, Theme t)
        {
            switch (control)
            {
                case IThemeable themeable:
                    themeable.ApplyTheme(t);
                    break;
                case Form form:
                    form.BackColor = t.Background;
                    form.ForeColor = t.Text;
                    NativeMethods.SetDarkTitleBar(form, t.IsDark);
                    break;
                case ToolStrip strip:
                    ApplyToToolStrip(strip, t);
                    break;
                case Button button:
                    StyleButton(button, t);
                    break;
                case CheckBox checkBox:
                    StyleCheckBox(checkBox, t);
                    break;
                case TextBoxBase textBox:
                    textBox.BackColor = t.Input;
                    textBox.ForeColor = t.Text;
                    NativeMethods.SetControlTheme(textBox);
                    break;
                case ListBox listBox:
                    listBox.BackColor = t.Input;
                    listBox.ForeColor = t.Text;
                    NativeMethods.SetControlTheme(listBox);
                    break;
                case ComboBox comboBox:
                    comboBox.FlatStyle = FlatStyle.Flat;
                    comboBox.BackColor = t.Input;
                    comboBox.ForeColor = t.Text;
                    NativeMethods.SetControlTheme(comboBox);
                    break;
                case Label label:
                    label.ForeColor = Equals(label.Tag, SubtleTag) ? t.SubtleText : t.Text;
                    break;
                case SplitContainer split:
                    split.BackColor = t.Background;
                    split.Panel1.BackColor = t.Background;
                    split.Panel2.BackColor = t.Background;
                    break;
            }

            if (control.ContextMenuStrip != null)
                ApplyToToolStrip(control.ContextMenuStrip, t);

            foreach (Control child in control.Controls)
                ApplyTo(child, t);
        }

        private static void StyleButton(Button b, Theme t)
        {
            bool primary = Equals(b.Tag, PrimaryTag);
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = primary ? t.Accent : t.Button;
            b.ForeColor = primary ? t.AccentText : t.Text;
            b.FlatAppearance.BorderColor = primary ? t.Accent : t.Border;
            b.FlatAppearance.MouseOverBackColor = primary ? t.AccentHover : t.ButtonHover;
            b.FlatAppearance.MouseDownBackColor = primary ? t.AccentHover : t.Selection;
        }

        private static void StyleCheckBox(CheckBox c, Theme t)
        {
            c.ForeColor = t.Text;
            if (c.Appearance == Appearance.Button)
            {
                c.FlatStyle = FlatStyle.Flat;
                c.UseVisualStyleBackColor = false;
                c.BackColor = t.Button;
                c.FlatAppearance.BorderColor = t.Border;
                c.FlatAppearance.CheckedBackColor = t.Selection;
                c.FlatAppearance.MouseOverBackColor = t.ButtonHover;
                c.FlatAppearance.MouseDownBackColor = t.Selection;
            }
        }

        private static void ApplyToToolStrip(ToolStrip strip, Theme t)
        {
            strip.Renderer = new ThemedToolStripRenderer(t);
            strip.BackColor = strip is ToolStripDropDown ? t.Surface : t.Background;
            strip.ForeColor = t.Text;
            foreach (ToolStripItem item in strip.Items)
                ApplyToItem(item, t);
        }

        private static void ApplyToItem(ToolStripItem item, Theme t)
        {
            item.ForeColor = t.Text;
            if (item is ToolStripDropDownItem dropDownItem && dropDownItem.HasDropDownItems)
                ApplyToToolStrip(dropDownItem.DropDown, t);
        }

        public void Dispose()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
    }
}
