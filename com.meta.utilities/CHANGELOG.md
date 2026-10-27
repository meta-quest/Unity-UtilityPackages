# Changelog

All notable changes to this project will be documented in this file.

## [1.1.3] - 2026-10-27

### Fixed

- "Tools/PSO Cache/Save Vulkan PSO Cache" now works with Unity 6. Unity 6 stores the Vulkan pipeline cache in the app's internal cache folder (`/data/user/0/<package>/cache`) instead of `/sdcard/Android/data/<package>/cache`, so the tool now reads it from there (with `run-as` on Development builds, or directly when adb runs as root), checks that the file is a valid cache, and warns when it was made by another Unity version. Older Unity versions keep the previous behavior.

## [1.1.0] - 2023-04-17

### Added

- "Tools/Identify Missing References (via git)" menu item. This uses the git history of the repo to try to identify the sources of missing references.
- "Tools/Fix Incorrect Asset Names" menu item. This finds all assets that have Object names that differ from their file names.
- "Assets/Resave" menu item. This will fully reimport and reserialize an asset.
- New `CameraFollowing`, `HoverAbove`, and `ResetTransform` components.
- New extension method `IEnumerator.CatchExceptions`. This wraps a coroutine method so that its exceptions are logged.

### Fixed

- Fixed issue where `AutoSetPostProcessor` would fail to set inherited fields in child classes.

### Changed

- `NetcodeHashFixer` has been simplified.
