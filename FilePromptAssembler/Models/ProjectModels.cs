namespace FilePromptAssembler.Models
{
    /// <summary>An entry of the project list.</summary>
    public sealed class ProjectInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public DateTime LastOpenedAt { get; set; } = DateTime.Now;
        public DateTime LastModifiedAt { get; set; } = DateTime.Now;

        public override string ToString() => Name;
    }

    /// <summary>The complete, automatically saved working state of one project.</summary>
    public sealed class ProjectState
    {
        public string UserPrompt { get; set; } = string.Empty;
        public bool PromptRightToLeft { get; set; }
        public string IncludePatterns { get; set; } = "*";
        public string ExcludePatterns { get; set; } = string.Empty;
        public string FilePatterns { get; set; } = string.Empty;

        /// <summary>Relative paths the user unchecked. Every other found file is checked.</summary>
        public List<string> UncheckedFiles { get; set; } = new();

        public string FinalPrompt { get; set; } = string.Empty;
        public string FileContents { get; set; } = string.Empty;
        public bool DontAddToHistory { get; set; }
    }

    /// <summary>One generated prompt in a project's history.</summary>
    public sealed class HistoryEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string ProjectName { get; set; } = string.Empty;
        public string UserPrompt { get; set; } = string.Empty;
        public bool PromptRightToLeft { get; set; }
        public string IncludePatterns { get; set; } = "*";
        public string ExcludePatterns { get; set; } = string.Empty;
        public string FilePatterns { get; set; } = string.Empty;
        public List<string> SelectedFiles { get; set; } = new();
        public List<string> UncheckedFiles { get; set; } = new();
        public string FileContents { get; set; } = string.Empty;

        /// <summary>True when both entries would produce the same prompt from the same inputs.</summary>
        public bool HasSameContentAs(HistoryEntry other) =>
            UserPrompt == other.UserPrompt
            && IncludePatterns == other.IncludePatterns
            && ExcludePatterns == other.ExcludePatterns
            && FilePatterns == other.FilePatterns
            && FileContents == other.FileContents
            && SelectedFiles.SequenceEqual(other.SelectedFiles, StringComparer.OrdinalIgnoreCase);
    }

    public enum TemplateKind
    {
        Prompt,
        Exclude
    }

    public sealed class TextTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public sealed class TemplateLibrary
    {
        public List<TextTemplate> PromptTemplates { get; set; } = new();
        public List<TextTemplate> ExcludeTemplates { get; set; } = new();

        public List<TextTemplate> Get(TemplateKind kind) =>
            kind == TemplateKind.Prompt ? PromptTemplates : ExcludeTemplates;

        public TextTemplate? Find(TemplateKind kind, string? name) =>
            string.IsNullOrEmpty(name)
                ? null
                : Get(kind).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
