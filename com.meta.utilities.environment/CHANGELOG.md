# Changelog

All notable changes to this project will be documented in this file.

## [1.0.2] - 2026-10-23

### Fixed

- Ocean shader no longer loses fog with URP 17.3 (Unity 6000.3). The ocean sub-target now includes `CoreIncludes.FogPregraph`, which declares the fog keywords that URP 17.3 removed from `CorePragmas.Forward`.
- Sky no longer turns reflective surfaces white with URP 17.3 on Vulkan. Shader Graph adds an `XRMotionVectors` pass there, which gave `SG_Skybox_New` exactly 6 passes, so Unity drew it as a 6-sided skybox with only one valid face. The new `Meta/Environment/Skybox (Single Pass)` shader contains only the graph's Universal Forward pass, and `Base Sky` (the root of the sky, sun and moon materials) now uses it.
- Ocean skirting is no longer incorrectly frustum culled in VR. `QuadtreeRenderer` now draws it with `Graphics.RenderMesh` instead of `Graphics.DrawMesh`.

## [1.0.1] - 2025-05-16

### Changed

- README links now point to the North Star documentation on GitHub, and the README explains how to install the package from its Git URL.

## [1.0.0] - 2025-05-16

- First release, from the North Star graphics showcase.
