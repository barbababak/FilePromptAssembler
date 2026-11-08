# Setup — FilePromptAssembler

## Tools and versions

| Item | Version / value | Source |
|---|---|---|
| .NET SDK | 10.0 (10.0.401 was used during development) | `TargetFramework` net10.0-windows; `dotnet --version` |
| Language | C# (the SDK's default language version for net10.0) with `Nullable` and `ImplicitUsings` enabled | csproj |
| UI | Windows Forms (`UseWindowsForms`), `ApplicationHighDpiMode` PerMonitorV2, `ApplicationDefaultFont` "Segoe UI, 9pt" | csproj |
| Other | `AllowUnsafeBlocks` = true (needed by `LibraryImport`) | csproj |
| Solution format | `.slnx` (`../FilePromptAssembler.slnx`) | |
| IDE | Visual Studio. The exact version is unknown; any version that supports .NET 10 and `.slnx` works | `.vs` folder present |
| OS | Windows 10/11. Dark title bars need a Windows build that supports DWM attribute 20 | |
| NuGet packages | None | csproj |

## Setup from scratch

1. Install the .NET 10 SDK, either on its own or with Visual Studio's ".NET desktop development" workload.
2. Open `FilePromptAssembler.slnx` in the solution root, or work from the command line:
   ```bash
   cd FilePromptAssembler
   dotnet build -c Debug
   dotnet run -c Debug
   ```
3. On first run the app creates `bin/<Config>/net10.0-windows/Data/` with these items:
   - `settings.json`
   - `templates.json`, seeded from `DefaultTemplates`
   - `Themes/README.txt`
   - the `Projects/` and `History/` folders

   `projects.json` is written when the first project is added.

## Configuration and data files

The app reads no environment variables, uses no secrets and needs no network access.

| File | Content |
|---|---|
| `Data/settings.json` | `AppSettings`: ThemeName, ProjectSort, ProjectSortDescending, SelectedProjectId, AutoCopy, BlankLinesAfterPrompt, Window, split ratios, last template choices |
| `Data/projects.json` | `List<ProjectInfo>` |
| `Data/templates.json` | `TemplateLibrary` (PromptTemplates, ExcludeTemplates). Delete it to restore the built-in templates on the next start |
| `Data/Projects/{id:N}.json` | `ProjectState` of one project |
| `Data/History/{id:N}.json` | `List<HistoryEntry>` of one project |
| `Data/Themes/*.json` | Custom `Theme` files with colors as `#RRGGBB`, loaded with View → Theme → Reload Themes. `README.txt` contains an example |
| `*.damaged-<timestamp>` | A backup of a JSON file that could not be parsed |

Notes:
- Enums are stored as strings, and the JSON may contain comments and trailing commas.
- Edit the files only while the app is closed, because the app overwrites them on save.
- Debug and Release builds use separate `Data` folders. The user's real data is in the Release folder.

## Publishing

There is no publish profile and no installer in the project. How the app is deployed is unknown. One working option is `dotnet publish -c Release -r win-x64`, which this documentation has not tested.
