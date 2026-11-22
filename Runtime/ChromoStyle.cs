using System;
using UnityEngine;

namespace Chromolithograph
{
    /// <summary>One setting a style may change: ticked = use value, unticked = keep the palette's Look.</summary>
    [Serializable]
    public struct ChromoOverride
    {
        public bool on;
        public float value;

        public ChromoOverride(float value)
        {
            on = false;
            this.value = value;
        }
    }

    /// <summary>Slider range for a ChromoOverride in the inspector.</summary>
    public class ChromoRangeAttribute : PropertyAttribute
    {
        public readonly float min, max;
        public ChromoRangeAttribute(float min, float max) { this.min = min; this.max = max; }
    }

    /// <summary>
    /// A print style for particular objects (flat rocks, the player, a building): the settings ticked here replace the
    /// palette's Look on those objects only. Put a ChromoStyled component on the objects to use it. Where two styles
    /// meet on screen, an outline is drawn.
    /// </summary>
    [CreateAssetMenu(menuName = "Chromolithograph/Style", fileName = "Chromo Style")]
    public class ChromoStyle : ScriptableObject
    {
        [Header("Print")]
        [Tooltip("0 = the plain rendered frame, 1 = fully printed.")]
        [ChromoRange(0f, 1f)] public ChromoOverride intensity = new ChromoOverride(1f);

        [Tooltip("Size of every stone's grain (1 = default).")]
        [ChromoRange(0.25f, 4f)] public ChromoOverride grainSize = new ChromoOverride(1.6f);

        [Tooltip("How much the grain breaks the colour into dots. 0 = smooth washes of colour, 1 = every speck is either ink or bare paper.")]
        [ChromoRange(0f, 1f)] public ChromoOverride grainStrength = new ChromoOverride(0.35f);

        [Tooltip("Blur (pixels at 1080p) on the colour before it is split into inks, so mixed colours run together instead of breaking into blotches. 0 = off.")]
        [ChromoRange(0f, 6f)] public ChromoOverride washBlur = new ChromoOverride(2f);

        [Tooltip("How soft the grain is at high Grain Strength. Low = crisp specks of ink and paper; high = gentle mottling.")]
        [ChromoRange(0.02f, 0.6f)] public ChromoOverride grainSoftness = new ChromoOverride(0.25f);

        [Tooltip("Scales every ink's registration offset (0 = perfect register).")]
        [ChromoRange(0f, 4f)] public ChromoOverride registration = new ChromoOverride(1f);

        [Tooltip("How unevenly the ink takes to the paper: slow blotches of stronger and weaker colour.")]
        [ChromoRange(0f, 1f)] public ChromoOverride inkVariation = new ChromoOverride(0.3f);

        [Header("Colour")]
        [Tooltip("Saturation of the scene's colour before it's split into inks (1 = as rendered).")]
        [ChromoRange(0f, 2.5f)] public ChromoOverride saturation = new ChromoOverride(1.15f);

        [Tooltip("Contrast of the scene's colour before it's split into inks (1 = as rendered). Higher gives deeper shadows and more open lights.")]
        [ChromoRange(0.5f, 1.8f)] public ChromoOverride contrast = new ChromoOverride(1f);

        [Tooltip("Brightness of the scene's colour before it's split into inks, in stops (0 = as rendered). Brighter leaves more bare paper.")]
        [ChromoRange(-2f, 2f)] public ChromoOverride exposure = new ChromoOverride(0f);

        [Tooltip("Pulls the print toward the scene's own colour, like the hand finishing on the Bien plates, so colours the inks can't mix still come through. 0 = inks only.")]
        [ChromoRange(0f, 1f)] public ChromoOverride handColouring = new ChromoOverride(0.5f);

        [Tooltip("Replaces this much of each ink's own grain with one shared texture, so every colour looks equally textured. 0 = each mix shows its own grain.")]
        [ChromoRange(0f, 1f)] public ChromoOverride evenTexture = new ChromoOverride(0.4f);

        [Tooltip("How strong the shared texture from Even Texture is.")]
        [ChromoRange(0f, 1f)] public ChromoOverride textureDepth = new ChromoOverride(0.3f);

        [Header("Wetness")]
        [Tooltip("How much the grain behaves like a wet wash: 0 = fine, dry crayon grain; 1 = soft blooms of pigment.")]
        [ChromoRange(0f, 1f)] public ChromoOverride wetness = new ChromoOverride(0.6f);

        [Tooltip("Size of the wet blooms, as a multiple of the grain size.")]
        [ChromoRange(1f, 12f)] public ChromoOverride bloomSize = new ChromoOverride(4f);

        [Tooltip("How much darker a wash dries at its edges, where the colour changes (needs Wash Blur above 0).")]
        [ChromoRange(0f, 1f)] public ChromoOverride edgePooling = new ChromoOverride(0.35f);

        [Tooltip("How much pigment settles into the paper's tooth, so the fibre shows through the colour.")]
        [ChromoRange(0f, 1f)] public ChromoOverride granulation = new ChromoOverride(0.3f);

