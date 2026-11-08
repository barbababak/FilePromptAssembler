# AGENTS.md — FilePromptAssembler

Start here. Before changing anything, read `docs/STATUS.md` and `docs/ARCHITECTURE.md`.

## Summary

File Prompt Assembler (v2.0.0) is a Windows Forms desktop app on .NET 10. It combines a user prompt with the contents of selected source files from a project folder into one "Final Prompt" that can be pasted into an online AI assistant.

Main features:
- Project list. Each project's working state is saved automatically.
- Folder, exclude and wildcard filters.
- A checked file list with content search.
- Prompt and exclude templates.
- A history of generated prompts.
- Light, Dark and System themes, plus custom JSON themes.

Version 2 is a full rewrite of an earlier app. That older app was renamed `FilePromptAssemblerOld` and is no longer inside this solution folder.

## Build / run / test

The commands below are run from this folder (`FilePromptAssembler/`):

```bash
dotnet build -c Debug
dotnet run -c Debug
dotnet build -c Release
```

- **Output:** `bin/<Configuration>/net10.0-windows/FilePromptAssembler.exe`.
- **Data folder:** the app keeps its data in `Data\` next to the exe. Debug and Release therefore have separate data. `bin/Release/net10.0-windows/Data` holds the user's real data, so do not delete it.
- **Tests:** there are no automated tests and no test project. Verify changes by running the app.
- **Locked build:** if the build fails because the exe is locked, the app is running. Only one instance can run per data folder. Ask the user to close it.

## Coding rules (follow the existing code)

- **Language and layout:** C# with `Nullable` and `ImplicitUsings` enabled. Use file-scoped *block* namespaces (`namespace X { ... }`), braces on new lines and 4-space indentation.
- **Naming:**
  - Private fields: `_camelCase`.
  - Constants: `PascalCase`.
  - Types and members: `PascalCase`.
  - Namespaces follow the folders: `FilePromptAssembler.Models`, `.Services`, `.UI`, `.UI.Controls`, `.UI.Dialogs`, `.UI.Theming`.
- **No Designer:** the UI is built in code. Do not add `.Designer.cs` or `.resx` files. Layout uses `TableLayoutPanel`, `SplitContainer`, `SectionPanel` and `BorderedHost`. Create controls through the `Ui` factory helpers (`UI/Controls/Ui.cs`) so they share the same style.
- **UI language:** all UI text is English. The built-in prompt templates are Persian on purpose; do not translate them.
- **Text boxes:** every editable text box must be an `UndoTextBox`. To set text without an undo step, use `SetTextWithoutUndo`. Assigning `Text` directly records an undo step, so the user can press Ctrl+Z.
- **Theming:**
  - New windows derive from `ThemedForm`.
  - Custom-painted controls implement `IThemeable`.
  - Never hard-code colors; take them from the `Theme`.
  - Primary buttons are marked with `Tag = ThemeManager.PrimaryTag`; subtle labels use `ThemeManager.SubtleTag`.
- **Persistence:** go only through `JsonStore` and the repositories. Never write JSON files directly.
- **Long work:** scanning, reading and searching run in `Task.Run` with a `CancellationTokenSource`. After an `await`, check that the project is still the current one: `ReferenceEquals(project, _current)`.
- **Dependencies:** no NuGet packages are used. Do not add any unless they are really necessary.
- **Win32 interop:** use `LibraryImport`, not `DllImport`. This is why `AllowUnsafeBlocks` is enabled.

## Do not change without an explicit request

- **Output format:** the `<FILE><PATH><INDEX><CONTENT>` format in `PromptBuilder` and the 15 blank lines between the prompt and the file contents (`AppSettings.BlankLinesAfterPrompt`). They must stay compatible with version 1.
- **Data files:** the JSON property names and file layout in `Data\` (`settings.json`, `projects.json`, `templates.json`, `Projects\{id:N}.json`, `History\{id:N}.json`, `Themes\*.json`). Renaming a property silently loses user data.
- **Template text:** the text of `DefaultTemplates.GeneralRules` and the Persian templates. The user provided it word for word.
- **Defaults:**
  - Include = `*`.
  - The exclude template "C#" = `obj`, `bin`, `.vs`.
  - Auto Copy = true.
  - Theme = System.
  - New files found by a scan start checked, because only unchecked files are stored.
- **Filter order:** Include (folders) → Exclude (files and folders) → Files Wildcard.
- **File sort order:** by parent folders first, then by file name.
- **Single instance:** the per-data-folder mutex in `Program.cs`.

## Documentation files

| File | Content |
|---|---|
| `README.md` | Introduction, features, requirements, how to run |
| `docs/ARCHITECTURE.md` | Folders, files, classes and methods with their purpose; data flow (Mermaid) |
| `docs/DECISIONS.md` | Design decisions, rejected options, constraints and assumptions |
| `docs/SETUP.md` | Tool versions, setup from scratch, configuration and data files |
| `docs/STATUS.md` | What is done, known issues, prioritized to-do list, next step |
| `docs/USER-GUIDE-fa.md` | Short Persian guide for end users (keep it in sync when UI text or shortcuts change) |
