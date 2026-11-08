using System.Diagnostics;

namespace FilePromptAssembler.Services
{
    public static class TextUtil
    {
        public static string NormalizeNewLines(string text) => text.ReplaceLineEndings("\r\n");

        /// <summary>Non-empty, trimmed lines.</summary>
        public static List<string> Lines(string text) =>
            text.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

        /// <summary>True when the text contains Hebrew/Arabic-script letters (e.g. Persian).</summary>
        public static bool ContainsRtl(string text)
        {
            foreach (char c in text)
            {
                if ((c >= '֐' && c <= 'ࣿ') || (c >= 'יִ' && c <= 'ﻼ'))
                    return true;
            }
            return false;
        }

        public static string LastNonEmptyLine(string text, int maxLength)
        {
            string line = text.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? string.Empty;
            return line.Length > maxLength ? line[..maxLength] + "…" : line;
        }
    }

    public static class PathUtil
    {
        public static string Normalize(string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetPathRoot(full) ?? string.Empty;
            return full.Length > root.Length ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
        }

        public static bool AreEqual(string a, string b) =>
            string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

        /// <summary>The innermost folder name of a path (the path itself for a drive root).</summary>
        public static string DefaultProjectName(string folder)
        {
            string name = Path.GetFileName(Normalize(folder));
            return string.IsNullOrEmpty(name) ? folder : name;
        }
    }

    public static class ShellHelper
    {
        /// <summary>Opens a folder with the system's default folder handler (Explorer, Directory Opus, ...).</summary>
        public static void OpenFolder(string folder)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }
}
