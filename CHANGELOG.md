# Changelog

## [0.2.0] - 2026-11-22

- Renderer feature: **Sky** and **Corruption** switches (both on by default, so existing setups are unchanged).
  - Sky off: the sky prints as bare paper, and `ChromoSky` (time-of-day styles, glow, horizon fog tint) is ignored.
  - Corruption off: `ChromoCorrupted` objects aren't gathered or drawn and the corruption map is ignored, so the
    print has no corruption at all.

## [0.1.0] - 2026-11-22

First package release, extracted from Marcescence (art style phases C1-C6 and corruption X1-X2).

- Namespace `Chromolithograph` (was `Marcescence.ArtStyle`); own assemblies `Chromolithograph` and
  `Chromolithograph.Editor`. Script GUIDs kept, so existing palettes, styles, volume profiles and renderer features
  keep their references.
- Menus: `Tools/Chromolithograph/Set Up`, `Create/Chromolithograph/Palette`, `Create/Chromolithograph/Style`,
  volume override `Chromolithograph`. Hidden shaders renamed to `Hidden/Chromolithograph/...`.
- Set Up's default folder is `Assets/Chromolithograph`.
- The F8 debug panel is standalone (IMGUI window); `ChromoDebugPanel.AutoSpawn` and `DrawControls()` let a project
  host it in its own overlay.
- Shader include path: `Packages/com.aidancmays.chromolithograph/ShaderLibrary/ChromoHints.hlsl`.
- Samples: Custom Shader, Sky Hook.
