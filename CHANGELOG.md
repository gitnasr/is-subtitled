# Changelog

## [2.3.4](https://github.com/gitnasr/is-subtitled/compare/v2.3.3...v2.3.4) (2026-10-27)

### Bug Fixes
* Restore header version pill (`v2.3.4`), **Check for updates** button, inline auto-update notification banner with download progress bar and **Restart & Install** button, and folder exclusion **Add** button in `Views/MainWindow.axaml` (accidentally overwritten during `v2.3.2` encoding cleanup)
* Extract `.tar.gz` archives before replacing executable in `UpdateService.ApplyUpdateAndRestart` on Linux and macOS
* Route release notes browser launch through `Platform.OpenUrl` and add XAML UI contract regression test in `UpdateTests.cs`

## [2.3.2](https://github.com/gitnasr/is-subtitled/compare/v2.3.1...v2.3.2) (2026-10-27)

### Bug Fixes
* Replace multi-byte UTF-8 glyphs in `Views/MainWindow.axaml` with XML numeric character entities (`&#x2026;`, `&#x2715;`, `&#x29C9;`, `&#x2197;`, `&#x2713;`, `&#x2315;`, `&#x25CF;`) to permanently prevent Windows CP1252/ANSI mojibake
* Use Unicode escape sequences in `Converters.cs` (`\u25BE`, `\u25B8`) and `ResultModels.cs` (`\u2014`) for 100% 7-bit ASCII resilience
* Clean up corrupted doc comment glyphs in `MainWindowViewModel.cs`

## [2.3.1](https://github.com/gitnasr/is-subtitled/compare/v2.3.0...v2.3.1) (2026-10-27)

### Changes
* Automated release for `v2.3.1`.


## [2.3.0](https://github.com/gitnasr/is-subtitled/compare/v2.2.0...v2.3.0) (2026-10-27)

### Features
* Introduce in-app auto-update from GitHub Releases with background download and restart installer
* Add interactive update notification banner and header version check pill
* Add explicit "Add" button and auto-commit pending input for folder exclusions

### Bug Fixes
* Fix folder exclusion matching by canonicalizing path separators and supporting relative paths
* Fix root folder exclusion boundary check to prevent traversal when root itself is excluded

### Documentation & Architecture
* Add ADR-0001, ADR-0002, and ADR-0003
* Add domain ubiquitous language in GLOSSARY.md
* Add agent guide AGENTS.md and project architecture skill

### Testing & CI/CD
* Add comprehensive xUnit test suite (18 tests)
* Integrate automated PR test validation in ci.yml and release pipeline in release.yml


## [2.1.1](https://github.com/gitnasr/is-subtitled/compare/v2.1.0...v2.1.1) (2026-09-01)


### Documentation

* add a screenshot of the redesigned UI ([91f5ae9](https://github.com/gitnasr/is-subtitled/commit/91f5ae94674546f0d6d5bf8f8d8f96de589c14ce))
* add a screenshot of the redesigned UI ([b4730dc](https://github.com/gitnasr/is-subtitled/commit/b4730dc2c39974cd04f27f40ef5a578770099842))

## [2.1.0](https://github.com/gitnasr/is-subtitled/compare/v2.0.0...v2.1.0) (2026-09-01)


### Features

* redesign the UI to the earth ember design ([b4bc032](https://github.com/gitnasr/is-subtitled/commit/b4bc03217ff8d3c4b4a6bf5f0d35bc318da7770b))

## [2.0.0](https://github.com/gitnasr/is-subtitled/compare/v1.0.0...v2.0.0) (2026-09-01)


### Features

* rewrite as Avalonia desktop app ([4b7d7af](https://github.com/gitnasr/is-subtitled/commit/4b7d7af32716ca1a6a6f54f54f27f1af032c49d0))


### Documentation

* rewrite the README and add the ISC license file ([2e768e2](https://github.com/gitnasr/is-subtitled/commit/2e768e25fd29ef3777e3405af0cfb3c2137c220d))


### Build & CI

* replace the Python pipeline with automated .NET releases ([0b6556c](https://github.com/gitnasr/is-subtitled/commit/0b6556c8589f692f60c991a1bb3164105ade43a9))
