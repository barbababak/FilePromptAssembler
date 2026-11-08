using System.Collections.Concurrent;

namespace FilePromptAssembler.Services
{
    /// <summary>
    /// Finds files that contain a text (case-insensitive). File contents are cached and re-read only
    /// when a file's size or last write time changes.
    /// </summary>
    public sealed class FileContentSearcher
    {
        private sealed record CachedText(DateTime LastWriteUtc, long Length, string? Text);

        private readonly ConcurrentDictionary<string, CachedText> _cache = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Search(string root, IReadOnlyList<string> relativePaths, string phrase, CancellationToken cancellationToken)
        {
            var matches = new ConcurrentBag<string>();
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2)
            };

            Parallel.ForEach(relativePaths, options, rel =>
            {
                string? text = GetText(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
                if (text != null && text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    matches.Add(rel);
            });

            return new HashSet<string>(matches, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Frees the cached contents (e.g. when another project is opened).</summary>
        public void Clear() => _cache.Clear();

        private string? GetText(string fullPath)
        {
            try
            {
                var info = new FileInfo(fullPath);
                if (!info.Exists)
                {
                    _cache.TryRemove(fullPath, out _);
                    return null;
                }

                if (_cache.TryGetValue(fullPath, out var cached)
                    && cached.LastWriteUtc == info.LastWriteTimeUtc && cached.Length == info.Length)
                {
                    return cached.Text;
                }

                string? text = FileContentReader.TryReadText(info);
                _cache[fullPath] = new CachedText(info.LastWriteTimeUtc, info.Length, text);
                return text;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
