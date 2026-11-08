using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FilePromptAssembler.UI.Theming
{
    /// <summary>
    /// A color theme. Built-in themes are defined in <see cref="BuiltInThemes"/>; more themes can be added
    /// as JSON files in the Data\Themes folder (same property names, colors as "#RRGGBB").
    /// </summary>
    public sealed class Theme
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDark { get; set; }

        /// <summary>Window background (behind the sections).</summary>
        public Color Background { get; set; }
        /// <summary>Section (card) background.</summary>
        public Color Surface { get; set; }
        /// <summary>Text boxes and lists.</summary>
        public Color Input { get; set; }
        public Color Border { get; set; }
        public Color Text { get; set; }
        public Color SubtleText { get; set; }
        public Color Accent { get; set; }
        public Color AccentHover { get; set; }
        public Color AccentText { get; set; }
        public Color Button { get; set; }
        public Color ButtonHover { get; set; }
        public Color Selection { get; set; }
        public Color SelectionText { get; set; }
    }

    public static class BuiltInThemes
    {
        public static readonly Theme Light = new()
        {
            Name = "Light",
            IsDark = false,
            Background = ColorTranslator.FromHtml("#EEF1F5"),
            Surface = ColorTranslator.FromHtml("#FFFFFF"),
            Input = ColorTranslator.FromHtml("#FFFFFF"),
            Border = ColorTranslator.FromHtml("#CDD3DC"),
            Text = ColorTranslator.FromHtml("#1F2733"),
            SubtleText = ColorTranslator.FromHtml("#667085"),
            Accent = ColorTranslator.FromHtml("#1F5FAD"),
            AccentHover = ColorTranslator.FromHtml("#184C8C"),
            AccentText = ColorTranslator.FromHtml("#FFFFFF"),
            Button = ColorTranslator.FromHtml("#F6F7F9"),
            ButtonHover = ColorTranslator.FromHtml("#E4E8EE"),
            Selection = ColorTranslator.FromHtml("#D6E4F5"),
            SelectionText = ColorTranslator.FromHtml("#0F2B4D")
        };

        public static readonly Theme Dark = new()
        {
            Name = "Dark",
            IsDark = true,
            Background = ColorTranslator.FromHtml("#1B1D21"),
            Surface = ColorTranslator.FromHtml("#26292E"),
            Input = ColorTranslator.FromHtml("#1E2024"),
            Border = ColorTranslator.FromHtml("#3B3F46"),
            Text = ColorTranslator.FromHtml("#E3E5E8"),
            SubtleText = ColorTranslator.FromHtml("#9AA1AB"),
            Accent = ColorTranslator.FromHtml("#3A7BD5"),
            AccentHover = ColorTranslator.FromHtml("#2F67B5"),
            AccentText = ColorTranslator.FromHtml("#FFFFFF"),
            Button = ColorTranslator.FromHtml("#31353B"),
            ButtonHover = ColorTranslator.FromHtml("#3C4148"),
            Selection = ColorTranslator.FromHtml("#23405F"),
            SelectionText = ColorTranslator.FromHtml("#E8F1FC")
        };
    }

    /// <summary>Serializes <see cref="Color"/> as "#RRGGBB" (or "#AARRGGBB" when not opaque).</summary>
    public sealed class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return Color.Empty;

            value = value.Trim();
            if (value.StartsWith('#') && value.Length == 9
                && uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            {
                return Color.FromArgb(unchecked((int)argb));
            }

            try
            {
                return ColorTranslator.FromHtml(value);
            }
            catch (Exception ex)
            {
                throw new JsonException($"Invalid color value '{value}'.", ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.A == 255
                ? $"#{value.R:X2}{value.G:X2}{value.B:X2}"
                : $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
        }
    }
}
