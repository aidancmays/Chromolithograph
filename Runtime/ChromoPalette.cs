using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chromolithograph
{
    /// <summary>
    /// The inks, paper and key stone a chromolithograph is printed with, plus the tables baked from them: the colour
    /// separation (a 3D table giving each ink's coverage for any colour) and the grain and paper textures. The baked
    /// textures are sub-assets; press Bake in the inspector after changing the inks (the renderer feature keeps
    /// printing the old bake until then, and says so in the inspector).
    ///
    /// Inks are listed in print order: pale tint stones first, the darkest last. The key stone (outlines and
    /// hatching) is printed after all of them.
    /// </summary>
    [CreateAssetMenu(menuName = "Chromolithograph/Palette", fileName = "Chromolithograph Palette")]
    public class ChromoPalette : ScriptableObject
    {
        public const int MaxInks = 8;
        public const int SeparationSize = 32;
        public const int GrainSize = 256;
        public const int PaperSize = 512;

        [Serializable]
        public class Ink
        {
            public string name = "Ink";

            [Tooltip("The ink's colour as printed solid on white. Inks multiply what is under them, like real transparent litho inks.")]
            public Color color = Color.gray;

            [Tooltip("How strongly a solid patch of this ink prints (1 = its full colour).")]
            [Range(0f, 1f)] public float density = 1f;

            [Tooltip("How this stone's ink breaks up: Crayon (grained stone, most tones), Stipple (fine even dots), Ruled (wavering lines, for skies and water).")]
            public ChromoGrain.Style grain = ChromoGrain.Style.Crayon;

            [Tooltip("Size of this stone's grain (1 = the default size).")]
            [Range(0.25f, 4f)] public float grainScale = 1f;

            [Tooltip("Added to the volume's Grain Strength for this stone only. Raise it on tints so their ruled lines or stipple show through as texture; lower it for inks that should lie as smooth washes. Takes effect without baking.")]
            [Range(-1f, 1f)] public float grainBoost;

            [Tooltip("When this ink drops out with distance: 0 = first (it's gone from the middle distance), 1 = last (it reaches the horizon). Pale tints usually last.")]
            [Range(0f, 1f)] public float fadeOrder = 0.5f;

            [Tooltip("How far this stone sits out of register, in pixels at 1080p. A pixel or two reads as printed; more looks careless.")]
            public Vector2 registration;

            [Tooltip("How reluctant the separation is to use this ink (1 = normal). Raise it for accent colours that should only appear where nothing else will do.")]
            [Range(0.1f, 5f)] public float cost = 1f;
        }

        [Header("Paper")]
        [Tooltip("Bare paper. Everything the inks don't cover shows this, and the frame's whites become it.")]
        public Color paper = new Color(0.94f, 0.90f, 0.81f);

        [Tooltip("Colour of foxing spots (age stains).")]
        public Color foxing = new Color(0.78f, 0.62f, 0.45f);

        [Header("Inks")]
        public List<Ink> inks = new List<Ink>();

        [Tooltip("The key stone: outlines and hatching, printed last.")]
        public Color key = new Color(0.16f, 0.12f, 0.10f);

        [Tooltip("The ink deep shadows are printed solid with (the Look's Deep Shadow settings), separate from the key " +
                 "stone's lines and hatching.")]
        public Color shadow = new Color(0.11f, 0.10f, 0.12f);

        [Tooltip("The ink a dark night sky is printed with, as one smooth wash (the Look's Night Sky settings).")]
        public Color nightSky = new Color(0.08f, 0.09f, 0.16f);

        [Tooltip("Corruption's inks (the Look's Corruption settings): a corrupted surface's colour is remapped through them by " +
                 "brightness, darks to Dark, middle tones to Mid, lights to Light. Dark also pools in the creases.")]
        public Color corruptDark = new Color(0.10f, 0.10f, 0.22f);
        public Color corruptMid = new Color(0.46f, 0.11f, 0.12f);
        public Color corruptLight = new Color(0.58f, 0.60f, 0.64f);

        [Tooltip("The colour inside corruption's blooms (the Look's Corrupt Blooms), laid over the corrupted surface like a " +
                 "paler pool of pigment. White = bare paper showing through.")]
        public Color corruptBloom = new Color(1f, 1f, 1f);

        [Tooltip("The colour of the hard rim the blooms dry to.")]
        public Color corruptBloomRim = new Color(0.10f, 0.10f, 0.22f);

        [Tooltip("Every setting of the print besides the inks: grain, colour, paper, lines, hatching, distance and sky. Changes show straight away, no bake needed. A Chromolithograph volume override can change any of them for an area.")]
        public ChromoLook look = new ChromoLook();

        [Header("Separation")]
        [Tooltip("How much the separation prefers fewer inks over a closer colour. Higher = flatter, cleaner colour areas and less speckle, but colours drift further from the scene's.")]
        [Range(0.0005f, 0.03f)] public float inkCost = 0.004f;

        [Tooltip("How much neighbouring colours are made to share an ink mix. Higher stops colours flickering between two mixes as the light shifts, at a small cost in colour accuracy. Bake after changing.")]
        [Range(0f, 1f)] public float mixSmoothness = 0.2f;

        [Tooltip("Seed for the grain and paper textures.")]
        public int seed = 1858;

        [SerializeField, HideInInspector] Texture3D separationA;
        [SerializeField, HideInInspector] Texture3D separationB;
        [SerializeField, HideInInspector] Texture2D grainA;
        [SerializeField, HideInInspector] Texture2D grainB;
        [SerializeField, HideInInspector] Texture2D paperTexture;
        [SerializeField, HideInInspector] string bakedSignature;
        [SerializeField, HideInInspector] int bakedInkCount;

        public Texture3D SeparationA => separationA;
        public Texture3D SeparationB => separationB;
        public Texture2D GrainA => grainA;
        public Texture2D GrainB => grainB;
        public Texture2D PaperTexture => paperTexture;

        /// <summary>Inks the shader prints: the baked count, so a half-edited list never mismatches the tables.</summary>
        public int PrintedInkCount => Mathf.Min(bakedInkCount, inks.Count, MaxInks);

        public bool IsBaked => separationA != null && separationB != null && grainA != null && grainB != null && paperTexture != null && bakedInkCount > 0;

        /// <summary>True when the inks or paper changed since the last bake.</summary>
        public bool NeedsBake => !IsBaked || bakedSignature != Signature();

        /// <summary>Everything the bake depends on, as text, to tell when it's stale.</summary>
        public string Signature()
        {
            var text = new System.Text.StringBuilder();
            text.Append("mips|").Append(paper).Append(inkCost).Append(mixSmoothness).Append(seed).Append('|');
            int count = Mathf.Min(inks.Count, MaxInks);
            for (int i = 0; i < count; i++)
            {
                Ink ink = inks[i];
                text.Append(ink.color).Append(ink.cost).Append(ink.grain).Append(';');
            }
            return text.ToString();
        }

        public ChromoSeparation.Settings SeparationSettings()
        {
            int count = Mathf.Min(inks.Count, MaxInks);
            var settings = new ChromoSeparation.Settings
            {
                Inks = new float[count][],
                Costs = new float[count],
                Paper = Linear(paper),
                InkCost = inkCost,
                Size = SeparationSize,
                Iterations = 120,
                Smoothness = mixSmoothness,
                SmoothPasses = 20,
            };
            for (int i = 0; i < count; i++)
            {
                settings.Inks[i] = Linear(inks[i].color);
                settings.Costs[i] = inks[i].cost;
            }
            return settings;
        }

        static float[] Linear(Color c)
        {
            Color l = c.linear;
            return new[] { l.r, l.g, l.b };
        }

#if UNITY_EDITOR
        /// <summary>Called by the editor bake. Reuses existing sub-asset textures where it can, so references hold.</summary>
        public void SetBaked(Texture3D a, Texture3D b, Texture2D grain0, Texture2D grain1, Texture2D paperTex, int inkCount)
        {
            separationA = a;
            separationB = b;
            grainA = grain0;
            grainB = grain1;
            paperTexture = paperTex;
            bakedInkCount = inkCount;
            bakedSignature = Signature();
        }
#endif

        /// <summary>The Bien edition's look: buff and grey-blue tint stones, chrome yellow, vermilion, Prussian blue, sap green, raw umber and a warm black.</summary>
        public void ResetToAudubon()
        {
            paper = new Color(0.94f, 0.90f, 0.81f);
            foxing = new Color(0.78f, 0.62f, 0.45f);
            key = new Color(0.16f, 0.12f, 0.10f);
            shadow = new Color(0.11f, 0.10f, 0.12f);
            nightSky = new Color(0.08f, 0.09f, 0.16f);
            corruptDark = new Color(0.10f, 0.10f, 0.22f);
            corruptMid = new Color(0.46f, 0.11f, 0.12f);
            corruptLight = new Color(0.58f, 0.60f, 0.64f);
            corruptBloom = new Color(1f, 1f, 1f);
            corruptBloomRim = new Color(0.10f, 0.10f, 0.22f);
            inkCost = 0.004f;
            inks = new List<Ink>
            {
                new Ink { name = "Buff tint", color = new Color(0.90f, 0.80f, 0.62f), grain = ChromoGrain.Style.Stipple, grainScale = 1.2f, grainBoost = 0.25f, fadeOrder = 1f, registration = new Vector2(0.6f, -0.4f), cost = 0.6f },
                new Ink { name = "Grey-blue tint", color = new Color(0.74f, 0.82f, 0.86f), grain = ChromoGrain.Style.Ruled, grainScale = 1f, grainBoost = 0.55f, fadeOrder = 0.9f, registration = new Vector2(-0.8f, 0.5f), cost = 0.6f },
                new Ink { name = "Chrome yellow", color = new Color(0.95f, 0.80f, 0.25f), grain = ChromoGrain.Style.Crayon, grainScale = 1f, fadeOrder = 0.55f, registration = new Vector2(1.2f, 0.7f) },
                new Ink { name = "Vermilion", color = new Color(0.82f, 0.30f, 0.18f), grain = ChromoGrain.Style.Crayon, grainScale = 0.9f, fadeOrder = 0.2f, registration = new Vector2(-1.0f, -0.9f), cost = 1.4f },
                new Ink { name = "Prussian blue", color = new Color(0.20f, 0.36f, 0.55f), grain = ChromoGrain.Style.Crayon, grainScale = 1.1f, fadeOrder = 0.6f, registration = new Vector2(0.4f, 1.3f) },
                new Ink { name = "Sap green", color = new Color(0.42f, 0.55f, 0.24f), grain = ChromoGrain.Style.Crayon, grainScale = 1.3f, fadeOrder = 0.45f, registration = new Vector2(-1.3f, 0.2f) },
                new Ink { name = "Raw umber", color = new Color(0.45f, 0.32f, 0.21f), grain = ChromoGrain.Style.Crayon, grainScale = 1f, fadeOrder = 0.3f, registration = new Vector2(0.9f, -1.2f) },
                new Ink { name = "Warm black", color = new Color(0.16f, 0.13f, 0.12f), grain = ChromoGrain.Style.Stipple, grainScale = 0.8f, fadeOrder = 0.1f, registration = new Vector2(-0.3f, -0.6f), cost = 1.2f },
            };
        }

        void Reset() => ResetToAudubon();
    }
}
