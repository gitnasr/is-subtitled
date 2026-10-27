# Direct GitHub Releases auto-updater over external updater frameworks

Third-party updater frameworks (such as Squirrel or Velopack) introduce custom packaging, binary wrappers, and extra runtime dependencies that complicate self-contained single-file .NET desktop distribution. We decided to implement direct GitHub Releases API polling, background streaming download, and self-contained process replacement. This eliminates external packaging dependencies, leverages existing GitHub release assets, and provides a native in-app update experience with minimal overhead.
