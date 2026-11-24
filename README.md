# Chromolithograph

A URP renderer feature that prints every frame as a 19th-century chromolithograph, in the manner of the Bien edition
of Audubon's *Birds of America*. The rendered image is separated into up to eight flat inks, and each ink is laid on
the paper as its own stone, with crayon grain, ruled hatching, a dark key stone for outlines, washes, paper tooth and
foxing. Objects can carry their own print styles, characters can be printed as the plate's "subject" against a paler
background, and an optional corruption layer turns surfaces into crumpled, bleeding watercolour.

Everything is a post-process on the camera's colour, depth and normals, so it works with any URP materials.

## Requirements

- Unity 6.3 (6000.3) or newer
- Universal Render Pipeline 17.3 (Render Graph, the default)
- Input System 1.18 (only for the F8 debug panel's hotkey; the panel falls back to the old Input Manager otherwise)

## Install

Package Manager > **+** > *Add package from disk...* and pick this folder's `package.json`, or *Add package from git
URL...* with the repository URL once it has one. To edit the package inside a project, copy the folder into that
project's `Packages/` folder instead (an embedded package).

## Set up in a project

1. **Tools > Chromolithograph > Set Up.** This
   - uses the first `ChromoPalette` in the project, or makes `Assets/Chromolithograph/Audubon Palette.asset`,
   - bakes it (the colour separation and grain textures, about two seconds),
   - adds the Chromolithograph feature to every renderer the active URP asset uses,
   - warns about Bloom and Vignette, which fight the print look.
2. Press Play. **F8** opens the debug panel (editor and development builds): print on/off, progressive proofs (stones
   printed one after another), one stone at a time, coverage, and the corruption view.
3. Projects without a visible sky or corruption can turn **Sky** and **Corruption** off on the renderer feature;
   that skips their work and prints the sky as bare paper.
4. Tune the print on the palette's **Look** (it is the base settings), and per area with a Volume:
   *Add Override > Chromolithograph* (only ticked settings replace the palette's).

The feature asks URP for the depth and normals textures itself; nothing else needs switching on. The camera should
render HDR with post-processing on. Neutral tonemapping is what the look was tuned with.

## The palette

`Create > Chromolithograph > Palette`. A palette holds:

- **Inks** (up to 8): colour, density, grain style and scale, fade order with distance, registration offset, cost
  (how reluctantly the separation uses it).
- **Paper**, **foxing**, **key** (outline ink), **shadow** (deep-shadow ink), **night sky**, and five
  **corruption** colours.
- **Look**: every print setting, grouped as Print, Colour, Wetness, Paper, Key lines, Hatching, Deep shadow,
  Distance, Subject, Sky and Corruption.
- Separation settings (ink cost, mix smoothness, seed).

Changing inks, paper or grain marks the palette as needing a bake; the inspector shows a Bake button. Look settings
apply live.

## Per-object control

| Component / asset | What it does |
|---|---|
| `ChromoStyle` (asset, `Create > Chromolithograph > Style`) | A set of Look overrides (tick a setting to override it). Up to 15 styles in use at once. |
| `ChromoStyled` | Prints an object (and its children) with a `ChromoStyle`. Outlines appear where styles meet (Look > Style Outlines). |
| `ChromoSubject` | Marks the plate's subject: keeps full inks and key lines with distance while the background fades (Look > Subject). |
| `ChromoMoving` | For anything that moves (rigid or skinned): keeps the print's world-mapped grain stuck to the object instead of sliding across it. |
| `ChromoCorrupted` | Corruption amount 0-1 for an object and its children; set `Amount` from gameplay to spread or cleanse. |

Styles are tracked in the rendering layer mask: in Play mode `ChromoStyled` uses rendering layer bits 17-31 on its
renderers, so keep those bits free for it.

## Custom shaders

Shaders can choose a print style per pixel, and shaders that move their own vertices (wind, vertex animation) can
tell the print where the unmoved surface is. Include

```hlsl
#include "Packages/com.aidancmays.chromolithograph/ShaderLibrary/ChromoHints.hlsl"
```

and add a `ChromoHints` pass and/or a `ChromoMotion` pass. The header of that file documents both passes; the
**Custom Shader** sample is a complete working shader. Style ids come from `ChromoStyles.Acquire(style)` and
`ChromoStyles.IdOf(style)`; refill material properties on `ChromoStyles.Changed`.

A `ChromoStyled` on the same object wins over the shader's own pass (ChromoStyleOwners switches the pass off for it).

## Hooks for other systems

These are static and optional; the print works without any of them.

- **`ChromoSky`**: the sky's horizon and zenith colours, sun direction and up, sky print styles (`Style`, `StyleTo`,
  `StyleBlend`) and the sunrise/sunset glow style. Write them every frame and set `Frame = Time.frameCount`; while
  nothing writes them, sky pixels use the plain Look and the default colours. The **Sky Hook** sample drives them from
  a sun light and two colours. If your sky shader draws tonemapped colours, write the inverse-tonemapped colour.
- **`ChromoStrip`**: a vignette in which the inks flake away to bare paper (for exhaustion or damage effects).
- **Corruption map**: a world-space corruption texture can be fed as shader globals
  `_ChromoCorruptionMap` (R8, sampled in world XZ), `_ChromoCorruptionMap1` = (1 / window size in metres,
  window min x, window min z, 1 = on) and `_ChromoCorruptionMap2` = (window max x, window max z). The print takes the
  larger of that and the per-object `ChromoCorrupted` amount. Leave `_ChromoCorruptionMap1.w` at 0 to switch it off.
- **Visibility (fog)**: a render pass that hides the scene (volumetric fog, smoke) can share how much of each pixel
  still shows through, as a global texture `_ChromoVisibility` (R: 1 = clear, 0 = hidden) plus the global float
  `_ChromoVisibilityOn` = 1, set before the print runs (in Render Graph: `SetGlobalTextureAfterPass` and
  `SetGlobalFloat` on a pass with global state modification allowed). Where the scene is hidden its key lines,
  hatching, grain, edge pooling and deep shadow fade, so it prints as flat washes of the fog's colour. The print sets
  `_ChromoVisibilityOn` back to 0 after each camera, so it only applies while something shares it. The Miasma fog
  package does this.
- **`ChromoDebug`**: the debug view (`Mode`, `Ink`). A project with its own debug overlay can set
  `ChromoDebugPanel.AutoSpawn = false` before the first scene loads and call `ChromoDebugPanel.DrawControls()` from
  its own panel.

## Samples

Import from the package's page in the Package Manager.

- **Custom Shader**: `Chromolithograph Samples/Sway`, a swaying shader with Forward, DepthOnly, DepthNormals,
  ChromoHints (one style below a height, another above) and ChromoMotion passes, plus `ChromoSampleStyles` to give
  it its two style ids.
- **Sky Hook**: `ChromoSkyHook`, which feeds `ChromoSky` from the scene's sun and blends day/night sky styles by the
  sun's height.

## Limits and notes

- 8 inks per palette, 16 style slots (0 is the plain Look, 15 for styles), plus the two sky slots.
- Cameras with post-processing off (for example portal or render-texture cameras) get key lines only, if
  *Lines In Portal Views* is ticked on the feature.
- The look reads the frame after post-processing, so Bloom and Vignette are printed too; Set Up warns about them.
- Baked palettes carry their textures as sub-assets (several MB). Set Up makes and bakes one, so a fresh project
  does not need a baked palette copied in.
