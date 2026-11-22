using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Chromolithograph
{
    /// <summary>
    /// Debug panel (F8 by default): turns the print on and off and steps through the proofs. Created by itself in
    /// play mode in the editor and in development builds; nothing to place.
    ///
    /// A project with its own debug overlay can set <see cref="AutoSpawn"/> to false (from a
    /// RuntimeInitializeOnLoadMethod with SubsystemRegistration) and call <see cref="DrawControls"/> from its own panel.
    /// </summary>
    [AddComponentMenu("")]
    public class ChromoDebugPanel : MonoBehaviour
    {
        /// <summary>False stops the panel creating itself (set it before the first scene loads).</summary>
        public static bool AutoSpawn = true;

#if ENABLE_INPUT_SYSTEM
        [SerializeField] Key toggleKey = Key.F8;
#else
        [SerializeField] KeyCode toggleKey = KeyCode.F8;
#endif
        [SerializeField] float width = 300f;

        public bool Visible { get; set; }

        Rect window;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            AutoSpawn = true;
            cachedPalette = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (!AutoSpawn || !Debug.isDebugBuild || FindAnyObjectByType<ChromoDebugPanel>() != null) return;
            var go = new GameObject("Chromolithograph Debug");
            DontDestroyOnLoad(go);
            go.AddComponent<ChromoDebugPanel>().Visible = false;
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) Visible = !Visible;
#else
            if (Input.GetKeyDown(toggleKey)) Visible = !Visible;
#endif
        }

        void OnGUI()
        {
            if (!Visible) return;
            if (window.width <= 0f) window = new Rect(Screen.width - width - 10f, 10f, width, 10f);
            window.x = Mathf.Clamp(window.x, 0f, Mathf.Max(0f, Screen.width - window.width));
            window = GUILayout.Window(GetInstanceID(), window, id =>
            {
                DrawControls();
                GUI.DragWindow();
            }, "Print", GUILayout.Width(width));
        }

        /// <summary>The panel's contents (IMGUI layout), for drawing inside another debug overlay.</summary>
        public static void DrawControls()
        {
            ChromoPalette palette = FindPalette();
            int inkCount = palette != null ? palette.PrintedInkCount : 0;

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(ChromoDebug.Mode == ChromoDebug.View.Print, "Print", "Button")) ChromoDebug.Mode = ChromoDebug.View.Print;
            if (GUILayout.Toggle(ChromoDebug.Mode == ChromoDebug.View.Off, "Off", "Button")) ChromoDebug.Mode = ChromoDebug.View.Off;
            if (GUILayout.Toggle(ChromoDebug.Mode == ChromoDebug.View.Corruption, "Corruption", "Button")) ChromoDebug.Mode = ChromoDebug.View.Corruption;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(ChromoDebug.Mode == ChromoDebug.View.ProgressiveProof, "Proofs", "Button")) ChromoDebug.Mode = ChromoDebug.View.ProgressiveProof;
            if (GUILayout.Toggle(ChromoDebug.Mode == ChromoDebug.View.SingleStone, "One stone", "Button")) ChromoDebug.Mode = ChromoDebug.View.SingleStone;
            if (GUILayout.Toggle(ChromoDebug.Mode == ChromoDebug.View.Coverage, "Coverage", "Button")) ChromoDebug.Mode = ChromoDebug.View.Coverage;
            GUILayout.EndHorizontal();

            bool stepping = ChromoDebug.Mode == ChromoDebug.View.ProgressiveProof || ChromoDebug.Mode == ChromoDebug.View.SingleStone || ChromoDebug.Mode == ChromoDebug.View.Coverage;
            if (stepping)
            {
                int last = ChromoDebug.Mode == ChromoDebug.View.Coverage ? inkCount - 1 : inkCount;
                ChromoDebug.Ink = Mathf.Clamp(ChromoDebug.Ink, 0, Mathf.Max(0, last));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", GUILayout.Width(30))) ChromoDebug.Ink = Mathf.Max(0, ChromoDebug.Ink - 1);
                GUILayout.Label(StoneName(palette, ChromoDebug.Ink, inkCount), GUILayout.ExpandWidth(true));
                if (GUILayout.Button(">", GUILayout.Width(30))) ChromoDebug.Ink = Mathf.Min(last, ChromoDebug.Ink + 1);
                GUILayout.EndHorizontal();
            }

            if (palette == null) GUILayout.Label("No palette found on the renderer.");
            else if (palette.NeedsBake) GUILayout.Label($"'{palette.name}' has changed since it was baked.");
        }

        static string StoneName(ChromoPalette palette, int ink, int inkCount)
        {
            if (palette == null) return "-";
            if (ink >= inkCount) return $"{inkCount + 1}. Key stone";
            return $"{ink + 1}. {palette.inks[ink].name}";
        }

        static ChromoPalette cachedPalette;

        static ChromoPalette FindPalette()
        {
            if (cachedPalette != null) return cachedPalette;
            foreach (ChromoPalette p in Resources.FindObjectsOfTypeAll<ChromoPalette>())
            {
                cachedPalette = p;
                break;
            }
            return cachedPalette;
        }
    }
}
