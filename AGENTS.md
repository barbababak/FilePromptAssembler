# AGENTS.md — FilePromptAssembler solution

**Before doing anything, read the project's `AGENTS.md`, `docs/STATUS.md` and `docs/ARCHITECTURE.md`.**

The solution `FilePromptAssembler.slnx` contains one Windows Forms desktop app (.NET 10). The app combines a user prompt with selected source files of a project folder into one prompt for an AI assistant.

## Projects

| Project | Path | Purpose |
|---|---|---|
| FilePromptAssembler (startup project, WinExe) | `FilePromptAssembler/FilePromptAssembler.csproj` | The app itself, version 2.0.0 |

The previous version (`FilePromptAssemblerOld`) is not part of this solution and is not in this folder.

## Project documentation

All paths are under `FilePromptAssembler/`:

- `AGENTS.md`: rules, commands, things that must not change
- `README.md`
- `docs/ARCHITECTURE.md`
- `docs/DECISIONS.md`
- `docs/SETUP.md`
- `docs/STATUS.md`

## Build and run (from this folder)

```bash
dotnet build FilePromptAssembler.slnx
dotnet run --project FilePromptAssembler/FilePromptAssembler.csproj
```
