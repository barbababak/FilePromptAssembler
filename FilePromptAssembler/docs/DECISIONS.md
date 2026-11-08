# Design decisions — FilePromptAssembler

These decisions were made during the version 2 rewrite (2026-11-06), based on the user's specification and their answers to clarifying questions.

## Decisions taken with the user

| # | Decision | Reason |
|---|---|---|
| U1 | A full rewrite as a new project. The old app was renamed `FilePromptAssemblerOld` (solution, project, folder and namespace) | The user wanted a consistent new codebase and wanted to keep the old app |
| U2 | Same output format as version 1 (`<FILE><PATH><INDEX><CONTENT>`, 15 blank lines after the prompt) | Prompts must work the same way as before |
| U3 | All UI text is English; the built-in prompt templates are Persian (the user's general rules, verbatim) | The user asked for both |
| U4 | Newly found files start **checked**, so only `UncheckedFiles` is stored | The user's answer. It also keeps the state file small |
| U5 | "Last Modified" is the last change made *in the app* to the project's state, not the file times on disk | The user's answer. Scanning disk times would be slow and is not what the user meant |
| U6 | Data is stored in `Data\` next to the exe (portable), not in `%AppData%` | The user's answer |
| U7 | Theme "System" is the default and follows Windows live; Light and Dark are built in; more themes can be added as JSON files | The user asked for this and for themes to be extensible |
| U8 | The RTL toggle applies to both the Prompt and Final Prompt boxes | A later request |
| U9 | Content search, "option 1": a search box filters the Files list to the files that contain a phrase (case-insensitive). The checkboxes stay editable; Select all affects only the matches; Generate still uses every checked file | The user chose this option from those proposed. The other options were not implemented |
| U10 | A new project starts either empty or from templates the user picks. Defaults are guessed by `ProjectTypeDetector`, then the last choice is used | Specification: "reset to empty or to templates the user picks" |
| U11 | History: an entry is skipped if it is identical to the latest one; Don't add to history; delete selected, delete all except the latest, delete all | Specification |
| U12 | Templates are read-only until Edit and are saved only on Save | Specification |

## Technical decisions

| # | Decision | Reason | Rejected options |
|---|---|---|---|
| T1 | The UI is built in code, without the WinForms Designer | Precise layout with Table/Split containers, equal sizes for matching controls on opposite sides, easier theming, no `.resx` files | The Designer, which fights dynamic layout and theming and creates large generated files |
| T2 | JSON with System.Text.Json, one file per project for state and for history | No dependencies; human-readable; a damaged file affects only one project; history files can grow large | SQLite or LiteDB (adds a dependency); one big file (slow and risky to save) |
| T3 | Atomic save (temp file + `File.Move`), and invalid JSON is backed up as `*.damaged-*` | Protects user data from crashes and bad edits | Overwriting the file in place |
| T4 | Auto-save with a debounce of 700 ms, plus a flush on project switch and on close | Saves everything without writing on every key press | An explicit Save button (rejected by the specification) |
| T5 | Own `UndoTextBox` with diff-based multi-level undo, also for changes made by the program, and a separate history per project | The native TextBox has only one undo level and loses it when the program sets `Text`. The specification requires Ctrl+Z for both user and program changes | `RichTextBox` undo (unreliable for programmatic changes; heavier) |
| T6 | `FileSystemName.MatchesSimpleExpression` for wildcards; a pattern with `/` matches the relative path | Built in, fast, case-insensitive | Regex or globbing libraries |
| T7 | Iterative scan that skips reparse points and stops at 20,000 files | No stack overflow, no symlink loops, and the UI stays responsive on huge folders | Recursive `Directory.EnumerateFiles(AllDirectories)`, which cannot prune excluded folders |
| T8 | Scan and search run in the background with debounce and cancellation; a result is ignored if the project changed | Responsive UI; no stale results | Synchronous scanning |
| T9 | Content search: an in-memory cache keyed by path, write time and size, with a parallel `Contains(OrdinalIgnoreCase)` | Typing more letters does not read the files again | An index or a watcher service (more complex than needed) |
| T10 | `ThemeManager` singleton + `IThemeable` + `ThemedForm`; native dark scroll bars and title bar | One place to change colors; new themes need no code | Per-form color code |
| T11 | A single-instance mutex that includes a hash of the data folder | Two instances on the same data would overwrite each other's saves; Debug and Release can still run side by side | No guard; a global mutex |
| T12 | Binary files (a NUL byte in the first 8000 bytes, unless there is a UTF-16 BOM) and files over 10 MB are skipped with a warning | They would break or flood the prompt | Reading every file |
| T13 | `ShellExecute` on the folder path to open folders | Respects the user's default folder handler, which the specification asked for | `explorer.exe <path>` |
| T14 | `LibraryImport` source-generated P/Invoke (needs `AllowUnsafeBlocks`) | The modern, AOT-friendly replacement for `DllImport` | `DllImport` |

## Constraints and assumptions

- Runs on Windows only (WinForms, DWM, uxtheme, registry).
- The normal window size is half of a 1920×1080 screen. The minimum window size is 780×640.
- Token counts are only an estimate (characters / 4).
- `BlankLinesAfterPrompt` can be changed only in `settings.json`; there is no UI for it.
- The search text is not saved with the project. It is cleared when the project changes.
- Prompt templates and the template names used by `ProjectTypeDetector` are linked by name. If a user renames a template, detection falls back to the last choice.
