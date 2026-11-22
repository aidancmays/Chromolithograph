using System.Collections.Generic;
using UnityEngine;

namespace Chromolithograph
{
    /// <summary>
    /// Prints this object (and, by default, its children) in the corrupted style (Corruption Plan X1): its colour
    /// drains into the palette's corruption inks, crumpled-film creases and facets cover it, and the edge of the
    /// corruption feathers into the clean print. Amount can be changed every frame (a spreading blight, a cleansing),
    /// and the edge creeps across the surface as it does. Works with any material and keeps the object's Chromo
    /// Style. The Look's Corruption settings (on the palette, or a volume) set how it looks.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ChromoCorrupted : MonoBehaviour
    {
        [Tooltip("How corrupted it is. 0 = clean, 1 = fully corrupted; in between, corruption covers part of the surface " +
                 "with a feathered edge.")]
        [Range(0f, 1f)] public float amount = 1f;

        [Tooltip("Corrupt the renderers on child objects too.")]
        public bool includeChildren = true;

        static readonly List<ChromoCorrupted> all = new List<ChromoCorrupted>();
        public static IReadOnlyList<ChromoCorrupted> All => all;

        readonly List<Renderer> renderers = new List<Renderer>();
        public IReadOnlyList<Renderer> Renderers => renderers;

        /// <summary>How corrupted it is (0..1). Set it from gameplay to spread or cleanse.</summary>
        public float Amount
        {
            get => amount;
            set => amount = Mathf.Clamp01(value);
        }

        float nextCheck;

        void OnEnable()
        {
            Refresh();
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDisable() => all.Remove(this);

        void OnValidate()
        {
            if (isActiveAndEnabled) Refresh();
        }

        void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) Refresh();
        }

        // Equipment deep in the hierarchy doesn't raise OnTransformChildrenChanged, so look again now and then.
        void LateUpdate()
        {
            if (!includeChildren || !Application.isPlaying || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            Refresh();
        }

        /// <summary>Gathers the renderers again.</summary>
        public void Refresh()
        {
            renderers.Clear();
            if (includeChildren) GetComponentsInChildren(true, renderers);
            else if (TryGetComponent(out Renderer own)) renderers.Add(own);
            renderers.RemoveAll(r => r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer);
        }
    }
}