        [Header("Paper")]
        [Tooltip("Strength of the paper's fibre texture.")]
        [ChromoRange(0f, 1f)] public ChromoOverride paperFibre = new ChromoOverride(0.5f);

        [Tooltip("Amount of foxing (age spots).")]
        [ChromoRange(0f, 1f)] public ChromoOverride foxing = new ChromoOverride(0.35f);

        [Header("Key lines")]
        [Tooltip("Outline width in pixels at 1080p (thinner with distance). 0 turns outlines off.")]
        [ChromoRange(0f, 4f)] public ChromoOverride lineWidth = new ChromoOverride(1.2f);

        [Tooltip("How dark the outlines print.")]
        [ChromoRange(0f, 1f)] public ChromoOverride lineStrength = new ChromoOverride(0.9f);

        [Tooltip("How big a jump in depth makes an outline. Lower = more lines (and more noise on rough ground).")]
        [ChromoRange(0.005f, 1f)] public ChromoOverride depthSensitivity = new ChromoOverride(0.06f);

        [Tooltip("How sharp a fold makes a crease line (0 = every bend, 1 = only right angles and sharper).")]
        [ChromoRange(0.02f, 1f)] public ChromoOverride creaseSensitivity = new ChromoOverride(0.35f);

        [Tooltip("How much outlines waver, in pixels, so they look drawn on stone.")]
        [ChromoRange(0f, 4f)] public ChromoOverride lineWobble = new ChromoOverride(0.8f);

        [Tooltip("How much bright light breaks up outlines (engravers left highlights open).")]
        [ChromoRange(0f, 1f)] public ChromoOverride lineHighlightBreak = new ChromoOverride(0.6f);

        [Tooltip("Distance in metres at which outlines are thinnest.")]
        [ChromoRange(1f, 200f)] public ChromoOverride lineFarDistance = new ChromoOverride(60f);

        [Tooltip("Outlines where one colour meets another, even with no step in depth (a flat stone on grass). 0 = off.")]
        [ChromoRange(0f, 1f)] public ChromoOverride colourEdges = new ChromoOverride(0.6f);

        [Tooltip("How different two colours must be to get an outline. Lower = more lines.")]
        [ChromoRange(0.02f, 1f)] public ChromoOverride colourSensitivity = new ChromoOverride(0.12f);

        [Tooltip("Smallest step in depth, in metres, that gets an outline even when it's tiny next to its distance (edges seen head on).")]
        [ChromoRange(0.005f, 1f)] public ChromoOverride depthGap = new ChromoOverride(0.05f);

        [Header("Hatching")]
        [Tooltip("Strength of the key stone's hatching in deep shadow. 0 turns it off.")]
        [ChromoRange(0f, 1f)] public ChromoOverride hatching = new ChromoOverride(0.5f);

        [Tooltip("Space between hatch strokes, in pixels at 1080p.")]
        [ChromoRange(2f, 20f)] public ChromoOverride hatchSpacing = new ChromoOverride(5f);

        [Tooltip("Brightness (0..1) below which shadows start to be hatched; crossed strokes come in at half of it.")]
        [ChromoRange(0f, 1f)] public ChromoOverride hatchBelow = new ChromoOverride(0.3f);

        [Header("Deep shadow")]
        [Tooltip("Deep shadow: the darkest parts of the scene print as solid key ink, so they read as one even black instead of grey and black patches crossed by line work. 0 = off.")]
        [ChromoRange(0f, 1f)] public ChromoOverride shadowFill = new ChromoOverride(1f);

        [Tooltip("Brightness (0..1) below which the scene prints as solid key ink.")]
        [ChromoRange(0f, 0.5f)] public ChromoOverride shadowBelow = new ChromoOverride(0.04f);

        [Tooltip("How gradually the solid ink comes in above Shadow Below (in brightness). Small = a crisp edge.")]
        [ChromoRange(0.001f, 0.2f)] public ChromoOverride shadowSoftness = new ChromoOverride(0.03f);

        [Header("Distance")]
        [Tooltip("How much inks drop out with distance (0 = everything printed in full to the horizon).")]
        [ChromoRange(0f, 1f)] public ChromoOverride distanceFade = new ChromoOverride(1f);

        [Tooltip("Where inks start dropping out with distance, in metres (the palette's fade order sets which go first).")]
        [ChromoRange(0f, 500f)] public ChromoOverride fadeStart = new ChromoOverride(35f);

        [Tooltip("Where the last ink is gone and only paper is left, in metres.")]
        [ChromoRange(1f, 1000f)] public ChromoOverride fadeEnd = new ChromoOverride(300f);

        [Tooltip("Distant land fades towards paper tinted with the procedural sky's horizon colour (ChromoSky) instead of bare paper, so far hills melt into the sky at dusk. 0 = bare paper (no change); only while the sky is running.")]
        [ChromoRange(0f, 1f)] public ChromoOverride fogSkyTint = new ChromoOverride(0f);

        [Header("Sky (used by the sky's time-of-day styles, ChromoSky)")]
        [Tooltip("How much ink the sky gets (0 = bare paper, 1 = printed like everything else).")]
        [ChromoRange(0f, 1f)] public ChromoOverride skyInk = new ChromoOverride(1f);

