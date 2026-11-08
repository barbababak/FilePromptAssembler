using FilePromptAssembler.Models;

namespace FilePromptAssembler.Services
{
    /// <summary>Project list and the per-project state files.</summary>
    public sealed class ProjectRepository
    {
        private readonly AppPaths _paths;

        public ProjectRepository(AppPaths paths)
        {
            _paths = paths;
        }

        public List<ProjectInfo> LoadProjects() => JsonStore.Load<List<ProjectInfo>>(_paths.ProjectsFile) ?? new List<ProjectInfo>();

        public void SaveProjects(IEnumerable<ProjectInfo> projects) => JsonStore.Save(_paths.ProjectsFile, projects.ToList());

        public ProjectState? LoadState(Guid projectId) => JsonStore.Load<ProjectState>(StatePath(projectId));

        public void SaveState(Guid projectId, ProjectState state) => JsonStore.Save(StatePath(projectId), state);

        public void DeleteState(Guid projectId) => JsonStore.Delete(StatePath(projectId));

        private string StatePath(Guid projectId) => Path.Combine(_paths.ProjectStatesDirectory, projectId.ToString("N") + ".json");
    }

    /// <summary>Prompt history, one file per project. Entries are kept newest first.</summary>
    public sealed class HistoryRepository
    {
        private readonly AppPaths _paths;

        public HistoryRepository(AppPaths paths)
        {
            _paths = paths;
        }

        public List<HistoryEntry> Load(Guid projectId) =>
            (JsonStore.Load<List<HistoryEntry>>(HistoryPath(projectId)) ?? new List<HistoryEntry>())
                .OrderByDescending(h => h.CreatedAt)
                .ToList();

        public void Save(Guid projectId, List<HistoryEntry> entries)
        {
            if (entries.Count == 0)
                JsonStore.Delete(HistoryPath(projectId));
            else
                JsonStore.Save(HistoryPath(projectId), entries);
        }

        public void Delete(Guid projectId) => JsonStore.Delete(HistoryPath(projectId));

        private string HistoryPath(Guid projectId) => Path.Combine(_paths.HistoryDirectory, projectId.ToString("N") + ".json");
    }

    /// <summary>Prompt and exclude templates. Seeded with the built-in templates on first run.</summary>
    public sealed class TemplateRepository
    {
        private readonly string _file;

        public TemplateRepository(string file)
        {
            _file = file;
        }

        public TemplateLibrary Library { get; private set; } = new();

        public void Load()
        {
            var library = JsonStore.Load<TemplateLibrary>(_file);
            bool created = library == null;
            library ??= DefaultTemplates.Create();

            foreach (var template in library.PromptTemplates.Concat(library.ExcludeTemplates))
                template.Text = TextUtil.NormalizeNewLines(template.Text ?? string.Empty);

            Library = library;
            if (created)
                Save();
        }

        public void Save() => JsonStore.Save(_file, Library);

        public List<TextTemplate> Sorted(TemplateKind kind) =>
            Library.Get(kind).OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
