using FilePromptAssembler.UI.Theming;

namespace FilePromptAssembler.UI.Controls
{
    /// <summary>The undo/redo history of one <see cref="UndoTextBox"/>. Can be detached and re-attached (e.g. per project).</summary>
    public sealed class UndoHistory
    {
        internal readonly List<TextEdit> UndoStack = new();
        internal readonly List<TextEdit> RedoStack = new();
        internal long Size;
        internal string TextAtDetach = string.Empty;
    }

    internal sealed class TextEdit
    {
        public int Position;
        public string Removed = string.Empty;
        public string Inserted = string.Empty;
        public DateTime Time;
        public bool Sealed;
        public long Size => Removed.Length + Inserted.Length;
    }

    /// <summary>
    /// A text box with a multi-level undo/redo history (Ctrl+Z / Ctrl+Y). Every change is recorded,
    /// whether typed by the user or set by the program through <see cref="Control.Text"/>.
    /// Only diffs are stored, and consecutive typing is grouped into words.
    /// </summary>
    public class UndoTextBox : TextBox, IThemeable
    {
        private const int MaxEdits = 500;
        private const long MaxHistoryChars = 16_000_000;
        private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(1500);

        private UndoHistory _history = new();
        private string _lastText = string.Empty;
        private bool _applying;
        private int _suppressDepth;
        private readonly ToolStripMenuItem _undoItem, _redoItem, _cutItem, _copyItem, _pasteItem, _deleteItem, _selectAllItem;

        public UndoTextBox() : this(true)
        {
        }

        public UndoTextBox(bool multiline)
        {
            BorderStyle = BorderStyle.None;
            HideSelection = false;
            MaxLength = int.MaxValue;
            if (multiline)
            {
                Multiline = true;
                AcceptsReturn = true;
                ScrollBars = ScrollBars.Both;
                WordWrap = false;
            }

            var menu = new ContextMenuStrip();
            _undoItem = AddItem(menu, "Undo", "Ctrl+Z", (_, _) => UndoEdit());
            _redoItem = AddItem(menu, "Redo", "Ctrl+Y", (_, _) => RedoEdit());
            menu.Items.Add(new ToolStripSeparator());
            _cutItem = AddItem(menu, "Cut", "Ctrl+X", (_, _) => Cut());
            _copyItem = AddItem(menu, "Copy", "Ctrl+C", (_, _) => Copy());
            _pasteItem = AddItem(menu, "Paste", "Ctrl+V", (_, _) => Paste());
            _deleteItem = AddItem(menu, "Delete", "Del", (_, _) => SelectedText = string.Empty);
            menu.Items.Add(new ToolStripSeparator());
            _selectAllItem = AddItem(menu, "Select All", "Ctrl+A", (_, _) => SelectAll());
            menu.Opening += (_, _) => UpdateMenu();
            ContextMenuStrip = menu;
        }

        public bool HasUndo => _history.UndoStack.Count > 0;
        public bool HasRedo => _history.RedoStack.Count > 0;

        /// <summary>Replaces the text without recording an undo step and starts a fresh history.</summary>
        public void SetTextWithoutUndo(string text)
        {
            _suppressDepth++;
            try
            {
                Text = text;
            }
            finally
            {
                _suppressDepth--;
            }
            _lastText = Text;
            _history = new UndoHistory();
        }

        public void ResetUndoHistory() => _history = new UndoHistory();

        /// <summary>Takes the current history out of the text box (a fresh one is started).</summary>
        public UndoHistory DetachHistory()
        {
            var history = _history;
            if (history.UndoStack.Count > 0)
                history.UndoStack[^1].Sealed = true;
            history.TextAtDetach = Text;
            _history = new UndoHistory();
            return history;
        }

        /// <summary>Re-attaches a history, but only if it belongs to the current text.</summary>
        public void AttachHistory(UndoHistory history)
        {
            if (string.Equals(history.TextAtDetach, Text, StringComparison.Ordinal))
                _history = history;
        }

        public void UndoEdit()
        {
            if (ReadOnly || _history.UndoStack.Count == 0)
                return;

            var edit = _history.UndoStack[^1];
            _history.UndoStack.RemoveAt(_history.UndoStack.Count - 1);
            edit.Sealed = true;
            _history.RedoStack.Add(edit);

            Replace(edit.Position, edit.Inserted.Length, edit.Removed);
            Select(edit.Position, edit.Removed.Length);
            ScrollToCaret();
        }

        public void RedoEdit()
        {
            if (ReadOnly || _history.RedoStack.Count == 0)
                return;

            var edit = _history.RedoStack[^1];
            _history.RedoStack.RemoveAt(_history.RedoStack.Count - 1);
            _history.UndoStack.Add(edit);

            Replace(edit.Position, edit.Removed.Length, edit.Inserted);
            Select(edit.Position + edit.Inserted.Length, 0);
            ScrollToCaret();
        }

