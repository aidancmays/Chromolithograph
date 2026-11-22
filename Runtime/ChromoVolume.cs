using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Chromolithograph
{
    /// <summary>
    /// Per-area changes to the chromolithograph print (Add Override > Chromolithograph on a Volume
    /// Profile). The print's own settings are the palette's Look; only the settings ticked here replace them, blended
    /// by the volume's weight like any post effect.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("Chromolithograph")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class ChromoVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("Print")]
        [Tooltip("0 = the plain rendered frame, 1 = fully printed.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("Size of every stone's grain (1 = default).")]
        public ClampedFloatParameter grainSize = new ClampedFloatParameter(1.6f, 0.25f, 4f);

        [Tooltip("How much the grain breaks the colour into dots. 0 = smooth washes of colour, 1 = every speck is either ink or bare paper.")]
        public ClampedFloatParameter grainStrength = new ClampedFloatParameter(0.35f, 0f, 1f);

        [Tooltip("Blur (pixels at 1080p) on the colour before it is split into inks, so mixed colours run together instead of breaking into blotches. 0 = off.")]
        public ClampedFloatParameter washBlur = new ClampedFloatParameter(2f, 0f, 6f);

        [Tooltip("How soft the grain is at high Grain Strength. Low = crisp specks of ink and paper; high = gentle mottling.")]
        public ClampedFloatParameter grainSoftness = new ClampedFloatParameter(0.25f, 0.02f, 0.6f);

        [Tooltip("Scales every ink's registration offset (0 = perfect register).")]
        public ClampedFloatParameter registration = new ClampedFloatParameter(1f, 0f, 4f);

        [Tooltip("How unevenly the ink takes to the paper: slow blotches of stronger and weaker colour.")]
        public ClampedFloatParameter inkVariation = new ClampedFloatParameter(0.3f, 0f, 1f);

        [Header("Colour")]
        [Tooltip("Saturation of the scene's colour before it's split into inks (1 = as rendered).")]
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1.15f, 0f, 2.5f);

        [Tooltip("Contrast of the scene's colour before it's split into inks (1 = as rendered). Higher gives deeper shadows and more open lights.")]
        public ClampedFloatParameter contrast = new ClampedFloatParameter(1f, 0.5f, 1.8f);

        [Tooltip("Brightness of the scene's colour before it's split into inks, in stops (0 = as rendered). Brighter leaves more bare paper.")]
        public ClampedFloatParameter exposure = new ClampedFloatParameter(0f, -2f, 2f);

        [Tooltip("Pulls the print toward the scene's own colour, like the hand finishing on the Bien plates, so colours the inks can't mix still come through. 0 = inks only.")]
        public ClampedFloatParameter handColouring = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("Replaces this much of each ink's own grain with one shared texture, so every colour looks equally textured. 0 = each mix shows its own grain.")]
        public ClampedFloatParameter evenTexture = new ClampedFloatParameter(0.4f, 0f, 1f);

        [Tooltip("How strong the shared texture from Even Texture is.")]
        public ClampedFloatParameter textureDepth = new ClampedFloatParameter(0.3f, 0f, 1f);

        [Header("Wetness")]
        [Tooltip("How much the grain behaves like a wet wash: 0 = fine, dry crayon grain; 1 = soft blooms of pigment.")]
        public ClampedFloatParameter wetness = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Tooltip("Size of the wet blooms, as a multiple of the grain size.")]
        public ClampedFloatParameter bloomSize = new ClampedFloatParameter(4f, 1f, 12f);

        [Tooltip("How much darker a wash dries at its edges, where the colour changes (needs Wash Blur above 0).")]
        public ClampedFloatParameter edgePooling = new ClampedFloatParameter(0.35f, 0f, 1f);

        [Tooltip("How much pigment settles into the paper's tooth, so the fibre shows through the colour.")]
        public ClampedFloatParameter granulation = new ClampedFloatParameter(0.3f, 0f, 1f);

        [Header("Paper")]
        [Tooltip("Strength of the paper's fibre texture.")]
        public ClampedFloatParameter paperFibre = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("Amount of foxing (age spots).")]
        public ClampedFloatParameter foxing = new ClampedFloatParameter(0.35f, 0f, 1f);

        [Header("Key lines")]
        [Tooltip("Outline width in pixels at 1080p (thinner with distance). 0 turns outlines off.")]
        public ClampedFloatParameter lineWidth = new ClampedFloatParameter(1.2f, 0f, 4f);

        [Tooltip("How dark the outlines print.")]
        public ClampedFloatParameter lineStrength = new ClampedFloatParameter(0.9f, 0f, 1f);

        [Tooltip("How big a jump in depth makes an outline. Lower = more lines (and more noise on rough ground).")]
        public ClampedFloatParameter depthSensitivity = new ClampedFloatParameter(0.06f, 0.005f, 1f);

        [Tooltip("How sharp a fold makes a crease line (0 = every bend, 1 = only right angles and sharper).")]
        public ClampedFloatParameter creaseSensitivity = new ClampedFloatParameter(0.35f, 0.02f, 1f);

        [Tooltip("How much outlines waver, in pixels, so they look drawn on stone.")]
        public ClampedFloatParameter lineWobble = new ClampedFloatParameter(0.8f, 0f, 4f);

        [Tooltip("How much bright light breaks up outlines (engravers left highlights open).")]
        public ClampedFloatParameter lineHighlightBreak = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Tooltip("Distance in metres at which outlines are thinnest.")]
        public MinFloatParameter lineFarDistance = new MinFloatParameter(60f, 1f);

        [Tooltip("Outlines where one colour meets another, even with no step in depth (a flat stone on grass). 0 = off.")]
        public ClampedFloatParameter colourEdges = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Tooltip("How different two colours must be to get an outline. Lower = more lines.")]
        public ClampedFloatParameter colourSensitivity = new ClampedFloatParameter(0.12f, 0.02f, 1f);

        [Tooltip("Smallest step in depth, in metres, that gets an outline even when it's tiny next to its distance (edges seen head on).")]
        public ClampedFloatParameter depthGap = new ClampedFloatParameter(0.05f, 0.005f, 1f);

        [Tooltip("Outline where objects with different Chromo Styles meet. 0 = off.")]
        public ClampedFloatParameter styleOutlines = new ClampedFloatParameter(1f, 0f, 1f);

        [Header("Hatching")]
        [Tooltip("Strength of the key stone's hatching in deep shadow. 0 turns it off.")]
        public ClampedFloatParameter hatching = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("Space between hatch strokes, in pixels at 1080p.")]
        public ClampedFloatParameter hatchSpacing = new ClampedFloatParameter(5f, 2f, 20f);

        [Tooltip("Brightness (0..1) below which shadows start to be hatched; crossed strokes come in at half of it.")]
        public ClampedFloatParameter hatchBelow = new ClampedFloatParameter(0.3f, 0f, 1f);

        [Header("Deep shadow")]
        [Tooltip("Deep shadow: the darkest parts of the scene print as solid key ink, so they read as one even black instead of grey and black patches crossed by line work. 0 = off.")]
        public ClampedFloatParameter shadowFill = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("Brightness (0..1) below which the scene prints as solid key ink.")]
        public ClampedFloatParameter shadowBelow = new ClampedFloatParameter(0.04f, 0f, 0.5f);

        [Tooltip("How gradually the solid ink comes in above Shadow Below (in brightness). Small = a crisp edge.")]
        public ClampedFloatParameter shadowSoftness = new ClampedFloatParameter(0.03f, 0.001f, 0.2f);

        [Header("Distance")]
        [Tooltip("How much inks drop out with distance (0 = everything printed in full to the horizon).")]
        public ClampedFloatParameter distanceFade = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("Where inks start dropping out with distance, in metres (the palette's fade order sets which go first).")]
        public MinFloatParameter fadeStart = new MinFloatParameter(35f, 0f);

        [Tooltip("Where the last ink is gone and only paper is left, in metres.")]
        public MinFloatParameter fadeEnd = new MinFloatParameter(300f, 1f);

        [Tooltip("Distant land fades towards paper tinted with the procedural sky's horizon colour (ChromoSky) instead of bare paper, so far hills melt into the sky at dusk. 0 = bare paper (no change); only while the sky is running.")]
        public ClampedFloatParameter fogSkyTint = new ClampedFloatParameter(0f, 0f, 1f);

        [Header("Subject")]
        [Tooltip("How much the plate's subjects (ChromoSubject: the player and enemies) keep their inks and key lines with distance. 1 = always printed in full, 0 = they fade like everything else.")]
        public ClampedFloatParameter subjectHold = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("How much sooner everything that isn't a subject fades, so subjects stand out against a paler background. 0 = the distances above, 1 = four times sooner. Turn it down where threats must stay readable.")]
        public ClampedFloatParameter backgroundFade = new ClampedFloatParameter(0f, 0f, 1f);

        [Header("Sky")]
        [Tooltip("How much ink the sky gets (0 = bare paper, 1 = printed like everything else).")]
        public ClampedFloatParameter skyInk = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("Extra saturation for the sky, on top of Saturation.")]
        public ClampedFloatParameter skySaturation = new ClampedFloatParameter(1.4f, 0f, 3f);

        [Tooltip("Grain Strength for the sky (each ink's Grain Boost still adds to it). Low keeps the sky a soft wash.")]
        public ClampedFloatParameter skyGrainStrength = new ClampedFloatParameter(0.2f, 0f, 1f);

        [Tooltip("Size of the sky's grain relative to everything else. Smaller gives a finer, more detailed sky.")]
        public ClampedFloatParameter skyGrainSize = new ClampedFloatParameter(0.7f, 0.25f, 4f);

        [Tooltip("Night sky: a dark sky prints as one smooth wash of the palette's Night Sky ink, with no grain breaking it up, so it isn't blotchy. 0 = off.")]
        public ClampedFloatParameter skyNightFill = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Brightness (0..1) of the sky below which the Night Sky ink is laid.")]
        public ClampedFloatParameter skyNightBelow = new ClampedFloatParameter(0.15f, 0f, 1f);

        [Tooltip("How gradually the Night Sky ink comes in above Night Below (in brightness). Large = a soft blend into the printed sky.")]
        public ClampedFloatParameter skyNightSoftness = new ClampedFloatParameter(0.15f, 0.001f, 0.5f);

        [Header("Corruption")]
        [Tooltip("Corruption: how corrupted objects, materials and regions print (ChromoCorrupted, later the spreading field). How much corrupted surfaces give up their own colour for the palette's corruption inks (Dark, Mid and Light, picked by brightness).")]
        public ClampedFloatParameter corruptDrain = new ClampedFloatParameter(0.85f, 0f, 1f);

        [Tooltip("Strength of the crumpled-film facets and creases on corrupted surfaces.")]
        public ClampedFloatParameter corruptCrumple = new ClampedFloatParameter(0.8f, 0f, 1f);

        [Tooltip("Size of the crumpled facets on screen, in pixels at 1080p. Like the grain, they keep about this size at any distance (the texture steps by powers of two and blends between steps).")]
        public ClampedFloatParameter corruptCrumplePixels = new ClampedFloatParameter(120f, 10f, 600f);

        [Tooltip("How much pigment pools in the creases, darkening them toward the Corruption Dark ink.")]
        public ClampedFloatParameter corruptCreaseDark = new ClampedFloatParameter(0.7f, 0f, 1f);

        [Tooltip("How much the creases print as key-stone lines (the angular dark cracks).")]
        public ClampedFloatParameter corruptCreaseLines = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("How much each facet takes its own tone, so the film reads as separate planes.")]
        public ClampedFloatParameter corruptFacets = new ClampedFloatParameter(0.4f, 0f, 1f);

        [Tooltip("How wide the edge of corruption is, where it feathers into the clean print.")]
        public ClampedFloatParameter corruptEdge = new ClampedFloatParameter(0.25f, 0.01f, 1f);

        [Tooltip("How much the edge breaks into branching, feathery bleeds instead of a smooth line.")]
        public ClampedFloatParameter corruptBranching = new ClampedFloatParameter(0.7f, 0f, 1f);

        [Tooltip("How dark the edge dries, like a wet front of ink soaking into the paper.")]
        public ClampedFloatParameter corruptWetFront = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("Soft blooms inside corruption: paler pools with hard dark rims.")]
        public ClampedFloatParameter corruptBlooms = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("Size of the blooms in metres. They live in the world (stuck to surfaces, travelling with moving objects) and slowly drift and change shape.")]
        public ClampedFloatParameter corruptBloomSize = new ClampedFloatParameter(5f, 0.25f, 40f);

        [Tooltip("How fast the blooms drift across surfaces, in metres per second. 0 = they stay put.")]
        public ClampedFloatParameter corruptBloomDrift = new ClampedFloatParameter(0.1f, 0f, 2f);

        [Tooltip("How fast the blooms change shape. 0 = fixed shapes; 1 = a bloom becomes a different one in about a second.")]
        public ClampedFloatParameter corruptBloomMorph = new ClampedFloatParameter(0.04f, 0f, 1f);

        [Tooltip("Strength of a second layer of blooms 2.5 times bigger, for a few large pools among the others. 0 = off.")]
        public ClampedFloatParameter corruptBloomsLarge = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Tooltip("Strength of a third layer of blooms 0.4 times the size, for small spots among the others. 0 = off.")]
        public ClampedFloatParameter corruptBloomsSmall = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Tooltip("Pale specks in the darks of corruption, like salt dropped into a wet wash.")]
        public ClampedFloatParameter corruptSalt = new ClampedFloatParameter(0.3f, 0f, 1f);

        public bool IsActive() => active && intensity.value > 0f;

        [Obsolete("Unused by URP's render graph path.")]
        public bool IsTileCompatible() => true;
    }
}
