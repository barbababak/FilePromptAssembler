using System.Text;

namespace FilePromptAssembler.Services
{
    public sealed record FileText(string RelativePath, string Content);

    public sealed class FileReadResult
    {
        public List<FileText> Files { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    public static class FileContentReader
    {
        public const long MaxFileSize = 10 * 1024 * 1024;

        /// <summary>Reads a text file with the same rules as <see cref="ReadFiles"/>; null for missing, too large or binary files.</summary>
        public static string? TryReadText(FileInfo info)
        {
            if (!info.Exists || info.Length > MaxFileSize)
                return null;
            byte[] bytes = File.ReadAllBytes(info.FullName);
            if (LooksBinary(bytes))
                return null;
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        public static FileReadResult ReadFiles(string root, IReadOnlyList<string> relativePaths)
        {
            var result = new FileReadResult();
            foreach (string rel in relativePaths)
            {
                string fullPath = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    var info = new FileInfo(fullPath);
                    if (!info.Exists)
                    {
                        result.Warnings.Add($"File not found: {rel}");
                        continue;
                    }
                    if (info.Length > MaxFileSize)
                    {
                        result.Warnings.Add($"Skipped (larger than 10 MB): {rel}");
                        continue;
                    }

                    byte[] bytes = File.ReadAllBytes(fullPath);
                    if (LooksBinary(bytes))
                    {
                        result.Warnings.Add($"Skipped (binary file): {rel}");
                        continue;
                    }

                    using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    result.Files.Add(new FileText(rel, TextUtil.NormalizeNewLines(reader.ReadToEnd())));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    result.Warnings.Add($"Could not read {rel}: {ex.Message}");
                }
            }
            return result;
        }

        private static bool LooksBinary(byte[] bytes)
        {
            // UTF-16 / UTF-32 text legitimately contains zero bytes; it always starts with a BOM here.
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
                return false;

            int length = Math.Min(bytes.Length, 8000);
            for (int i = 0; i < length; i++)
            {
                if (bytes[i] == 0)
                    return true;
            }
            return false;
        }
    }

    /// <summary>Builds the "File Contents" and "Final Prompt" texts (same format as version 1).</summary>
    public static class PromptBuilder
    {
        private const string NewLine = "\r\n";

        public static string BuildFileContents(IReadOnlyList<FileText> files)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                sb.Append("<FILE>").Append(NewLine);
                sb.Append("<PATH>").Append(file.RelativePath).Append("</PATH>").Append(NewLine);
                sb.Append("<INDEX>").Append(i + 1).Append('/').Append(files.Count).Append("</INDEX>").Append(NewLine);
                sb.Append("<CONTENT>").Append(NewLine);
                sb.Append(file.Content);
                if (!file.Content.EndsWith('\n'))
                    sb.Append(NewLine);
                sb.Append("</CONTENT>").Append(NewLine);
                sb.Append("</FILE>").Append(NewLine);

                if (i < files.Count - 1)
                    sb.Append(NewLine);
            }
            return sb.ToString();
        }

        public static string Compose(string userPrompt, string fileContents, int blankLines)
        {
            if (fileContents.Length == 0)
                return userPrompt;
            if (string.IsNullOrWhiteSpace(userPrompt))
                return fileContents;

            var sb = new StringBuilder(userPrompt.Length + fileContents.Length + blankLines * 2);
            sb.Append(userPrompt);
            for (int i = 0; i < Math.Max(1, blankLines); i++)
                sb.Append(NewLine);
            sb.Append(fileContents);
            return sb.ToString();
        }
    }
}
