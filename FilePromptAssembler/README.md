# File Prompt Assembler

A Windows desktop tool that builds a single prompt for an online AI assistant from two parts:
- your request text;
- the contents of the source files you pick from a project folder.

## Purpose

Pasting many source files into a chat by hand is slow and easy to get wrong. With this tool you keep a list of projects. For each project you pick the relevant files with folder and wildcard filters and write your request. One click then produces a prompt you can paste (prompt + blank lines + every file wrapped in `<FILE><PATH><INDEX><CONTENT>` tags). Everything is saved automatically for each project.

## Features

- **Projects**
  - Add a project with *Add…* or Ctrl+O, or drop a folder onto the list. Remove it with Del.
  - Rename a project with F2. The default name is the innermost folder name.
  - Sort by Name, Date Opened or Last Modified, ascending or descending.
  - Right-click menu: Open Folder, Open Parent Folder (both use the system's default folder handler), Rename, Remove.
  - Selecting a project restores its full state. Each text box keeps its own undo history while you switch between projects.
- **New project dialog**
  - Name.
  - Detected project type: cTrader, .NET MAUI, ASP.NET Core, WPF, WinForms, Console, Python or JS/TS.
  - Prompt template, exclude template and file wildcards. Each can be empty or taken from a template.
- **Prompt box**
  - *Open Template…* lists the prompt templates sorted by name.
  - The *RTL* toggle sets right-to-left text for the Prompt and Final Prompt boxes.
- **Filters**
  - *Include Folders*: folder patterns. `*` means all folders, `.` means the project root.
  - *Exclude*: file and folder patterns. Exclude templates such as C#, JavaScript / Node.js and Python are available.
  - *Files Wildcard*: file name patterns, for example `*.cs`.
  - A pattern line that contains `/` is matched against the relative path.
- **Files list**
  - Shown sorted by folder, then by file name.
  - A checkbox for each file, plus a three-state *Select all*.
  - *Find text in files…* shows only the files that contain a phrase (case-insensitive), so you can check or uncheck just those.
- **Templates**
  - Templates are read-only until you click *Edit*. Click *Save* or press Ctrl+S to store changes.
  - You can create, rename and delete templates. *Use Template* inserts the text; Ctrl+Z undoes it.
- **Output**
  - *Generate Prompt* (F5 or Ctrl+Enter) fills *Final Prompt* and *File Contents*, each with a character count and an approximate token count.
  - *Copy to Clipboard* (Ctrl+Shift+C).
  - *Auto Copy* (on by default) copies automatically after Generate.
- **History**
  - Each Generate adds an entry (project name + date and time), unless it is identical to the latest entry or *Don't add to history* is checked.
  - Restore an entry with Enter or a double-click, or copy it.
  - Delete the selected entries, all entries except the latest, or all entries.
- **Undo:** every text box supports multi-level Ctrl+Z and Ctrl+Y, including changes made by the program (templates, history restore, Generate).
- **Themes:** System (the default, follows Windows), Light and Dark. You can add custom themes as JSON files in `Data\Themes`.

## Requirements

- Windows. The app uses WinForms, DWM and uxtheme APIs, and the registry to read the Windows theme.
- .NET 10 SDK (to build) or the .NET 10 Desktop Runtime (to run).

## Build and run

```bash
cd FilePromptAssembler
dotnet run -c Release
```

You can also open `../FilePromptAssembler.slnx` in Visual Studio and press F5. The data folder is `Data\` next to the executable. See `docs/SETUP.md` for details.
