using System.IO.Enumeration;

namespace FilePromptAssembler.Services
{
    /// <summary>
    /// A set of wildcard lines. A line without '/' is matched against a single file or folder name,
    /// a line containing '/' is matched against the path relative to the project folder.
    /// </summary>
    public sealed class WildcardSet
    {
        private readonly List<string> _namePatterns = new();
        private readonly List<string> _pathPatterns = new();

        public WildcardSet(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                string pattern = line.Trim().Replace('\\', '/');
                while (pattern.StartsWith("./", StringComparison.Ordinal))
                    pattern = pattern[2..];
                if (pattern.Length > 1)
                    pattern = pattern.Trim('/');
                if (pattern.Length == 0)
                    continue;

                if (pattern == ".")
                {
                    ContainsRootMarker = true;
                    continue;
                }

                if (pattern is "*" or "**")
                    MatchesEverything = true;

                if (pattern.Contains('/'))
                    _pathPatterns.Add(pattern);
                else
                    _namePatterns.Add(pattern);
            }
        }

        public bool IsEmpty => _namePatterns.Count == 0 && _pathPatterns.Count == 0 && !ContainsRootMarker;
        public bool MatchesEverything { get; }
        public bool ContainsRootMarker { get; }

        public bool Matches(string name, string relativePath)
        {
            foreach (string p in _namePatterns)
            {
                if (FileSystemName.MatchesSimpleExpression(p, name, ignoreCase: true))
                    return true;
            }
            foreach (string p in _pathPatterns)
            {
                if (FileSystemName.MatchesSimpleExpression(p, relativePath, ignoreCase: true))
                    return true;
            }
            return false;
        }
    }

    public sealed record ScanRequest(string Root, IReadOnlyList<string> Include, IReadOnlyList<string> Exclude, IReadOnlyList<string> FilePatterns);

    public sealed class ScanResult
    {
        public List<string> Files { get; } = new();
        public bool Truncated { get; set; }
        public int InaccessibleFolders { get; set; }
        public bool NoFilePatterns { get; set; }
    }

    /// <summary>
    /// Finds project files in three steps:
    /// 1. folders are filtered by the Include lines (a matching folder includes its sub-folders; "*" = all, "." = project root),
    /// 2. files and folders are filtered by the Exclude lines (an excluded folder is skipped entirely),
    /// 3. files are selected by the Files Wildcard lines.
    /// </summary>
    public static class FileScanner
    {
        public const int MaxFiles = 20000;

        public static ScanResult Scan(ScanRequest request, CancellationToken cancellationToken)
        {
            var result = new ScanResult();
            var include = new WildcardSet(request.Include);
            var exclude = new WildcardSet(request.Exclude);
            var files = new WildcardSet(request.FilePatterns);

            if (files.IsEmpty)
            {
                result.NoFilePatterns = true;
                return result;
            }

            bool includeAll = include.IsEmpty || include.MatchesEverything;
            bool rootIncluded = includeAll || include.ContainsRootMarker;

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = 0,
                ReturnSpecialDirectories = false
            };

            // (directory, relative path, files of this folder are included, sub-folders inherit inclusion)
            var stack = new Stack<(DirectoryInfo Dir, string Rel, bool Included, bool Cascade)>();
            stack.Push((new DirectoryInfo(request.Root), string.Empty, rootIncluded, includeAll));

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (dir, rel, included, cascade) = stack.Pop();

                try
                {
                    if (included)
                    {
                        foreach (var file in dir.EnumerateFiles("*", options))
                        {
                            string fileRel = rel.Length == 0 ? file.Name : rel + "/" + file.Name;
                            if (exclude.Matches(file.Name, fileRel) || !files.Matches(file.Name, fileRel))
                                continue;

                            result.Files.Add(fileRel);
                            if (result.Files.Count >= MaxFiles)
                            {
                                result.Truncated = true;
                                result.Files.Sort(RelativePathComparer.Instance);
                                return result;
                            }
                        }
                    }

                    foreach (var sub in dir.EnumerateDirectories("*", options))
                    {
                        if ((sub.Attributes & FileAttributes.ReparsePoint) != 0)
                            continue; // junctions/symlinks can create loops

                        string subRel = rel.Length == 0 ? sub.Name : rel + "/" + sub.Name;
                        if (exclude.Matches(sub.Name, subRel))
                            continue;

                        bool subIncluded = cascade || include.Matches(sub.Name, subRel);
                        stack.Push((sub, subRel, subIncluded, subIncluded));
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    result.InaccessibleFolders++;
                }
            }

            result.Files.Sort(RelativePathComparer.Instance);
            return result;
        }
    }

    /// <summary>Orders relative paths by their parent folders (segment by segment), then by file name.</summary>
    public sealed class RelativePathComparer : IComparer<string>
    {
        public static readonly RelativePathComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            string[] a = x.Split('/');
            string[] b = y.Split('/');
            int dirsA = a.Length - 1;
            int dirsB = b.Length - 1;

            for (int i = 0; i < Math.Min(dirsA, dirsB); i++)
            {
                int c = StringComparer.OrdinalIgnoreCase.Compare(a[i], b[i]);
                if (c != 0) return c;
            }

            if (dirsA != dirsB)
                return dirsA.CompareTo(dirsB);

            return StringComparer.OrdinalIgnoreCase.Compare(a[^1], b[^1]);
        }
    }
}
