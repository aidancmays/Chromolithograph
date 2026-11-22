using Chromolithograph;
using UnityEngine;

namespace Chromolithograph.Samples
{
    /// <summary>
    /// Tells the print what the sky looks like each frame, for projects without a sky system of their own: the
    /// horizon and zenith colours (used for sky pixels and the Look's Fog Sky Tint) and the sun direction (from the
    /// scene's sun light). Optional sky print styles can be blended by the sun's height (day above the horizon,
    /// night below). Put it on any object in the scene.
    /// </summary>
    [ExecuteAlways]
    public class ChromoSkyHook : MonoBehaviour
    {
        [Tooltip("The sun; empty = RenderSettings.sun.")]
        public Light sun;
        [ColorUsage(false, true)] public Color horizon = new Color(0.8f, 0.78f, 0.72f);
        [ColorUsage(false, true)] public Color zenith = new Color(0.55f, 0.62f, 0.7f);

        [Header("Optional sky print styles")]
        public ChromoStyle day;
        public ChromoStyle night;
        [Tooltip("Sun height (degrees) over which day blends to night, either side of the horizon.")]
        public float twilightDegrees = 8f;

        void LateUpdate()
        {
            Light light = sun != null ? sun : RenderSettings.sun;
            Vector3 toSun = light != null ? -light.transform.forward : Vector3.up;

            ChromoSky.Horizon = horizon;
            ChromoSky.Zenith = zenith;
            ChromoSky.SunDirection = toSun;
            ChromoSky.Up = Vector3.up;

            float sunHeight = Mathf.Asin(Mathf.Clamp(toSun.y, -1f, 1f)) * Mathf.Rad2Deg;
            ChromoSky.Style = day;
            ChromoSky.StyleTo = night;
            ChromoSky.StyleBlend = night == null ? 0f : Mathf.InverseLerp(twilightDegrees, -twilightDegrees, sunHeight);

            ChromoSky.Frame = Time.frameCount;
        }
    }
}
