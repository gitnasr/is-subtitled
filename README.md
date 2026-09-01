# Is Subtitled

A small desktop app that scans a folder for video files **missing a matching subtitle file**, grouped by folder. Built with C# / [Avalonia](https://avaloniaui.net/) (.NET 10).

Because it's a native app, it has full local filesystem access and can **open any result straight in File Explorer** — no upload, no browser sandbox limits.

## Features

- Pick any local folder and recursively scan it
- Detects videos (`.mp4 .mkv .flv .avi .mov .wmv .ts`) with no same-named subtitle (`.srt .sub .ssa .ass`)
- Exclude folders by name (e.g. `COMP`)
- Click a file to reveal it in File Explorer; click 📋 / a folder name to copy its path
- Save results to a `.txt` file
- Remembers your last folder and excluded list (`%AppData%\IsSubtitled\config.json`)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (to build/run from source)

## Run

```bash
dotnet run
```

## Build a self-contained Windows .exe

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The single-file executable lands in `bin/Release/net10.0/win-x64/publish/`.

## Project layout

- `Models/SubtitleScanner.cs` — the scan algorithm
- `Models/AppConfig.cs` — settings persistence
- `Models/Platform.cs` — "reveal in file manager" (Windows/macOS/Linux)
- `ViewModels/MainWindowViewModel.cs` — UI logic & commands
- `Views/MainWindow.axaml` — the window
- Cross-platform via Avalonia (Windows/macOS/Linux), though "reveal" selects the file on Windows/macOS and opens the folder on Linux.

## License

ISC