        [Tooltip("Extra saturation for the sky, on top of Saturation.")]
        [ChromoRange(0f, 3f)] public ChromoOverride skySaturation = new ChromoOverride(1.4f);

        [Tooltip("Grain Strength for the sky (each ink's Grain Boost still adds to it). Low keeps the sky a soft wash.")]
        [ChromoRange(0f, 1f)] public ChromoOverride skyGrainStrength = new ChromoOverride(0.2f);

        [Tooltip("Size of the sky's grain relative to everything else. Smaller gives a finer, more detailed sky.")]
        [ChromoRange(0.25f, 4f)] public ChromoOverride skyGrainSize = new ChromoOverride(0.7f);

        [Tooltip("Night sky: a dark sky prints as one smooth wash of the palette's Night Sky ink, with no grain breaking it up, so it isn't blotchy. 0 = off.")]
        [ChromoRange(0f, 1f)] public ChromoOverride skyNightFill = new ChromoOverride(0f);

        [Tooltip("Brightness (0..1) of the sky below which the Night Sky ink is laid.")]
        [ChromoRange(0f, 1f)] public ChromoOverride skyNightBelow = new ChromoOverride(0.15f);

        [Tooltip("How gradually the Night Sky ink comes in above Night Below (in brightness). Large = a soft blend into the printed sky.")]
        [ChromoRange(0.001f, 0.5f)] public ChromoOverride skyNightSoftness = new ChromoOverride(0.15f);

        /// <summary>The base look with this style's ticked settings laid over it.</summary>
        public void Apply(ChromoLook baseLook, ChromoLook into)
        {
            baseLook.Resolve(null, into);
            if (intensity.on) into.intensity = intensity.value;
            if (grainSize.on) into.grainSize = grainSize.value;
            if (grainStrength.on) into.grainStrength = grainStrength.value;
            if (washBlur.on) into.washBlur = washBlur.value;
            if (grainSoftness.on) into.grainSoftness = grainSoftness.value;
            if (registration.on) into.registration = registration.value;
            if (inkVariation.on) into.inkVariation = inkVariation.value;
            if (saturation.on) into.saturation = saturation.value;
            if (contrast.on) into.contrast = contrast.value;
            if (exposure.on) into.exposure = exposure.value;
            if (handColouring.on) into.handColouring = handColouring.value;
            if (evenTexture.on) into.evenTexture = evenTexture.value;
            if (textureDepth.on) into.textureDepth = textureDepth.value;
            if (wetness.on) into.wetness = wetness.value;
            if (bloomSize.on) into.bloomSize = bloomSize.value;
            if (edgePooling.on) into.edgePooling = edgePooling.value;
            if (granulation.on) into.granulation = granulation.value;
            if (paperFibre.on) into.paperFibre = paperFibre.value;
            if (foxing.on) into.foxing = foxing.value;
            if (lineWidth.on) into.lineWidth = lineWidth.value;
            if (lineStrength.on) into.lineStrength = lineStrength.value;
            if (depthSensitivity.on) into.depthSensitivity = depthSensitivity.value;
            if (creaseSensitivity.on) into.creaseSensitivity = creaseSensitivity.value;
            if (lineWobble.on) into.lineWobble = lineWobble.value;
            if (lineHighlightBreak.on) into.lineHighlightBreak = lineHighlightBreak.value;
            if (lineFarDistance.on) into.lineFarDistance = lineFarDistance.value;
            if (colourEdges.on) into.colourEdges = colourEdges.value;
            if (colourSensitivity.on) into.colourSensitivity = colourSensitivity.value;
            if (depthGap.on) into.depthGap = depthGap.value;
            if (hatching.on) into.hatching = hatching.value;
            if (hatchSpacing.on) into.hatchSpacing = hatchSpacing.value;
            if (hatchBelow.on) into.hatchBelow = hatchBelow.value;
            if (shadowFill.on) into.shadowFill = shadowFill.value;
            if (shadowBelow.on) into.shadowBelow = shadowBelow.value;
            if (shadowSoftness.on) into.shadowSoftness = shadowSoftness.value;
            if (distanceFade.on) into.distanceFade = distanceFade.value;
            if (fadeStart.on) into.fadeStart = fadeStart.value;
            if (fadeEnd.on) into.fadeEnd = fadeEnd.value;
            if (fogSkyTint.on) into.fogSkyTint = fogSkyTint.value;
            if (skyInk.on) into.skyInk = skyInk.value;
            if (skySaturation.on) into.skySaturation = skySaturation.value;
            if (skyGrainStrength.on) into.skyGrainStrength = skyGrainStrength.value;
            if (skyGrainSize.on) into.skyGrainSize = skyGrainSize.value;
            if (skyNightFill.on) into.skyNightFill = skyNightFill.value;
            if (skyNightBelow.on) into.skyNightBelow = skyNightBelow.value;
            if (skyNightSoftness.on) into.skyNightSoftness = skyNightSoftness.value;
        }
    }
}
