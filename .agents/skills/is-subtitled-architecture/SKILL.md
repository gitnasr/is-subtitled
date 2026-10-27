---
name: is-subtitled-architecture
description: Complete architecture, domain models, release pipeline, and development standards for the Is Subtitled project (Desktop Avalonia .NET 10 app and web landing page). Use whenever inspecting, developing, refactoring, building, or releasing Is Subtitled.
---

# Is Subtitled Architecture & Development Reference

Comprehensive reference for the Is Subtitled ecosystem: desktop application (`app/`), upcoming marketing site (`landing/`), domain models, and release invariants.

## 1. System Topology & Contexts

```
D:\Coding\Is Subtitled Project\
├── GLOSSARY.md                                       # Canonical domain terms
├── docs/adr/                                         # Architectural Decision Records
│   ├── 0001-sidecar-only-subtitle-detection.md
│   ├── 0002-decoupled-app-and-landing-workspaces.md
│   └── 0003-direct-github-releases-auto-updater.md
├── app/                                              # Avalonia desktop application (git repo gitnasr/is-subtitled)
│   ├── .github/
│   │   ├── workflows/
│   │   │   ├── ci.yml                                # PR build & validation gate
│   │   │   └── release.yml                           # Automated bump, multi-target build & release
│   │   └── scripts/
│   │       ├── calculate-version.mjs                 # SemVer calculator from git log
│   │       ├── generate-release-notes.mjs            # Release notes & CHANGELOG.md synchronizer
│   │       └── release-lib.mjs                       # Pure commit classifier & bump decider
│   ├── Models/                                       # Core domain records, scanner, platform interop
│   │   ├── SubtitleScanner.cs                        # Recursive filesystem walker & matcher
│   │   ├── AppConfig.cs                              # User settings persistence (~/.issubtitled.json)
│   │   ├── Platform.cs                               # OS-native file reveal & clipboard interop
│   │   └── UpdateService.cs                          # GitHub Releases auto-checker & bg downloader
│   ├── ViewModels/                                   # MVVM presentation logic
│   │   ├── MainWindowViewModel.cs                    # Scan orchestration, filtering, sorting
│   │   ├── ResultModels.cs                           # FolderGroupViewModel, FileItemViewModel
│   │   ├── IUiServices.cs                            # Dialog and navigation service contracts
│   │   └── Converters.cs                             # UI formatting (byte size formatting, etc.)
│   ├── Views/                                        # Avalonia XAML UI
│   │   └── MainWindow.axaml                          # Split-pane layout (config left, results right)
│   ├── tests/IsSubtitled.Tests/                      # xUnit test suite
│   ├── Styles/                                       # Earth Ember design system
│   │   ├── Theme.axaml                               # Semantic palette & color resources
│   │   └── Controls.axaml                            # Custom control styles
│   ├── CHANGELOG.md                                  # Synchronized changelog
│   └── IsSubtitled.csproj                            # .NET 10 single-file compilation target
└── landing/                                          # Marketing and distribution web portal
```

## 2. Invariants & Rules

### Invariant 1: Continuous Post-Edit Documentation & Skill Sync
**Rule**: After **every** edit, immediately evaluate and apply necessary documentation updates:
- **`SKILL.md`**: Update when architecture, dependencies, scanner heuristics, or commands change. Mirror between project `.agents/` and global config skills.
- **`AGENTS.md`**: Update when agent workflows, invariants, or development tooling change.
- **Root Docs (`GLOSSARY.md`, `docs/adr/`)**: Update `GLOSSARY.md` as domain terms crystallize; record ADRs in `docs/adr/` for irreversible trade-offs.

### Invariant 2: Automated CI/CD Release & Changelog on Push
**Rule**: Every push to `main` triggers `.github/workflows/release.yml`.

1. **Version Calculation**:
   - `calculate-version.mjs` analyzes git commits against the previous tag.
   - Conventional Commits dictate bump: `feat:` -> minor, `fix:` -> patch, `BREAKING CHANGE:` -> major.
2. **Changelog & Notes**:
   - `generate-release-notes.mjs` prepends release entries to `CHANGELOG.md` and generates release notes.
3. **Commit & Tag**:
   - Commits version bump `chore(release): vX.Y.Z [skip ci]` and creates tag `vX.Y.Z`.
4. **Publish**:
   - Compiles native self-contained binaries:
     - Windows: `IsSubtitled.exe` (single-file)
     - Linux: `IsSubtitled-linux-x64.tar.gz`
     - macOS: `IsSubtitled-osx-x64.tar.gz` and `IsSubtitled-osx-arm64.tar.gz`
   - Publishes GitHub Release attaching all binaries.

## 3. Core Domain Models & Algorithms

- **`SubtitleScanner`**:
  - Traverses directory hierarchy using `DirectoryInfo` (syscall-optimized).
  - Video extensions: `.mp4`, `.mkv`, `.flv`, `.avi`, `.mov`, `.wmv`, `.ts`.
  - Subtitle extensions: `.srt`, `.sub`, `.ssa`, `.ass`.
  - Matching condition: Video without exact matching base-name subtitle in same directory is recorded in `DirectoryResult`.
  - Exclusion evaluation: Bare name or path prefix matching.
  - Progress reporting: Per-directory `ScanProgress(CurrentDirectory, FilesExamined, FoldersScanned, MissingFound)`.

- **`UpdateService`**:
  - Checks `https://api.github.com/repos/gitnasr/is-subtitled/releases/latest` for newer SemVer tags.
  - Automatically matches OS/architecture (`.exe` on Windows, `linux` on Linux, `osx` on macOS).
  - Downloads in background with `IProgress<double>` stream reporting.
  - Applies update via transient replacement script and restarts.

- **`AppConfig`**:
  - Persisted in `%USERPROFILE%\.issubtitled.json`.
  - Stores `LastFolder`, `ExcludedFolders`, `WindowWidth`, `WindowHeight`.

## 4. UI Architecture & Patterns

- **MVVM Pattern**: CommunityToolkit.Mvvm source generators:
  - `[ObservableProperty]` generates property change notifications.
  - `[RelayCommand]` generates `ICommand` bindings with execution guards (`CanExecute`).
- **UI Decoupling**: File manager reveal (`RevealInFileManager`) and folder picker dialogs are invoked via `IUiServices`, enabling headless unit testing.
- **Filtering & Sorting**:
  - `ResultsFilter`: Dynamic case-insensitive substring search across filename and directory path.
  - `ResultsSortOrder`: Sort by `Folder` (alphabetical), `FileCount` (descending), or `TotalSize` (descending).
- **Styling**: Earth Ember dark theme defined in `Styles/Theme.axaml`.
- **Encoding & UI Symbols**: Avalonia XAML files must use XML numeric character entities (`&#x2026;`, `&#x2715;`, `&#x29C9;`, `&#x2197;`, `&#x2713;`, `&#x2315;`, `&#x25CF;`) and C# files must use Unicode escapes (`\u25BE`, `\u25B8`, `\u2014`) to prevent Windows ANSI/CP1252 mojibake.


## 5. Development Commands

Run from `D:\Coding\Is Subtitled Project\app`:
```powershell
# Build application
dotnet build IsSubtitled.csproj

# Run tests
dotnet test tests/IsSubtitled.Tests/IsSubtitled.Tests.csproj

# Run locally in development
dotnet run --project IsSubtitled.csproj

# Publish self-contained Windows executable
dotnet publish IsSubtitled.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
```



