# File Prompt Assembler

A Windows desktop app (.NET 10, WinForms) that combines your request with the contents of selected source files from a project folder into one prompt, ready to paste into an AI assistant such as ChatGPT, Claude or Gemini.

## Features

- Keep a list of projects; each one is saved automatically and restored when you select it.
- Pick files with folder, exclude and wildcard filters, then check or uncheck them.
- Search file contents to find the files that contain a phrase.
- Prompt and exclude templates; multi-level undo in every text box.
- History of generated prompts; Light, Dark and System themes.

## Quick start

Requires Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/barbababak/FilePromptAssembler.git
cd FilePromptAssembler
dotnet run --project FilePromptAssembler/FilePromptAssembler.csproj -c Release
```

Or open `FilePromptAssembler.slnx` in Visual Studio and press F5.

## Documentation

- [Full feature list and usage](FilePromptAssembler/README.md)
- [راهنمای فارسی کاربر (Persian user guide)](FilePromptAssembler/docs/USER-GUIDE-fa.md)
- [Architecture](FilePromptAssembler/docs/ARCHITECTURE.md) · [Design decisions](FilePromptAssembler/docs/DECISIONS.md) · [Setup](FilePromptAssembler/docs/SETUP.md) · [Status](FilePromptAssembler/docs/STATUS.md)
