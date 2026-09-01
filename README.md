<h1 align="center">Is Subtitled</h1>

<p align="center">
  Scan a folder tree and find every video file that has no matching subtitle.
</p>

<p align="center">
  <a href="https://github.com/gitnasr/Is-Subtitled/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/gitnasr/Is-Subtitled/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/gitnasr/Is-Subtitled/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/gitnasr/Is-Subtitled?sort=semver"></a>
  <a href="#license"><img alt="License" src="https://img.shields.io/badge/license-ISC-blue.svg"></a>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey">
</p>

---

Point it at a media library, and it walks the whole tree and reports the videos sitting there
without a subtitle file next to them — grouped by folder, so you can see which season or
collection is the problem rather than reading a flat list of paths.

Built with C# and [Avalonia](https://avaloniaui.net/) on .NET 10. Native, so it has real
filesystem access: results open straight in your file manager, nothing is uploaded, and the
folder you picked is still there next time you launch it.

## Install

Download `IsSubtitled.exe` from the [latest release](https://github.com/gitnasr/Is-Subtitled/releases/latest)
and run it. It is a self-contained single file — no .NET runtime installation needed.

To run from source instead, see [Building](#building).

## Usage

1. **Choose folder** — pick the root of your library. It is scanned recursively.
2. **Exclude folders** *(optional)* — type a name and press <kbd>Enter</kbd>. Separate several
   with commas or semicolons to add them in one go. Entries show as chips you can remove.
3. **Scan** — runs in the background and can be cancelled mid-scan. While it runs you see the
   folder being walked, the number of files examined, and how many are missing so far.
4. **Act on the results** — videos are grouped by folder, each group collapsible and badged with
   its file count and shown with each file's size. Filter by name and sort by folder, file count
   or total size. Click a filename to reveal it in your file manager, click a folder header to
   open it, or use the copy button for either path. **Save results…** writes a plain-text report.

Your last folder and exclusion list are restored on the next launch.

### How a video counts as "missing subtitles"

A video is reported when **no subtitle file in the same folder shares its exact base name**.

| Extension type | Recognised |
| --- | --- |
| Video | `.mp4` `.mkv` `.flv` `.avi` `.mov` `.wmv` `.ts` |
| Subtitle | `.srt` `.sub` `.ssa` `.ass` |

```text
Movie.mkv + Movie.srt      -> has subtitles
Movie.mkv + Movie.en.srt   -> reported (base names differ: "Movie" vs "Movie.en")
Movie.mkv alone            -> reported
```

Matching is case-insensitive. Language-suffixed subtitles are **not** matched yet — see
[Known limitations](#known-limitations).

### Exclusions

An exclusion entry can take either form, matched case-insensitively:

- a **bare folder name** — `COMP` skips every folder named `COMP`, anywhere in the tree
- a **full path** — `H:\PX\COMP` skips that one folder and everything beneath it

Folders the OS refuses to read are skipped silently rather than aborting the scan.

## Configuration

Settings are written the moment you change the folder or the exclusion list:

| OS | Path |
| --- | --- |
| Windows | `%AppData%\IsSubtitled\config.json` |
| macOS / Linux | `~/.config/IsSubtitled/config.json` |

Delete that file to reset the app to defaults. A corrupt file is ignored rather than fatal.

## Known limitations

- Subtitles must match the video's base name **exactly** — `Movie.en.srt` does not satisfy
  `Movie.mkv`. Multi-language libraries will show false positives.
- Embedded subtitle tracks muxed into an `.mkv` are not inspected; only sidecar files count.
- Reveal-in-file-manager selects the file on Windows and macOS, but only opens the containing
  folder on Linux.
- Prebuilt binaries are published for `win-x64` only. macOS and Linux run fine from source.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run
```

Self-contained single-file executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The binary lands in `bin/Release/net10.0/win-x64/publish/`. Swap the `-r` value for
`osx-arm64` or `linux-x64` to target another platform.

## Project layout

| Path | Responsibility |
| --- | --- |
| `Models/SubtitleScanner.cs` | Directory walk and the missing-subtitle rule |
| `Models/AppConfig.cs` | Loading and saving `config.json` |
| `Models/Platform.cs` | Reveal-in-file-manager per OS, reusing an open Explorer window |
| `Styles/Theme.axaml` | Colour, radius and font tokens — the whole palette |
| `Styles/Controls.axaml` | Control styles built on those tokens |
| `ViewModels/MainWindowViewModel.cs` | Commands, scan lifecycle, cancellation |
| `Views/MainWindow.axaml` | The window |

MVVM via [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet), with Avalonia
compiled bindings enabled.

## Contributing

Commits follow [Conventional Commits](https://www.conventionalcommits.org/) — the prefix picks
the next version number, so it matters:

| Prefix | Release |
| --- | --- |
| `fix:` | patch |
| `feat:` | minor |
| `feat!:` or a `BREAKING CHANGE:` footer | major |
| `docs:` `chore:` `refactor:` `ci:` | none on its own |

Pull requests run a build with warnings treated as errors and attach the resulting exe as a
14-day artifact, so reviewers can test the actual binary.

## Releases

Fully automated. Every push to `main` runs
[release-please](https://github.com/googleapis/release-please-action), which reads the commits
since the last tag and keeps a release PR open with the next version and a generated
`CHANGELOG.md`. Merging that PR:

1. bumps `<Version>` in `IsSubtitled.csproj`
2. commits the updated `CHANGELOG.md`
3. tags `vX.Y.Z` and publishes the GitHub release
4. builds the `win-x64` executable and attaches it to that release

## License

[ISC](LICENSE) © Mahmoud Nasr
