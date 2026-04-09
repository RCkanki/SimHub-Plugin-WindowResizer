# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.1] - 2026-04-09

### Fixed

- Clamp resized windows to monitor bounds so applied layouts remain fully visible.
- Shrink width/height when requested size exceeds the current monitor bounds.

## [0.1.0] - 2026-04-01

Initial development release. Behavior and settings may change before **1.0.0**.

### Added

- Named window layout profiles (position, size, flags, optional title filter).
- Window picker (refresh list, select process/title).
- Per-profile options: borderless, bring to front, focus, NEXT/PREV cycle membership.
- Auto-resize when a window appears (with delay).
- SimHub actions: NextProfile, PrevProfile, ApplyProfileByName, TestMoveActiveWindow.
- JSON persistence under SimHub base directory: `WindowResizer/profiles.json`.