        public virtual void ApplyTheme(Theme theme)
        {
            BackColor = theme.Input;
            ForeColor = theme.Text;
            NativeMethods.SetControlTheme(this);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            string current = Text;
            if (!_applying && _suppressDepth == 0)
                Record(_lastText, current);
            _lastText = current;
            base.OnTextChanged(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.Z:
                    UndoEdit();
                    return true;
                case Keys.Control | Keys.Y:
                case Keys.Control | Keys.Shift | Keys.Z:
                    RedoEdit();
                    return true;
                case Keys.Control | Keys.A:
                    SelectAll();
                    return true;
                case Keys.Control | Keys.Back:
                    if (!ReadOnly)
                        DeleteWordLeft();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                    SealLastEdit();
                    break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            SealLastEdit();
            base.OnMouseDown(e);
        }

        private void SealLastEdit()
        {
            if (_history.UndoStack.Count > 0)
                _history.UndoStack[^1].Sealed = true;
        }

        private void Replace(int position, int length, string text)
        {
            _applying = true;
            try
            {
                if (position == 0 && length == TextLength)
                {
                    Text = text;
                }
                else
                {
                    Select(position, length);
                    SelectedText = text;
                }
            }
            finally
            {
                _applying = false;
            }
            _lastText = Text;
        }

        private void Record(string oldText, string newText)
        {
            int max = Math.Min(oldText.Length, newText.Length);
            int prefix = 0;
            while (prefix < max && oldText[prefix] == newText[prefix])
                prefix++;

            int suffix = 0;
            while (suffix < max - prefix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
                suffix++;

            string removed = oldText.Substring(prefix, oldText.Length - prefix - suffix);
            string inserted = newText.Substring(prefix, newText.Length - prefix - suffix);
            if (removed.Length == 0 && inserted.Length == 0)
                return;

            foreach (var redo in _history.RedoStack)
                _history.Size -= redo.Size;
            _history.RedoStack.Clear();

            var now = DateTime.UtcNow;
            var last = _history.UndoStack.Count > 0 ? _history.UndoStack[^1] : null;
            if (last != null && !last.Sealed && now - last.Time < MergeWindow)
            {
                long before = last.Size;
                if (TryMerge(last, prefix, removed, inserted))
                {
                    last.Time = now;
                    _history.Size += last.Size - before;
                    return;
                }
            }

            var edit = new TextEdit { Position = prefix, Removed = removed, Inserted = inserted, Time = now };
            _history.UndoStack.Add(edit);
            _history.Size += edit.Size;

            while (_history.UndoStack.Count > 1 && (_history.UndoStack.Count > MaxEdits || _history.Size > MaxHistoryChars))
            {
                _history.Size -= _history.UndoStack[0].Size;
                _history.UndoStack.RemoveAt(0);
            }
        }

        private static bool TryMerge(TextEdit last, int position, string removed, string inserted)
        {
            // Typing: a single character right after the previous insertion. A new word starts a new step.
            if (removed.Length == 0 && last.Removed.Length == 0 && inserted.Length == 1 && last.Inserted.Length > 0
                && inserted[0] != '\r' && inserted[0] != '\n'
                && position == last.Position + last.Inserted.Length)
            {
                bool startsNewWord = !char.IsWhiteSpace(inserted[0]) && char.IsWhiteSpace(last.Inserted[^1]);
                if (startsNewWord)
                    return false;
                last.Inserted += inserted;
                return true;
            }

            if (inserted.Length == 0 && last.Inserted.Length == 0 && removed.Length == 1 && removed[0] != '\n' && removed[0] != '\r')
            {
                // Backspace
                if (position + 1 == last.Position)
                {
                    last.Removed = removed + last.Removed;
                    last.Position = position;
                    return true;
                }
                // Delete
                if (position == last.Position)
                {
                    last.Removed += removed;
                    return true;
                }
            }
            return false;
        }

        private void DeleteWordLeft()
        {
            if (SelectionLength > 0)
            {
                SelectedText = string.Empty;
                return;
            }

            int end = SelectionStart;
            if (end == 0)
                return;

            string text = Text;
            int start = end;
            while (start > 0 && char.IsWhiteSpace(text[start - 1]) && text[start - 1] != '\n')
                start--;
            while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
                start--;
            if (start == end)
            {
                start = end - 1;
                if (start > 0 && text[start] == '\n' && text[start - 1] == '\r')
                    start--;
            }

            Select(start, end - start);
            SelectedText = string.Empty;
        }

        private void UpdateMenu()
        {
            bool hasSelection = SelectionLength > 0;
            _undoItem.Enabled = !ReadOnly && HasUndo;
            _redoItem.Enabled = !ReadOnly && HasRedo;
            _cutItem.Enabled = !ReadOnly && hasSelection;
            _copyItem.Enabled = hasSelection;
            _pasteItem.Enabled = !ReadOnly && Clipboard.ContainsText();
            _deleteItem.Enabled = !ReadOnly && hasSelection;
            _selectAllItem.Enabled = TextLength > 0;
        }

        private static ToolStripMenuItem AddItem(ContextMenuStrip menu, string text, string shortcut, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, onClick) { ShortcutKeyDisplayString = shortcut };
            menu.Items.Add(item);
            return item;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ContextMenuStrip?.Dispose();
            base.Dispose(disposing);
        }
    }
}
