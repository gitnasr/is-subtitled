# Is Subtitled — Agent Guide

## Context Pointers
- **Project Architecture Skill**: Reference the `is-subtitled-architecture` skill (`SKILL.md`) for full system architecture, MVVM patterns, and domain boundaries.
- **Domain Ubiquitous Language**: Reference [GLOSSARY.md](file:///D:/Coding/Is%20Subtitled%20Project/GLOSSARY.md).
- **Architectural Decision Records**: Reference [docs/adr/](file:///D:/Coding/Is%20Subtitled%20Project/docs/adr/).
- **Environment Profile**: Reference `nasr-profile` skill.

---

## 1. Project Topology

```
D:\Coding\Is Subtitled Project\
├── GLOSSARY.md             # Canonical ubiquitous domain vocabulary
├── docs/                   # Architectural decisions (ADRs) and shared documentation
│   └── adr/                # 0001-..., 0002-...
├── app/                    # Production C# / Avalonia 12 / .NET 10 desktop application (git repo gitnasr/is-subtitled)
│   ├── .github/
│   │   ├── workflows/      # CI (ci.yml) and Automated Release (release.yml)
│   │   └── scripts/        # Release automation (calculate-version.mjs, generate-release-notes.mjs, release-lib.mjs)
│   ├── Models/             # Domain logic (SubtitleScanner, AppConfig, Platform, UpdateService)
│   ├── ViewModels/         # MVVM presentation layer
│   ├── Views/              # Avalonia XAML UI
│   ├── Styles/             # Earth Ember design system
│   ├── CHANGELOG.md        # Automatically synchronized changelog
│   └── IsSubtitled.csproj  # .NET 10 single-file compilation target
└── landing/                # Web landing page and distribution site
```

---

## 2. Invariants & Rules

### Invariant 1: Continuous Post-Edit Documentation & Skill Sync
**Rule**: After **every** edit (code, configuration, UI, or infrastructure), immediately check whether the skill, agent guide, or root docs require synchronization:
1. **Project Skill (`SKILL.md`)**:
   - If an architectural pattern, model contract, scanner logic, dependency, or development command changed, update `D:\Coding\Is Subtitled Project\.agents\skills\is-subtitled-architecture\SKILL.md` and mirror to `C:\Users\nasr-pc\.t3\userdata\providers\antigravity\ac0a3dfd6dddb20962cecff6ee5fe65e19d3923be20e52c5ab52ff877f7e4c32\config\skills\is-subtitled-architecture\SKILL.md`.
2. **Agent Guide (`AGENTS.md`)**:
   - If workflows, commands, repository structures, or validation rules evolve, update `D:\Coding\Is Subtitled Project\AGENTS.md` and ensure `app/AGENTS.md` is synchronized.
3. **Root Docs (`GLOSSARY.md` & `docs/adr/`)**:
   - When a new domain concept or term is introduced, sharpened, or altered, update `GLOSSARY.md` inline immediately.
   - When an architectural trade-off that is hard to reverse and surprising without context is made, record an ADR in `docs/adr/`.

---

### Invariant 2: Automated CI/CD Release & Changelog Synchronization
**Rule**: Every push to `main` triggers the automated release pipeline in `.github/workflows/release.yml`.

#### How the Automated Pipeline Works:
1. **Version Calculation (`calculate-version.mjs`)**:
   - Scans commits since the latest git tag.
   - Computes SemVer bump from Conventional Commits (`feat:` -> minor, `fix:` -> patch, `BREAKING CHANGE:` -> major).
   - If `IsSubtitled.csproj` has already been bumped manually by the author or agent, that target version is prioritized.
2. **Changelog & Notes (`generate-release-notes.mjs`)**:
   - Categorizes commits into Highlights/Features, Fixes, Performance, Docs, and Maintenance.
   - Prepends the formatted entry into `app/CHANGELOG.md`.
   - Generates release notes for the GitHub Release body.
3. **Commit & Tagging**:
   - Commits `IsSubtitled.csproj` and `CHANGELOG.md` with message `chore(release): vX.Y.Z [skip ci]`.
   - Tags the commit `vX.Y.Z` and pushes tag to remote.
4. **Multi-Platform Build & Publish**:
   - Compiles self-contained executables:
     - `IsSubtitled.exe` (Windows x64 single-file)
     - `IsSubtitled-linux-x64.tar.gz` (Linux x64)
     - `IsSubtitled-osx-x64.tar.gz` & `IsSubtitled-osx-arm64.tar.gz` (macOS)
   - Publishes the GitHub Release via `gh release create` and uploads all binaries.

#### Agent Commit Requirements:
- Strictly adhere to **Conventional Commits** (`feat:`, `fix:`, `perf:`, `docs:`, `refactor:`, `chore:`).
- For breaking changes, use `feat!:` or `BREAKING CHANGE:` in commit footer.
- The pipeline will handle the bump, changelog update, tag, build, and release.

---

## 3. Architecture & Code Standards

- **Runtime & UI**: .NET 10.0, Avalonia 12.0.4, CommunityToolkit.Mvvm (Source Generators: `[ObservableProperty]`, `[RelayCommand]`).
- **Filesystem Inspection**:
  - Scanning is strictly sidecar-based (per ADR-0001).
  - Directory traversal must use `DirectoryInfo` (not `Directory.GetFiles`) for zero-allocation metadata on Windows.
  - Cancellable via `CancellationToken`.
  - Progress reporting throttled to once per directory (`IProgress<ScanProgress>`) to prevent UI dispatch flooding.
- **Auto-Update**: Direct GitHub Releases polling, background streaming download with progress reporting, and non-blocking process replacement (per ADR-0003).
- **Theme**: Earth Ember dark theme defined in `Styles/Theme.axaml` and `Styles/Controls.axaml`. Avoid hardcoded HEX colors in views.
- **Platform Interop**: File reveal and path operations are abstracted through `IUiServices` and `Models/Platform.cs` for cross-platform compatibility (Windows Explorer, macOS Finder, Linux dbus/xdg-open).

---

## 4. Local Build & Validation Commands

Run from `D:\Coding\Is Subtitled Project\app`:

```powershell
# Build application
dotnet build IsSubtitled.csproj

# Run test suite
dotnet test tests/IsSubtitled.Tests/IsSubtitled.Tests.csproj

# Run in development mode
dotnet run --project IsSubtitled.csproj

# Test publish for Windows
dotnet publish IsSubtitled.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64

# Test version calculation locally
node .github/scripts/calculate-version.mjs
```



