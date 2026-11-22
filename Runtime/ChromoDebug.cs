using UnityEngine;

namespace Chromolithograph
{
    /// <summary>
    /// What the chromolithograph pass shows, for checking a print the way printers did: progressive proofs (the
    /// stones printed one after another) or a single stone on bare paper. Set from the Chromolithograph debug panel.
    /// </summary>
    public static class ChromoDebug
    {
        public enum View
        {
            Print = 0,
            /// <summary>Stones up to and including Ink, in print order (Ink = ink count means all plus the key).</summary>
            ProgressiveProof = 1,
            /// <summary>Only stone Ink on paper (Ink = ink count means the key stone).</summary>
            SingleStone = 2,
            /// <summary>Ink's coverage before grain, as grey.</summary>
            Coverage = 3,
            /// <summary>The plain frame with no print at all.</summary>
            Off = 4,
            /// <summary>The print, with how corrupted each pixel prints shown as red over grey.</summary>
            Corruption = 5,
        }

        public static View Mode = View.Print;
        public static int Ink;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Mode = View.Print;
            Ink = 0;
        }
    }
}
