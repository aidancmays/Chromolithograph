using System.Collections.Generic;
using UnityEngine;

namespace Chromolithograph
{
    /// <summary>
    /// Gives each ChromoStyle in use a style id (1 to MaxStyles - 1; 0 = the palette's plain Look) that stays the same
    /// while anything holds it, so code that writes ids itself (a shader's "ChromoHints" pass fed by per-material ids,
    /// like the terrain's) can keep them in material properties. ChromoStyled components acquire theirs automatically.
    /// </summary>
    public static class ChromoStyles
    {
        public const int MaxStyles = 16;

        static readonly ChromoStyle[] styles = new ChromoStyle[MaxStyles];
        static readonly int[] holds = new int[MaxStyles];
        static readonly Dictionary<ChromoStyle, int> ids = new Dictionary<ChromoStyle, int>();
        static bool warned;

        /// <summary>Raised when an id is given out or freed, so holders of material properties can refill them.</summary>
        public static event System.Action Changed;

        /// <summary>
        /// Holds an id for this style and returns it (0 when style is null or all ids are taken). Each Acquire needs one
        /// Release.
        /// </summary>
        public static int Acquire(ChromoStyle style)
        {
            if (style == null) return 0;
            if (ids.TryGetValue(style, out int id))
            {
                holds[id]++;
                return id;
            }
            for (id = 1; id < MaxStyles; id++)
            {
                if (styles[id] != null) continue;
                // Free, or held by a style asset that has since been destroyed.
                if (!ReferenceEquals(styles[id], null)) ids.Remove(styles[id]);
                styles[id] = style;
                holds[id] = 1;
                ids[style] = id;
                Changed?.Invoke();
                return id;
            }
            if (!warned) Debug.LogWarning($"Chromolithograph: more than {MaxStyles - 1} styles in use at once; the rest print with the plain Look.");
            warned = true;
            return 0;
        }

        /// <summary>Lets go of one hold on this style's id; the id is freed when nothing holds it.</summary>
        public static void Release(ChromoStyle style)
        {
            if (style == null || !ids.TryGetValue(style, out int id)) return;
            if (--holds[id] > 0) return;
            ids.Remove(style);
            styles[id] = null;
            holds[id] = 0;
            Changed?.Invoke();
        }

        /// <summary>The id this style has now (0 if nothing holds it).</summary>
        public static int IdOf(ChromoStyle style) => style != null && ids.TryGetValue(style, out int id) ? id : 0;

        /// <summary>The style with this id, or null (the plain Look).</summary>
        public static ChromoStyle StyleOf(int id) => id > 0 && id < MaxStyles ? styles[id] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => warned = false;
    }
}
