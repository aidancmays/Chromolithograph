using System.Collections.Generic;
using UnityEngine;

namespace Chromolithograph
{
    /// <summary>
    /// Prints this object (and, by default, its children) with a ChromoStyle instead of the palette's plain Look.
    /// Works with any material: the Chromolithograph feature draws the object's shape into a style buffer each frame.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ChromoStyled : MonoBehaviour
    {
        [Tooltip("The style these objects print with.")]
        public ChromoStyle style;

        [Tooltip("Style the renderers on child objects too.")]
        public bool includeChildren = true;

        static readonly List<ChromoStyled> all = new List<ChromoStyled>();
        public static IReadOnlyList<ChromoStyled> All => all;

        // Children added in bursts (the terrain streams in whole chunks) are gathered once, just before the style
        // feature reads them, instead of re-scanning the object's whole hierarchy for every child: that scan is
        // milliseconds each time.
        static readonly List<ChromoStyled> pending = new List<ChromoStyled>();
        bool queued;

        readonly List<Renderer> renderers = new List<Renderer>();
        public IReadOnlyList<Renderer> Renderers => renderers;

        // Each renderer's GameObject, same order: the feature checks thousands of renderers a frame, and asking each
        // for its object is a native call.
        readonly List<GameObject> rendererObjects = new List<GameObject>();
        public IReadOnlyList<GameObject> RendererObjects => rendererObjects;

        ChromoStyle held;

        // In play, each renderer carries its style as a rendering layer bit (16 + id), so the feature draws all of one
        // style with a single culled renderer list (LOD and visibility respected) instead of one draw per renderer.
        // Bits 0 to 7 are the light layers; 16 to 31 are otherwise unused. Not in edit mode, where it would be saved.
        uint appliedBit;
        static readonly HashSet<Renderer> kept = new HashSet<Renderer>();

        /// <summary>Where the feature's ownership sweep (ChromoStyleOwners) is up to in Renderers.</summary>
        internal int sweepCursor;

        /// <summary>The rendering layer bit that marks renderers of this style id (0 for the plain Look).</summary>
        public static uint LayerBit(int id) => id > 0 && id < ChromoStyles.MaxStyles ? 1u << (16 + id) : 0u;

        /// <summary>This object's style id in the style buffer (0 = plain Look), following changes to style.</summary>
        public int StyleId
        {
            get
            {
                if (held != style)
                {
                    ChromoStyles.Release(held);
                    held = style;
                    ChromoStyles.Acquire(held);
                }
                int id = ChromoStyles.IdOf(held);
                uint bit = Application.isPlaying ? LayerBit(id) : 0u;
                if (bit != appliedBit) ApplyBit(bit);
                return id;
            }
        }

        void ApplyBit(uint bit)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer r = renderers[i];
                if (r != null) r.renderingLayerMask = (r.renderingLayerMask & ~appliedBit) | bit;
            }
            appliedBit = bit;
        }

        void OnEnable()
        {
            Refresh();
            if (!all.Contains(this)) all.Add(this);
            held = style;
            ChromoStyles.Acquire(held);
        }

        void OnDisable()
        {
            all.Remove(this);
            if (appliedBit != 0) ApplyBit(0);
            ChromoStyles.Release(held);
            held = null;
            queued = false;
        }

        void OnValidate()
        {
            if (isActiveAndEnabled) Refresh();
        }

        void OnTransformChildrenChanged()
        {
            if (!isActiveAndEnabled) return;
            if (!Application.isPlaying)
            {
                Refresh(); // authoring: keep the inspector's renderer list straight away
                return;
            }
            if (queued) return;
            queued = true;
            pending.Add(this);
        }

        /// <summary>Gathers the queued objects' renderers now (the Chromolithograph feature calls this before it draws them).</summary>
        public static void FlushPending()
        {
            if (pending.Count == 0) return;
            for (int i = 0; i < pending.Count; i++)
            {
                ChromoStyled styled = pending[i];
                if (styled == null) continue;
                styled.queued = false;
                if (styled.isActiveAndEnabled) styled.Refresh();
            }
            pending.Clear();
        }

        /// <summary>Gathers the renderers again (call after adding or removing child meshes at runtime).</summary>
        public void Refresh()
        {
            // Renderers that have left keep no style bit; ones that stay or arrive get it below.
            if (appliedBit != 0)
            {
                kept.Clear();
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i] != null) kept.Add(renderers[i]);
                }
            }
            renderers.Clear();
            // Inactive children too (pooled or switched off for now): the feature skips them while they're inactive,
            // and they keep their style when they come back.
            if (includeChildren) GetComponentsInChildren(true, renderers);
            else if (TryGetComponent(out Renderer own)) renderers.Add(own);
            // Particles, trails and lines have no solid surface to style.
            renderers.RemoveAll(r => r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer);
            rendererObjects.Clear();
            for (int i = 0; i < renderers.Count; i++) rendererObjects.Add(renderers[i].gameObject);
            if (sweepCursor >= renderers.Count) sweepCursor = 0;
            if (appliedBit == 0) return;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer r = renderers[i];
                if (!kept.Remove(r) || (r.renderingLayerMask & appliedBit) == 0) r.renderingLayerMask |= appliedBit;
            }
            foreach (Renderer gone in kept)
            {
                if (gone != null) gone.renderingLayerMask &= ~appliedBit;
            }
            kept.Clear();
        }
    }
}
