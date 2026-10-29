# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] - 2026-10-29

### Added

- Render Graph support for URP 17 (Unity 6): `ViewportRenderPass` implements `RecordRenderGraph`. With Render Graph, URP skips passes that only implement `Execute`, so the viewport (for example North Star's telescope view) didn't draw. `Execute` is kept for Compatibility Mode; on URP 17.3 and newer it only compiles when the project defines `URP_COMPATIBILITY_MODE`, as URP requires.

### Fixed

- Compile error with URP 14 (Unity 2022.3): use the 4-argument `XRBuiltinShaderConstants.UpdateBuiltinShaderConstants` from URP 14.0, not 15.0. URP 14 has no 6-argument overload.

## [1.0.2] - 2026-01-21

### Fixed

- Support newer versions of URP: use the `XRBuiltinShaderConstants.UpdateBuiltinShaderConstants` signature of URP 15 and newer, through an assembly definition with a version define.

## [1.0.1] - 2025-05-16

### Changed

- README explains how to install the package from its Git URL.

## [1.0.0] - 2025-05-16

- First release, from the North Star graphics showcase.
