using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.Services
{
    /// <summary>Reads and writes JSON files. Writes are atomic (temp file + move).</summary>
    public static class JsonStore
    {
        public static readonly JsonSerializerOptions Options = CreateOptions();

        /// <summary>Raised when reading or writing a file fails. The argument is a user-readable message.</summary>
        public static event Action<string>? Error;

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new ColorJsonConverter());
            return options;
        }

        /// <summary>Returns null when the file does not exist or cannot be read. A damaged file is kept as a backup.</summary>
        public static T? Load<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, Options);
            }
            catch (JsonException ex)
            {
                string backup = path + $".damaged-{DateTime.Now:yyyyMMdd-HHmmss}";
                try { File.Copy(path, backup, true); } catch (IOException) { }
                Error?.Invoke($"'{Path.GetFileName(path)}' is damaged and was reset (backup: {Path.GetFileName(backup)}). {ex.Message}");
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Error?.Invoke($"Could not read '{Path.GetFileName(path)}': {ex.Message}");
                return null;
            }
        }

        public static bool Save<T>(string path, T value)
        {
            string tempPath = path + ".tmp";
            try
            {
                string json = JsonSerializer.Serialize(value, Options);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Error?.Invoke($"Could not save '{Path.GetFileName(path)}': {ex.Message}");
                return false;
            }
        }

        public static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Error?.Invoke($"Could not delete '{Path.GetFileName(path)}': {ex.Message}");
            }
        }
    }
}
