using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chromolithograph
{
    /// <summary>
    /// Every setting of the chromolithograph print except the inks, kept on the palette so the look can be tuned with
    /// no volume at all. A Chromolithograph volume override changes only the settings ticked on it, for that area.
    /// </summary>
    [Serializable]
    public class ChromoLook
    {
        [Header("Print")]
        [Tooltip("0 = the plain rendered frame, 1 = fully printed.")]
        [Range(0f, 1f)] public float intensity = 1f;

        [Tooltip("Size of every stone's grain (1 = default).")]
        [Range(0.25f, 4f)] public float grainSize = 1.6f;

        [Tooltip("How much the grain breaks the colour into dots. 0 = smooth washes of colour, 1 = every speck is either ink or bare paper.")]
        [Range(0f, 1f)] public float grainStrength = 0.35f;

        [Tooltip("Blur (pixels at 1080p) on the colour before it is split into inks, so mixed colours run together instead of breaking into blotches. 0 = off.")]
        [Range(0f, 6f)] public float washBlur = 2f;

        [Tooltip("How soft the grain is at high Grain Strength. Low = crisp specks of ink and paper; high = gentle mottling.")]
        [Range(0.02f, 0.6f)] public float grainSoftness = 0.25f;

        [Tooltip("Scales every ink's registration offset (0 = perfect register).")]
        [Range(0f, 4f)] public float registration = 1f;

        [Tooltip("How unevenly the ink takes to the paper: slow blotches of stronger and weaker colour.")]
        [Range(0f, 1f)] public float inkVariation = 0.3f;

        [Header("Colour")]
        [Tooltip("Saturation of the scene's colour before it's split into inks (1 = as rendered).")]
        [Range(0f, 2.5f)] public float saturation = 1.15f;

        [Tooltip("Contrast of the scene's colour before it's split into inks (1 = as rendered). Higher gives deeper shadows and more open lights.")]
        [Range(0.5f, 1.8f)] public float contrast = 1f;

        [Tooltip("Brightness of the scene's colour before it's split into inks, in stops (0 = as rendered). Brighter leaves more bare paper.")]
        [Range(-2f, 2f)] public float exposure = 0f;

        [Tooltip("Pulls the print toward the scene's own colour, like the hand finishing on the Bien plates, so colours the inks can't mix still come through. 0 = inks only.")]
        [Range(0f, 1f)] public float handColouring = 0.5f;

        [Tooltip("Replaces this much of each ink's own grain with one shared texture, so every colour looks equally textured. 0 = each mix shows its own grain.")]
        [Range(0f, 1f)] public float evenTexture = 0.4f;

        [Tooltip("How strong the shared texture from Even Texture is.")]
        [Range(0f, 1f)] public float textureDepth = 0.3f;

        [Header("Wetness")]
        [Tooltip("How much the grain behaves like a wet wash: 0 = fine, dry crayon grain; 1 = soft blooms of pigment.")]
        [Range(0f, 1f)] public float wetness = 0.6f;

        [Tooltip("Size of the wet blooms, as a multiple of the grain size.")]
        [Range(1f, 12f)] public float bloomSize = 4f;

        [Tooltip("How much darker a wash dries at its edges, where the colour changes (needs Wash Blur above 0).")]
        [Range(0f, 1f)] public float edgePooling = 0.35f;

        [Tooltip("How much pigment settles into the paper's tooth, so the fibre shows through the colour.")]
        [Range(0f, 1f)] public float granulation = 0.3f;

        [Header("Paper")]
        [Tooltip("Strength of the paper's fibre texture.")]
        [Range(0f, 1f)] public float paperFibre = 0.5f;

        [Tooltip("Amount of foxing (age spots).")]
        [Range(0f, 1f)] public float foxing = 0.35f;

        [Header("Key lines")]
        [Tooltip("Outline width in pixels at 1080p (thinner with distance). 0 turns outlines off.")]
        [Range(0f, 4f)] public float lineWidth = 1.2f;

        [Tooltip("How dark the outlines print.")]
        [Range(0f, 1f)] public float lineStrength = 0.9f;

        [Tooltip("How big a jump in depth makes an outline. Lower = more lines (and more noise on rough ground).")]
        [Range(0.005f, 1f)] public float depthSensitivity = 0.06f;

        [Tooltip("How sharp a fold makes a crease line (0 = every bend, 1 = only right angles and sharper).")]
        [Range(0.02f, 1f)] public float creaseSensitivity = 0.35f;

        [Tooltip("How much outlines waver, in pixels, so they look drawn on stone.")]
        [Range(0f, 4f)] public float lineWobble = 0.8f;

        [Tooltip("How much bright light breaks up outlines (engravers left highlights open).")]
        [Range(0f, 1f)] public float lineHighlightBreak = 0.6f;

        [Tooltip("Distance in metres at which outlines are thinnest.")]
        [Min(1f)] public float lineFarDistance = 60f;

        [Tooltip("Outlines where one colour meets another, even with no step in depth (a flat stone on grass). 0 = off.")]
        [Range(0f, 1f)] public float colourEdges = 0.6f;

        [Tooltip("How different two colours must be to get an outline. Lower = more lines.")]
        [Range(0.02f, 1f)] public float colourSensitivity = 0.12f;

        [Tooltip("Smallest step in depth, in metres, that gets an outline even when it's tiny next to its distance (edges seen head on).")]
        [Range(0.005f, 1f)] public float depthGap = 0.05f;

        [Tooltip("Outline where objects with different Chromo Styles meet. 0 = off.")]
        [Range(0f, 1f)] public float styleOutlines = 1f;

        [Header("Hatching")]
        [Tooltip("Strength of the key stone's hatching in deep shadow. 0 turns it off.")]
        [Range(0f, 1f)] public float hatching = 0.5f;

        [Tooltip("Space between hatch strokes, in pixels at 1080p.")]
        [Range(2f, 20f)] public float hatchSpacing = 5f;

        [Tooltip("Brightness (0..1) below which shadows start to be hatched; crossed strokes come in at half of it.")]
        [Range(0f, 1f)] public float hatchBelow = 0.3f;

        [Header("Deep shadow")]
        [Tooltip("Deep shadow: the darkest parts of the scene print as solid key ink, so they read as one even black instead of grey and black patches crossed by line work. 0 = off.")]
        [Range(0f, 1f)] public float shadowFill = 1f;

        [Tooltip("Brightness (0..1) below which the scene prints as solid key ink.")]
        [Range(0f, 0.5f)] public float shadowBelow = 0.04f;

        [Tooltip("How gradually the solid ink comes in above Shadow Below (in brightness). Small = a crisp edge.")]
        [Range(0.001f, 0.2f)] public float shadowSoftness = 0.03f;

        [Header("Distance")]
        [Tooltip("How much inks drop out with distance (0 = everything printed in full to the horizon).")]
        [Range(0f, 1f)] public float distanceFade = 1f;

        [Tooltip("Where inks start dropping out with distance, in metres (the palette's fade order sets which go first).")]
        [Min(0f)] public float fadeStart = 35f;

        [Tooltip("Where the last ink is gone and only paper is left, in metres.")]
        [Min(1f)] public float fadeEnd = 300f;

        [Tooltip("Distant land fades towards paper tinted with the procedural sky's horizon colour (ChromoSky) instead of bare paper, so far hills melt into the sky at dusk. 0 = bare paper (no change); only while the sky is running.")]
        [Range(0f, 1f)] public float fogSkyTint = 0f;

        [Header("Subject")]
        [Tooltip("How much the plate's subjects (ChromoSubject: the player and enemies) keep their inks and key lines with distance. 1 = always printed in full, 0 = they fade like everything else.")]
        [Range(0f, 1f)] public float subjectHold = 1f;

        [Tooltip("How much sooner everything that isn't a subject fades, so subjects stand out against a paler background. 0 = the distances above, 1 = four times sooner. Turn it down where threats must stay readable.")]
        [Range(0f, 1f)] public float backgroundFade = 0f;

        [Header("Sky")]
        [Tooltip("How much ink the sky gets (0 = bare paper, 1 = printed like everything else).")]
        [Range(0f, 1f)] public float skyInk = 1f;

        [Tooltip("Extra saturation for the sky, on top of Saturation.")]
        [Range(0f, 3f)] public float skySaturation = 1.4f;

        [Tooltip("Grain Strength for the sky (each ink's Grain Boost still adds to it). Low keeps the sky a soft wash.")]
        [Range(0f, 1f)] public float skyGrainStrength = 0.2f;

        [Tooltip("Size of the sky's grain relative to everything else. Smaller gives a finer, more detailed sky.")]
        [Range(0.25f, 4f)] public float skyGrainSize = 0.7f;

        [Tooltip("Night sky: a dark sky prints as one smooth wash of the palette's Night Sky ink, with no grain breaking it up, so it isn't blotchy. 0 = off.")]
        [Range(0f, 1f)] public float skyNightFill = 0f;

        [Tooltip("Brightness (0..1) of the sky below which the Night Sky ink is laid.")]
        [Range(0f, 1f)] public float skyNightBelow = 0.15f;

        [Tooltip("How gradually the Night Sky ink comes in above Night Below (in brightness). Large = a soft blend into the printed sky.")]
        [Range(0.001f, 0.5f)] public float skyNightSoftness = 0.15f;

        [Header("Corruption")]
        [Tooltip("Corruption: how corrupted objects, materials and regions print (ChromoCorrupted, later the spreading field). How much corrupted surfaces give up their own colour for the palette's corruption inks (Dark, Mid and Light, picked by brightness).")]
        [Range(0f, 1f)] public float corruptDrain = 0.85f;

        [Tooltip("Strength of the crumpled-film facets and creases on corrupted surfaces.")]
        [Range(0f, 1f)] public float corruptCrumple = 0.8f;

        [Tooltip("Size of the crumpled facets on screen, in pixels at 1080p. Like the grain, they keep about this size at any distance (the texture steps by powers of two and blends between steps).")]
        [Range(10f, 600f)] public float corruptCrumplePixels = 120f;

        [Tooltip("How much pigment pools in the creases, darkening them toward the Corruption Dark ink.")]
        [Range(0f, 1f)] public float corruptCreaseDark = 0.7f;

        [Tooltip("How much the creases print as key-stone lines (the angular dark cracks).")]
        [Range(0f, 1f)] public float corruptCreaseLines = 0.5f;

        [Tooltip("How much each facet takes its own tone, so the film reads as separate planes.")]
        [Range(0f, 1f)] public float corruptFacets = 0.4f;

        [Tooltip("How wide the edge of corruption is, where it feathers into the clean print.")]
        [Range(0.01f, 1f)] public float corruptEdge = 0.25f;

        [Tooltip("How much the edge breaks into branching, feathery bleeds instead of a smooth line.")]
        [Range(0f, 1f)] public float corruptBranching = 0.7f;

        [Tooltip("How dark the edge dries, like a wet front of ink soaking into the paper.")]
        [Range(0f, 1f)] public float corruptWetFront = 0.5f;

        [Tooltip("Soft blooms inside corruption: paler pools with hard dark rims.")]
        [Range(0f, 1f)] public float corruptBlooms = 0.5f;

        [Tooltip("Size of the blooms in metres. They live in the world (stuck to surfaces, travelling with moving objects) and slowly drift and change shape.")]
        [Range(0.25f, 40f)] public float corruptBloomSize = 5f;

        [Tooltip("How fast the blooms drift across surfaces, in metres per second. 0 = they stay put.")]
        [Range(0f, 2f)] public float corruptBloomDrift = 0.1f;

        [Tooltip("How fast the blooms change shape. 0 = fixed shapes; 1 = a bloom becomes a different one in about a second.")]
        [Range(0f, 1f)] public float corruptBloomMorph = 0.04f;

        [Tooltip("Strength of a second layer of blooms 2.5 times bigger, for a few large pools among the others. 0 = off.")]
        [Range(0f, 1f)] public float corruptBloomsLarge = 0.6f;

        [Tooltip("Strength of a third layer of blooms 0.4 times the size, for small spots among the others. 0 = off.")]
        [Range(0f, 1f)] public float corruptBloomsSmall = 0.6f;

        [Tooltip("Pale specks in the darks of corruption, like salt dropped into a wet wash.")]
        [Range(0f, 1f)] public float corruptSalt = 0.3f;

        /// <summary>Copies these settings into another, then applies whatever the volume stack overrides.</summary>
        public void Resolve(ChromoVolume v, ChromoLook into)
        {
            into.intensity = intensity;
            into.grainSize = grainSize;
            into.grainStrength = grainStrength;
            into.washBlur = washBlur;
            into.grainSoftness = grainSoftness;
            into.registration = registration;
            into.inkVariation = inkVariation;
            into.saturation = saturation;
            into.contrast = contrast;
            into.exposure = exposure;
            into.handColouring = handColouring;
            into.evenTexture = evenTexture;
            into.textureDepth = textureDepth;
            into.wetness = wetness;
            into.bloomSize = bloomSize;
            into.edgePooling = edgePooling;
            into.granulation = granulation;
            into.paperFibre = paperFibre;
            into.foxing = foxing;
            into.lineWidth = lineWidth;
            into.lineStrength = lineStrength;
            into.depthSensitivity = depthSensitivity;
            into.creaseSensitivity = creaseSensitivity;
            into.lineWobble = lineWobble;
            into.lineHighlightBreak = lineHighlightBreak;
            into.lineFarDistance = lineFarDistance;
            into.colourEdges = colourEdges;
            into.colourSensitivity = colourSensitivity;
            into.depthGap = depthGap;
            into.styleOutlines = styleOutlines;
            into.hatching = hatching;
            into.hatchSpacing = hatchSpacing;
            into.hatchBelow = hatchBelow;
            into.shadowFill = shadowFill;
            into.shadowBelow = shadowBelow;
            into.shadowSoftness = shadowSoftness;
            into.distanceFade = distanceFade;
            into.fadeStart = fadeStart;
            into.fadeEnd = fadeEnd;
            into.fogSkyTint = fogSkyTint;
            into.subjectHold = subjectHold;
            into.backgroundFade = backgroundFade;
            into.skyInk = skyInk;
            into.skySaturation = skySaturation;
            into.skyGrainStrength = skyGrainStrength;
            into.skyGrainSize = skyGrainSize;
            into.skyNightFill = skyNightFill;
            into.skyNightBelow = skyNightBelow;
            into.skyNightSoftness = skyNightSoftness;
            into.corruptDrain = corruptDrain;
            into.corruptCrumple = corruptCrumple;
            into.corruptCrumplePixels = corruptCrumplePixels;
            into.corruptCreaseDark = corruptCreaseDark;
            into.corruptCreaseLines = corruptCreaseLines;
            into.corruptFacets = corruptFacets;
            into.corruptEdge = corruptEdge;
            into.corruptBranching = corruptBranching;
            into.corruptWetFront = corruptWetFront;
            into.corruptBlooms = corruptBlooms;
            into.corruptBloomSize = corruptBloomSize;
            into.corruptBloomDrift = corruptBloomDrift;
            into.corruptBloomMorph = corruptBloomMorph;
            into.corruptBloomsLarge = corruptBloomsLarge;
            into.corruptBloomsSmall = corruptBloomsSmall;
            into.corruptSalt = corruptSalt;
            if (v != null && v.active) into.Override(v);
        }

        void Override(ChromoVolume v)
        {
            intensity = Pick(v.intensity, intensity);
            grainSize = Pick(v.grainSize, grainSize);
            grainStrength = Pick(v.grainStrength, grainStrength);
            washBlur = Pick(v.washBlur, washBlur);
            grainSoftness = Pick(v.grainSoftness, grainSoftness);
            registration = Pick(v.registration, registration);
            inkVariation = Pick(v.inkVariation, inkVariation);
            saturation = Pick(v.saturation, saturation);
            contrast = Pick(v.contrast, contrast);
            exposure = Pick(v.exposure, exposure);
            handColouring = Pick(v.handColouring, handColouring);
            evenTexture = Pick(v.evenTexture, evenTexture);
            textureDepth = Pick(v.textureDepth, textureDepth);
            wetness = Pick(v.wetness, wetness);
            bloomSize = Pick(v.bloomSize, bloomSize);
            edgePooling = Pick(v.edgePooling, edgePooling);
            granulation = Pick(v.granulation, granulation);
            paperFibre = Pick(v.paperFibre, paperFibre);
            foxing = Pick(v.foxing, foxing);
            lineWidth = Pick(v.lineWidth, lineWidth);
            lineStrength = Pick(v.lineStrength, lineStrength);
            depthSensitivity = Pick(v.depthSensitivity, depthSensitivity);
            creaseSensitivity = Pick(v.creaseSensitivity, creaseSensitivity);
            lineWobble = Pick(v.lineWobble, lineWobble);
            lineHighlightBreak = Pick(v.lineHighlightBreak, lineHighlightBreak);
            lineFarDistance = Pick(v.lineFarDistance, lineFarDistance);
            colourEdges = Pick(v.colourEdges, colourEdges);
            colourSensitivity = Pick(v.colourSensitivity, colourSensitivity);
            depthGap = Pick(v.depthGap, depthGap);
            styleOutlines = Pick(v.styleOutlines, styleOutlines);
            hatching = Pick(v.hatching, hatching);
            hatchSpacing = Pick(v.hatchSpacing, hatchSpacing);
            hatchBelow = Pick(v.hatchBelow, hatchBelow);
            shadowFill = Pick(v.shadowFill, shadowFill);
            shadowBelow = Pick(v.shadowBelow, shadowBelow);
            shadowSoftness = Pick(v.shadowSoftness, shadowSoftness);
            distanceFade = Pick(v.distanceFade, distanceFade);
            fadeStart = Pick(v.fadeStart, fadeStart);
            fadeEnd = Pick(v.fadeEnd, fadeEnd);
            fogSkyTint = Pick(v.fogSkyTint, fogSkyTint);
            subjectHold = Pick(v.subjectHold, subjectHold);
            backgroundFade = Pick(v.backgroundFade, backgroundFade);
            skyInk = Pick(v.skyInk, skyInk);
            skySaturation = Pick(v.skySaturation, skySaturation);
            skyGrainStrength = Pick(v.skyGrainStrength, skyGrainStrength);
            skyGrainSize = Pick(v.skyGrainSize, skyGrainSize);
            skyNightFill = Pick(v.skyNightFill, skyNightFill);
            skyNightBelow = Pick(v.skyNightBelow, skyNightBelow);
            skyNightSoftness = Pick(v.skyNightSoftness, skyNightSoftness);
            corruptDrain = Pick(v.corruptDrain, corruptDrain);
            corruptCrumple = Pick(v.corruptCrumple, corruptCrumple);
            corruptCrumplePixels = Pick(v.corruptCrumplePixels, corruptCrumplePixels);
            corruptCreaseDark = Pick(v.corruptCreaseDark, corruptCreaseDark);
            corruptCreaseLines = Pick(v.corruptCreaseLines, corruptCreaseLines);
            corruptFacets = Pick(v.corruptFacets, corruptFacets);
            corruptEdge = Pick(v.corruptEdge, corruptEdge);
            corruptBranching = Pick(v.corruptBranching, corruptBranching);
            corruptWetFront = Pick(v.corruptWetFront, corruptWetFront);
            corruptBlooms = Pick(v.corruptBlooms, corruptBlooms);
            corruptBloomSize = Pick(v.corruptBloomSize, corruptBloomSize);
            corruptBloomDrift = Pick(v.corruptBloomDrift, corruptBloomDrift);
            corruptBloomMorph = Pick(v.corruptBloomMorph, corruptBloomMorph);
            corruptBloomsLarge = Pick(v.corruptBloomsLarge, corruptBloomsLarge);
            corruptBloomsSmall = Pick(v.corruptBloomsSmall, corruptBloomsSmall);
            corruptSalt = Pick(v.corruptSalt, corruptSalt);
        }

        static float Pick(VolumeParameter<float> parameter, float own) => parameter.overrideState ? parameter.value : own;
    }
}
