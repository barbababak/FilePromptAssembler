namespace FilePromptAssembler.UI.Theming
{
    /// <summary>Base class of all windows: applies the current theme and follows theme changes.</summary>
    public class ThemedForm : Form
    {
        public ThemedForm()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ThemeManager.Instance.ThemeChanged += OnThemeChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTheme();
        }

        protected virtual void ApplyTheme()
        {
            SuspendLayout();
            ThemeManager.Instance.Apply(this);
            ResumeLayout(true);
            Invalidate(true);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            if (InvokeRequired)
                BeginInvoke(ApplyTheme);
            else
                ApplyTheme();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
