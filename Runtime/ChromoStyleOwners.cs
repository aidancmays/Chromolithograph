using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chromolithograph
{
    /// <summary>
    /// Who decides a renderer's print style (see "Style ownership" in the Chromolithograph plan). Each renderer has one
    /// owner, the first of:
    /// 1. A ChromoStyled on the renderer or above it: one style for the whole renderer.
    /// 2. Its shader's own "ChromoHints" pass: a style per pixel (the terrain per material, foliage per type).
    /// 3. Neither: the palette's plain Look (id 0, nothing is drawn).
    /// Both used to draw, and the ChromoStyled draw covered every pixel the shader's pass had written (it is drawn
    /// later, with no culling), so the shader's draw was wasted: for the terrain, a second draw of every chunk. Now,
    /// while a ChromoStyled owns a renderer whose material has that pass, the material's pass is switched off, so the
    /// style buffer comes out the same with one draw.
    /// Only materials made at runtime and never saved (the terrain's) are switched; a saved material is left alone, so
    /// nothing changes in assets, and it keeps both draws (same result, slower). A material switched off is off for
    /// every renderer using it, so a runtime material shared with renderers outside the ChromoStyled loses its
    /// per-pixel styles there while it is owned.
    /// </summary>
    public static class ChromoStyleOwners
    {
        public const string HintsLightMode = "ChromoHints";

        static readonly ShaderTagId LightMode = new ShaderTagId("LightMode");
        static readonly ShaderTagId Hints = new ShaderTagId(HintsLightMode);
        static readonly Dictionary<Shader, bool> hasPass = new Dictionary<Shader, bool>();
        // Whether a material can be switched (made at runtime, shader has the pass), asked thousands of times a frame:
        // forgotten now and then in case a material's shader is swapped.
        static readonly Dictionary<Material, bool> switchable = new Dictionary<Material, bool>();
        const int SwitchableForgetFrames = 300;
        static readonly HashSet<Material> off = new HashSet<Material>();
        // The frame each switched material was last owned. In play the feature checks each owned renderer only every
        // so often (a sweep), so a material keeps its pass off until it has gone unowned for KeepFrames.
        static readonly Dictionary<Material, int> lastSeen = new Dictionary<Material, int>();
        const int KeepFrames = 150;
        static readonly List<Material> stale = new List<Material>();
        static int frame = -1;

        static ChromoStyleOwners()
        {
#if UNITY_EDITOR
            // Statics are lost on a script reload but the materials keep their switch, so put them back first.
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += RestoreAll;
#endif
        }

        /// <summary>The number of materials whose ChromoHints pass is switched off now.</summary>
        public static int SwitchedOff => off.Count;

        /// <summary>True when this shader draws its own styles (it has a pass with LightMode ChromoHints).</summary>
        public static bool HasHintsPass(Shader shader)
        {
            if (shader == null) return false;
            if (hasPass.TryGetValue(shader, out bool has)) return has;
            for (int i = 0; i < shader.passCount && !has; i++) has = shader.FindPassTagValue(i, LightMode) == Hints;
            hasPass[shader] = has;
            return has;
        }

        /// <summary>
        /// Starts a frame: materials not owned for KeepFrames get their pass back. Call before any OwnedBy this
        /// frame; further calls in the same frame (more cameras) do nothing.
        /// </summary>
        public static void BeginFrame()
        {
            int now = Time.frameCount;
            if (now == frame) return;
            frame = now;
            if (now % SwitchableForgetFrames == 0) switchable.Clear();
            stale.Clear();
            foreach (Material m in off)
            {
                if (!lastSeen.TryGetValue(m, out int at) || now - at > KeepFrames) stale.Add(m);
            }
            foreach (Material m in stale)
            {
                off.Remove(m);
                lastSeen.Remove(m);
                if (m != null) m.SetShaderPassEnabled(HintsLightMode, true);
            }
            stale.Clear();
        }

        /// <summary>A renderer a ChromoStyled owns draws with this material (checked this frame).</summary>
        public static void OwnedBy(Material material)
        {
            if (material is null) return;
            if (!switchable.TryGetValue(material, out bool canSwitch))
            {
                canSwitch = material != null && (material.hideFlags & HideFlags.DontSaveInEditor) != 0 && HasHintsPass(material.shader);
                switchable[material] = canSwitch;
            }
            if (!canSwitch || !material) return;
            lastSeen[material] = frame;
            if (off.Add(material)) material.SetShaderPassEnabled(HintsLightMode, false);
        }

        /// <summary>Gives every switched material its pass back (the feature going away, scripts reloading).</summary>
        public static void RestoreAll()
        {
            foreach (Material m in off)
            {
                if (m != null) m.SetShaderPassEnabled(HintsLightMode, true);
            }
            off.Clear();
            lastSeen.Clear();
            frame = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            RestoreAll();
            hasPass.Clear();
            switchable.Clear();
        }
    }
}
