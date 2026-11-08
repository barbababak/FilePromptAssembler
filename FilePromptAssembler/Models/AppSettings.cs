namespace FilePromptAssembler.Models
{
    public enum ProjectSortMode
    {
        Name,
        DateOpened,
        LastModified
    }

    public sealed class WindowPlacement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Maximized { get; set; }
    }

    /// <summary>Application-wide settings (not tied to a single project).</summary>
    public sealed class AppSettings
    {
        public string ThemeName { get; set; } = "System";
        public ProjectSortMode ProjectSort { get; set; } = ProjectSortMode.DateOpened;
        public bool ProjectSortDescending { get; set; } = true;
        public Guid? SelectedProjectId { get; set; }
        public bool AutoCopy { get; set; } = true;
        public int BlankLinesAfterPrompt { get; set; } = 15;

        public WindowPlacement? Window { get; set; }
        public double MainSplitRatio { get; set; } = 0.27;
        public double LeftSplitRatio { get; set; } = 0.55;
        public double OutputSplitRatio { get; set; } = 0.5;

        public string? LastNewProjectPromptTemplate { get; set; }
        public string? LastNewProjectExcludeTemplate { get; set; }
        public string LastNewProjectFilePatterns { get; set; } = string.Empty;

        public string? LastPromptTemplateName { get; set; }
        public string? LastExcludeTemplateName { get; set; }
    }
}
