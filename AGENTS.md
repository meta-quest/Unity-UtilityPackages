# Agent Instructions — Unity Utility Packages

A collection of standalone Unity UPM packages developed by Meta, covering both general-purpose Unity utilities and XR-specific helpers (input, avatars, environment, ropes, multiplayer, tutorials, narrative). Consumed by other Meta Quest Unity samples.

## Source-of-truth files (read these first, do not duplicate their contents in this file)

For setup, package list, and install instructions, read:

- `README.md` — full package catalog with descriptions and Git URL install examples
- `<package>/package.json` — each `com.meta.*` directory is its own UPM package; the manifest lists its name, version, and dependencies
- `<package>/README.md` — per-package usage and Git URL install snippet
- `<package>/CHANGELOG.md` — per-package version history
- `.gitattributes` — Git LFS configuration
- `LICENSE` — license terms

## Quest / Horizon-specific notes

- This repo is NOT a Unity project — there is no `Assets/`, no `ProjectSettings/`, no `Packages/manifest.json` at the root. Each top-level `com.meta.*` directory is a self-contained UPM package meant to be added to a separate consuming Unity project via Git URL (`?path=com.meta.utilities` etc.). Do not try to open the repo root in Unity Hub.
- When refactoring or bumping a dependency, edit only the relevant `com.meta.*/package.json` and source files — do not introduce cross-package implicit dependencies that are not declared in the per-package `package.json`.

## Meta Quest tooling

This repository is part of the Meta Quest / Horizon OS ecosystem (a sample, library, template, or related project — the bespoke intro above describes which). Use that intro and the source-of-truth files it references for project-specific decisions; don't restate or invent facts from memory.

When the user asks anything about Quest device behavior, build / deploy / debug / capture flows, on-device performance, or Horizon OS APIs, reach for these tools instead of generic Unity answers:

- **`hzdb`** — Quest-aware ADB wrapper (device list, install / launch / stop, logs, screenshots, Perfetto traces, on-device docs search). Already wired up as an MCP server via `.mcp.json`, `.vscode/mcp.json`, and `.cursor/mcp.json`. Also runnable directly: `npx -y @meta-quest/hzdb <subcommand>`.
- **Meta Quest Agentic Tools** — the full skill set, including Unity-specific skills: [github.com/meta-quest/agentic-tools](https://github.com/meta-quest/agentic-tools). Install per your client (Claude Code: `/plugin install meta-vr@meta-quest`; Gemini CLI: `gemini extensions install https://github.com/meta-quest/agentic-tools`; Cursor / VS Code: install the **Meta Horizon** extension from the Marketplace).

A few behavior expectations:

- **Read this repo's files first.** Before answering anything project-specific, read `README.md` and whichever source-of-truth files the intro above points at. Don't restate their contents in chat — quote or link instead.
- **Use `hzdb` for device-side work.** Anything that touches an attached Quest (install, launch, logs, screenshot, capture, manifest inspection) goes through `hzdb`, not raw `adb`.
- **Check live Horizon OS docs before answering API questions.** `hzdb docs search "..."` queries the live docs; training data on Horizon OS APIs goes stale fast.
- **Don't fabricate SDK / engine versions.** If a version isn't visible in this repo's files, say so rather than guessing.
