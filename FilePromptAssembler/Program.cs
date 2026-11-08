using System.Security.Cryptography;
using System.Text;
using FilePromptAssembler.Models;
using FilePromptAssembler.Services;
using FilePromptAssembler.UI;
using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler
{
    internal static class Program
    {
        public const string AppName = "File Prompt Assembler";

        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowFatal(e.ExceptionObject as Exception);

            var paths = new AppPaths(Path.Combine(AppContext.BaseDirectory, "Data"));
            try
            {
                paths.EnsureCreated();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"The data folder could not be created:\n{paths.Root}\n\n{ex.Message}",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string mutexName = @"Local\FilePromptAssembler_" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(paths.Root.ToUpperInvariant())))[..16];
            using var mutex = new Mutex(true, mutexName, out bool isFirstInstance);
            if (!isFirstInstance)
            {
                MessageBox.Show("File Prompt Assembler is already running.", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var settings = JsonStore.Load<AppSettings>(paths.SettingsFile) ?? new AppSettings();
            using (ThemeManager.Initialize(paths.ThemesDirectory, settings.ThemeName))
            {
                Application.Run(new MainForm(paths, settings));
            }

            GC.KeepAlive(mutex);
        }

        private static void ShowFatal(Exception? ex)
        {
            MessageBox.Show("An unexpected error occurred:\n\n" + (ex?.ToString() ?? "Unknown error"),
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
