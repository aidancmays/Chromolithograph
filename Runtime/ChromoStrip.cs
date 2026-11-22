using UnityEngine;

namespace Chromolithograph
{
    /// <summary>
    /// Stripped paint: a vignette shape in which the print's inks flake away to the bare page (key lines stay). Set
    /// each frame by whoever drives it (the exhaustion vignette); read by ChromolithographFeature.
    /// </summary>
    public static class ChromoStrip
    {
        /// <summary>0 = off, 1 = everything past the fade is bare page.</summary>
        public static float Amount;
        /// <summary>Centre of the clear area, 0-1 across the screen (y up).</summary>
        public static Vector2 Centre = new Vector2(0.5f, 0.5f);
        /// <summary>Scales a 0-1 screen offset into circle space (where radii are in half screen heights).</summary>
        public static Vector2 ToCircle = new Vector2(2f, 2f);
        /// <summary>Radius of the untouched area, in half screen heights.</summary>
        public static float Clear = 1f;
        /// <summary>How far past the clear area the stripping runs before it is complete, in half screen heights.</summary>
        public static float Width = 1f;
        /// <summary>0 = a clean fade, 1 = paint comes away in ragged flakes.</summary>
        public static float Ragged = 1f;

        // The vignette's darkness, printed as a last stone of ink (same centre and circle space as above).
        /// <summary>0 = no painted vignette.</summary>
        public static float PaintOpacity;
        /// <summary>Colour nearest the clear area (linear; alpha = darkest it gets).</summary>
        public static Color PaintInner = Color.black;
        /// <summary>Colour at the edge (linear).</summary>
        public static Color PaintOuter = Color.black;
        /// <summary>Where along the fade (0-1) the colour starts turning to PaintOuter, and how much of it is reached.</summary>
        public static float PaintOuterFrom, PaintOuterAmount;
        /// <summary>Radius of the clear area and width of the fade, in half screen heights.</summary>
        public static float PaintClear = 1f, PaintWidth = 1f;
        /// <summary>How far the wash's edge and the edge colour's border wander, in fade widths.</summary>
        public static float PaintRagged = 0.4f, PaintOuterRagged = 0.6f;
        /// <summary>0 = the edge colour blends in smoothly, 1 = it comes in crisp, jagged patches.</summary>
        public static float PaintOuterHardness = 0.6f;
        /// <summary>How much the grain breaks up the wash (0-1), and how much heavier it lies at its rim.</summary>
        public static float PaintGrain = 0.6f, PaintPooling = 0.35f;
        /// <summary>Size of the blotches the edges wander in (half screen heights), and their drift (0-1, wraps).</summary>
        public static float PaintBlotchSize = 0.12f;
        public static Vector2 PaintDrift;

        /// <summary>The frame the print last ran on a game camera, so drivers know whether to strip or fall back.</summary>
        public static int PrintedFrame = -100;
        public static bool PrintIsOn => Time.frameCount - PrintedFrame <= 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Amount = 0f;
            PaintOpacity = 0f;
            PrintedFrame = -100;
        }
    }
}
