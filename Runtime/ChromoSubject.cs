using System.Collections.Generic;
using UnityEngine;

namespace Chromolithograph
{
    /// <summary>
    /// Marks this object (and, by default, its children) as the plate's subject, the way Audubon's birds are printed
    /// in full against a paler, sketchier background. Subjects keep their inks and key lines with distance (the
    /// Look's Subject Hold), while everything else can be made to fade sooner (Background Fade). Characters get one
    /// automatically (Integration/ArtStyle/ChromoMovingAuto). It doesn't change the object's style.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ChromoSubject : MonoBehaviour
    {
        [Tooltip("Mark the renderers on child objects too.")]
        public bool includeChildren = true;

        static readonly List<ChromoSubject> all = new List<ChromoSubject>();
        public static IReadOnlyList<ChromoSubject> All => all;

        readonly List<Renderer> renderers = new List<Renderer>();
        public IReadOnlyList<Renderer> Renderers => renderers;

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

        // Weapons and gear equipped deep in the hierarchy don't raise OnTransformChildrenChanged, so look again now
        // and then.
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
