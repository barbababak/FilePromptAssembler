namespace FilePromptAssembler.Services
{
    public sealed record ProjectDetection(string Description, string? PromptTemplate, string? ExcludeTemplate, string FilePatterns)
    {
        public static readonly ProjectDetection Unknown = new("Unknown project type", null, null, string.Empty);
    }

    /// <summary>Guesses the project type of a folder to suggest templates for a new project.</summary>
    public static class ProjectTypeDetector
    {
        public static ProjectDetection Detect(string folder)
        {
            try
            {
                var deep = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3, IgnoreInaccessible = true };
                var top = new EnumerationOptions { IgnoreInaccessible = true };

                var csprojFiles = Directory.EnumerateFiles(folder, "*.csproj", deep)
                    .Where(p => !IsInBuildFolder(folder, p))
                    .Take(20)
                    .ToList();

                bool hasSolution = Directory.EnumerateFiles(folder, "*.sln", top).Any()
                    || Directory.EnumerateFiles(folder, "*.slnx", top).Any();

                if (csprojFiles.Count > 0 || hasSolution)
                    return DetectCSharp(csprojFiles);

                if (File.Exists(Path.Combine(folder, "pyproject.toml"))
                    || File.Exists(Path.Combine(folder, "requirements.txt"))
                    || Directory.EnumerateFiles(folder, "*.py", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, IgnoreInaccessible = true }).Any())
                {
                    return new ProjectDetection("Python project", "Python", "Python", "*.py");
                }

                if (File.Exists(Path.Combine(folder, "package.json")))
                {
                    return new ProjectDetection("JavaScript / TypeScript project", "JavaScript / TypeScript", "JavaScript / Node.js",
                        "*.js\r\n*.jsx\r\n*.ts\r\n*.tsx\r\npackage.json");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            return ProjectDetection.Unknown;
        }

        private static ProjectDetection DetectCSharp(List<string> csprojFiles)
        {
            string text = string.Empty;
            foreach (string file in csprojFiles)
            {
                try
                {
                    if (new FileInfo(file).Length < 512 * 1024)
                        text += File.ReadAllText(file) + "\n";
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            bool Has(string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);

            const string patterns = "*.cs\r\n*.csproj";
            const string xamlPatterns = "*.cs\r\n*.csproj\r\n*.xaml";

            if (Has("cAlgo") || Has("cTrader"))
                return new ProjectDetection("C# cTrader project", "C# cTrader", "C#", patterns);
            if (Has("<UseMaui>true"))
                return new ProjectDetection("C# .NET MAUI project", "C# .NET MAUI", "C#", xamlPatterns);
            if (Has("Microsoft.NET.Sdk.Web") || Has("Microsoft.NET.Sdk.Razor"))
                return new ProjectDetection("C# ASP.NET Core project", "C# ASP.NET Core", "C#", "*.cs\r\n*.csproj\r\n*.cshtml\r\n*.razor\r\nappsettings.json");
            if (Has("<UseWPF>true"))
                return new ProjectDetection("C# WPF application", "C# WPF Application", "C#", xamlPatterns);
            if (Has("<UseWindowsForms>true") || Has("System.Windows.Forms"))
                return new ProjectDetection("C# Windows Forms application", "C# Windows Application", "C#", patterns);
            if (Has("<OutputType>Exe"))
                return new ProjectDetection("C# console application", "C# Console Application", "C#", patterns);

            return new ProjectDetection("C# project", null, "C#", patterns);
        }

        private static bool IsInBuildFolder(string root, string path)
        {
            string rel = Path.GetRelativePath(root, path);
            foreach (string part in rel.Split(Path.DirectorySeparatorChar))
            {
                if (part.Equals("bin", StringComparison.OrdinalIgnoreCase) || part.Equals("obj", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
