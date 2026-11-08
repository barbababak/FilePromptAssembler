# Status — FilePromptAssembler

Last updated: 2026-11-06. Version 2.0.0. The most recent build (2026-11-06) succeeded.

## Completed

Every feature below was tested manually in the running app. Screenshots were checked in both the Dark and Light themes.

- **Rewrite:** v2 rewrite on .NET 10. The old app is preserved as `FilePromptAssemblerOld`, outside this solution folder.
- **Projects:** add (dialog or drag & drop), remove, rename, sort (Name / Date Opened / Last Modified, ascending or descending), Open Folder / Open Parent Folder, restore state per project, undo history per project.
- **New project dialog:** project type detection; template selection; remembers the last choices.
- **Prompt:** prompt templates (read-only until Edit; New / Rename / Delete; Ctrl+S); RTL toggle for Prompt and Final Prompt.
- **Filters:** Include (default `*`), Exclude with templates (C# = obj, bin, .vs), Files Wildcard; background scan with debounce.
- **Files list:** sorted by folder, then name; checkboxes; three-state Select all; counter.
- **Content search:** a search box above the Files list, case-insensitive, with a cache. Enter searches immediately; Esc or ✕ clears. Tested with "class mainform": the two expected files were found, and Generate still included all 25 checked files.
- **Output:** Generate, which produces the same format as v1, with a warning for skipped files; Copy; Auto Copy (default on); character and ~token counts.
- **History:** skips duplicates of the latest entry; Don't add to history; Restore, Copy, Delete selected, Delete all except latest, Delete all.
- **Themes:** System / Light / Dark, custom JSON themes, dark title bar and scroll bars.
- **Persistence and safety:** auto-save, atomic JSON writes, backups of damaged files, single-instance guard, window and splitter layout saved.

## Partially done

None known.

## Known issues and limitations

These were found by reading the code; none has been reported as a bug by the user.

1. **Doubled typed text, seen once.** During automated UI testing, typed text appeared doubled once in the search box (`classclass mainform`). It could not be reproduced, and the likely cause is keys sent before the app had finished starting. Unconfirmed.
2. **Only the last theme error is visible.** `MainForm.OnShown` calls `SetStatus` once for each theme load error, so each message replaces the previous one.
3. **The search cache has no size limit.** `FileContentSearcher` caches every searched text of the current project. It is cleared only when the project changes.
4. **History has no size limit.** Each `HistoryEntry` stores the full `FileContents`, so history files can grow large.
5. **RTL is ignored when detecting duplicates.** `HistoryEntry.HasSameContentAs` does not compare `PromptRightToLeft`.
6. **No automated tests.**
7. **No UI for `BlankLinesAfterPrompt`.** It can only be changed in `settings.json`.

## Remaining work (by priority)

No feature request is open. The items below are suggestions that follow from the known issues; nobody has requested them.

1. The user's real-world check of v2 against their daily workflow. Not yet confirmed.
2. Show all theme load errors, for example by joining them into one status message (issue 2).
3. Consider a limit for the history size and the search cache (issues 3–4).
4. Add a test project for the services that have no UI: `FileScanner`, `WildcardSet`, `RelativePathComparer`, `PromptBuilder`, `UndoTextBox` diff/merge logic, `JsonStore`.

## Suggested next step

Ask the user whether v2 works for their daily use and whether anything should be changed, before starting any item above.
