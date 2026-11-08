using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FilePromptAssembler.UI.Theming
{
    internal static partial class NativeMethods
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

        private static readonly ConditionalWeakTable<Control, object> HookedControls = new();

        public static void SetDarkTitleBar(Form form, bool dark)
        {
            if (!form.IsHandleCreated)
                return;
            int value = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }

        /// <summary>Gives scroll bars (and combo drop-downs) of a native control the dark or light system look.</summary>
        public static void SetControlTheme(Control control)
        {
            if (!HookedControls.TryGetValue(control, out _))
            {
                HookedControls.Add(control, new object());
                // Some property changes (e.g. RightToLeft) recreate the handle; re-apply then.
                control.HandleCreated += (s, _) => ApplyControlTheme((Control)s!);
            }

            if (control.IsHandleCreated)
                ApplyControlTheme(control);
        }

        private static void ApplyControlTheme(Control control)
        {
            bool dark = ThemeManager.Instance.Current.IsDark;
            string? name = control is ComboBox
                ? (dark ? "DarkMode_CFD" : "CFD")
                : (dark ? "DarkMode_Explorer" : "Explorer");
            _ = SetWindowTheme(control.Handle, name, null);
        }
    }
}
