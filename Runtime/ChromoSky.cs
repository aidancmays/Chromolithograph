using UnityEngine;

namespace Chromolithograph
{
    /// <summary>
    /// What the sky looks like this frame, for the print. A sky system (or the Sky Hook sample)
    /// writes it every frame and sets Frame. The print prints sky pixels with Style/StyleTo and GlowStyle, and fades
    /// distant land towards Horizon (Look "Fog Sky Tint").
    /// </summary>
    public static class ChromoSky
    {
        /// <summary>The sky's colour at the horizon (linear).</summary>
        public static Color Horizon = new Color(0.8f, 0.78f, 0.72f);

        /// <summary>The sky's colour overhead (linear).</summary>
        public static Color Zenith = new Color(0.55f, 0.62f, 0.7f);

        /// <summary>Towards the sun, in world space.</summary>
        public static Vector3 SunDirection = Vector3.up;

        /// <summary>
        /// The print style for sky pixels (time of day): the Look with Style's ticked settings, blended toward the Look
        /// with StyleTo's by StyleBlend (0 = Style, 1 = StyleTo). Null = the plain Look. Used only while IsLive.
        /// </summary>
        public static ChromoStyle Style, StyleTo;
        public static float StyleBlend;

        /// <summary>
        /// A style laid around the sun near the horizon (the sunrise and sunset glow), over the sky's Style/StyleTo:
        /// strongest towards the sun, gone GlowWidth degrees away from it along the sky and GlowHeight degrees above
        /// the horizon, scaled by GlowAmount (0 = off). Null = off.
        /// </summary>
        public static ChromoStyle GlowStyle;
        public static float GlowAmount;
        public static float GlowWidth = 70f;
        public static float GlowHeight = 35f;

        /// <summary>Local up at the camera (gravity can tilt it), for the glow's height above the horizon.</summary>
        public static Vector3 Up = Vector3.up;

        /// <summary>Time.frameCount when the sky last wrote these; older means no sky is running.</summary>
        public static int Frame = -1;

        /// <summary>True when the sky wrote these this frame or the one before.</summary>
        public static bool IsLive => Frame >= Time.frameCount - 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Frame = -1;
            Style = StyleTo = null;
            StyleBlend = 0f;
            GlowStyle = null;
            GlowAmount = 0f;
            GlowWidth = 70f;
            GlowHeight = 35f;
            Up = Vector3.up;
        }
    }
}
