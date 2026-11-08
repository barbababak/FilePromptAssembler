namespace FilePromptAssembler.Services
{
    /// <summary>Locations of all data files. Everything lives in a "Data" folder next to the executable.</summary>
    public sealed class AppPaths
    {
        public AppPaths(string root)
        {
            Root = Path.GetFullPath(root);
        }

        public string Root { get; }
        public string SettingsFile => Path.Combine(Root, "settings.json");
        public string ProjectsFile => Path.Combine(Root, "projects.json");
        public string TemplatesFile => Path.Combine(Root, "templates.json");
        public string ProjectStatesDirectory => Path.Combine(Root, "Projects");
        public string HistoryDirectory => Path.Combine(Root, "History");
        public string ThemesDirectory => Path.Combine(Root, "Themes");

        public void EnsureCreated()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(ProjectStatesDirectory);
            Directory.CreateDirectory(HistoryDirectory);
            Directory.CreateDirectory(ThemesDirectory);
        }
    }
}
