using Chromolithograph;
using UnityEngine;

namespace Chromolithograph.Samples
{
    /// <summary>
    /// Gives the "Chromolithograph Samples/Sway" shader its two print style ids. Ids come from ChromoStyles and are
    /// held while this is enabled; they can change when other styles come and go, so they are refilled on Changed.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class ChromoSampleStyles : MonoBehaviour
    {
        public ChromoStyle below;
        public ChromoStyle above;

        static readonly int StyleLow = Shader.PropertyToID("_StyleLow");
        static readonly int StyleHigh = Shader.PropertyToID("_StyleHigh");

        MaterialPropertyBlock block;
        ChromoStyle heldBelow, heldAbove;

        void OnEnable()
        {
            Hold();
            ChromoStyles.Changed += Fill;
            Fill();
        }

        void OnDisable()
        {
            ChromoStyles.Changed -= Fill;
            Let();
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            Let();
            Hold();
            Fill();
        }

        void Hold()
        {
            heldBelow = below;
            heldAbove = above;
            if (heldBelow != null) ChromoStyles.Acquire(heldBelow);
            if (heldAbove != null) ChromoStyles.Acquire(heldAbove);
        }

        void Let()
        {
            if (heldBelow != null) ChromoStyles.Release(heldBelow);
            if (heldAbove != null) ChromoStyles.Release(heldAbove);
            heldBelow = heldAbove = null;
        }

        void Fill()
        {
            var r = GetComponent<Renderer>();
            if (r == null) return;
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetFloat(StyleLow, ChromoStyles.IdOf(heldBelow));
            block.SetFloat(StyleHigh, ChromoStyles.IdOf(heldAbove));
            r.SetPropertyBlock(block);
        }
    }
}
