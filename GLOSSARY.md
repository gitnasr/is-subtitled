# Is Subtitled

Desktop application and web presence for scanning local media libraries to identify video files lacking matching subtitles.

## Language

**Media Root**:
The top-level filesystem directory selected by the user to recursively discover media files.
_Avoid_: Library folder, search path, target directory

**Video Asset**:
A standalone video media file whose extension belongs to the supported video container list.
_Avoid_: Movie, episode, stream, clip

**Sidecar Subtitle**:
An external text or timed-markup subtitle file residing on the filesystem alongside a video asset.
_Avoid_: Subtitle track, caption file, srt

**Subtitle Match**:
The rule-based pairing between a video asset and one or more sidecar subtitle files based on filename base matching and optional language tags.
_Avoid_: Subtitle link, sync, attachment

**Unsubtitled Video**:
A video asset for which no valid matching sidecar subtitle file exists under the active matching criteria.
_Avoid_: Missing file, incomplete media, uncaptioned video

**Media Group**:
A parent directory containing one or more unsubtitled video assets, summarizing cumulative count and aggregate disk size.
_Avoid_: Folder result, bucket, cluster, directory result

**Exclusion Rule**:
A path or folder name pattern configured to prune entire directory subtrees from filesystem recursion.
_Avoid_: Blacklist, skip pattern, ignore list

**Scan Session**:
A discrete execution of the filesystem scanner that traverses the media root, emitting progress counters and collapsible media groups.
_Avoid_: Scan job, crawler, search run

**Update Asset**:
The platform-specific compiled binary or archive attached to a GitHub release for distribution.
_Avoid_: Installer, patch file, setup

**Update Session**:
An asynchronous lifecycle checking remote release tags against the local application version, streaming background assets, and orchestrating replacement.
_Avoid_: Updater job, patcher, downloader
