using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Chromolithograph.Editor
{
    [CustomEditor(typeof(ChromoPalette))]
    public class ChromoPaletteEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var palette = (ChromoPalette)target;

            DrawSwatches(palette);
            if (palette.inks.Count > ChromoPalette.MaxInks)
                EditorGUILayout.HelpBox($"Only the first {ChromoPalette.MaxInks} inks are printed.", MessageType.Warning);
            if (!palette.IsBaked)
                EditorGUILayout.HelpBox("Not baked yet: nothing is printed until it is.", MessageType.Warning);
            else if (palette.NeedsBake)
                EditorGUILayout.HelpBox("Inks or paper changed since the last bake; the game still prints the old bake.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(palette.NeedsBake ? "Bake" : "Bake again", GUILayout.Height(24))) ChromoBake.Bake(palette);
            if (GUILayout.Button("Reset to Audubon", GUILayout.Height(24)) &&
                EditorUtility.DisplayDialog("Reset palette", "Replace every ink with the Audubon (Bien edition) set?", "Reset", "Cancel"))
            {
                Undo.RecordObject(palette, "Reset palette");
                palette.ResetToAudubon();
                EditorUtility.SetDirty(palette);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            DrawDefaultInspector();
        }

        static void DrawSwatches(ChromoPalette palette)
        {
            Rect row = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
            int count = Mathf.Min(palette.inks.Count, ChromoPalette.MaxInks) + 2;
            float width = row.width / count;
            Rect cell = new Rect(row.x, row.y, width - 2f, row.height);
            EditorGUI.DrawRect(cell, palette.paper);
            for (int i = 0; i < count - 2; i++)
            {
                cell.x += width;
                EditorGUI.DrawRect(cell, palette.paper);
                Rect ink = new Rect(cell.x + 3f, cell.y + 3f, cell.width - 6f, cell.height - 6f);
                EditorGUI.DrawRect(ink, palette.inks[i].color);
            }
            cell.x += width;
            EditorGUI.DrawRect(cell, palette.paper);
            EditorGUI.DrawRect(new Rect(cell.x + 3f, cell.y + 3f, cell.width - 6f, cell.height - 6f), palette.key);
        }
    }

    /// <summary>Bakes a palette's separation table and textures, and sets the print up on the renderer.</summary>
    public static class ChromoBake
    {
        const string DefaultFolder = "Assets/Chromolithograph";

        public static void Bake(ChromoPalette palette)
        {
            ChromoSeparation.Settings settings = palette.SeparationSettings();
            int inkCount = settings.Inks.Length;
            if (inkCount == 0)
            {
                Debug.LogWarning($"Chromolithograph: '{palette.name}' has no inks to bake.");
                return;
            }

            float[] coverage;
            try
            {
                EditorUtility.DisplayProgressBar("Chromolithograph", $"Separating colours into {inkCount} inks...", 0.1f);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                coverage = ChromoSeparation.Solve(settings);

                EditorUtility.DisplayProgressBar("Chromolithograph", "Making grain and paper...", 0.7f);
                int n = ChromoPalette.SeparationSize;
                Texture3D sepA = Separation(palette, "Separation A", coverage, inkCount, 0, n);
                Texture3D sepB = Separation(palette, "Separation B", coverage, inkCount, 4, n);
                Texture2D grainA = Grain(palette, "Grain A", 0);
                Texture2D grainB = Grain(palette, "Grain B", 4);
                Texture2D paper = Paper(palette);

                Undo.RecordObject(palette, "Bake palette");
                palette.SetBaked(sepA, sepB, grainA, grainB, paper, inkCount);
                EditorUtility.SetDirty(palette);
                AssetDatabase.SaveAssets();
                Debug.Log($"Chromolithograph: baked '{palette.name}' ({inkCount} inks) in {timer.ElapsedMilliseconds} ms. {SeparationReport(settings, coverage)}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>How close the inks come to the colours they replace, so a palette that can't make greens (say) shows up.</summary>
        static string SeparationReport(ChromoSeparation.Settings settings, float[] coverage)
        {
            int n = settings.Size, inkCount = settings.Inks.Length;
            double total = 0, worst = 0;
            float[] c = new float[inkCount];
            int worstIndex = 0;
            for (int i = 0; i < n * n * n; i++)
            {
                Array.Copy(coverage, i * inkCount, c, 0, inkCount);
                float[] print = ChromoSeparation.Print(settings, c);
                int r = i % n, g = (i / n) % n, b = i / (n * n);
                float step = 1f / (n - 1);
                float[] a = ChromoSeparation.OkLab(print[0], print[1], print[2]);
                float[] t = ChromoSeparation.OkLab(
                    ChromoSeparation.SrgbToLinear(r * step) * settings.Paper[0],
                    ChromoSeparation.SrgbToLinear(g * step) * settings.Paper[1],
                    ChromoSeparation.SrgbToLinear(b * step) * settings.Paper[2]);
                double e = Math.Sqrt((a[0] - t[0]) * (a[0] - t[0]) + (a[1] - t[1]) * (a[1] - t[1]) + (a[2] - t[2]) * (a[2] - t[2]));
                total += e;
                if (e > worst) { worst = e; worstIndex = i; }
            }
            int wr = worstIndex % n, wg = (worstIndex / n) % n, wb = worstIndex / (n * n);
            var worstColor = new Color(wr / (n - 1f), wg / (n - 1f), wb / (n - 1f));
            return $"Average colour error {total / (n * n * n):0.000}, worst {worst:0.000} at #{ColorUtility.ToHtmlStringRGB(worstColor)} (OKLab; under 0.05 is close, over 0.15 is a colour the inks can't make).";
        }

        static Texture3D Separation(ChromoPalette palette, string name, float[] coverage, int inkCount, int firstInk, int n)
        {
            var pixels = new Color32[n * n * n];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte Channel(int k)
                {
                    int ink = firstInk + k;
                    return ink < inkCount ? (byte)Mathf.Clamp(Mathf.RoundToInt(coverage[i * inkCount + ink] * 255f), 0, 255) : (byte)0;
                }
                pixels[i] = new Color32(Channel(0), Channel(1), Channel(2), Channel(3));
            }

            Texture3D texture = SubAsset<Texture3D>(palette, name, t => t.width == n && t.height == n && t.depth == n,
                () => new Texture3D(n, n, n, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None));
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        static Texture2D Grain(ChromoPalette palette, string name, int firstInk)
        {
            int size = ChromoPalette.GrainSize;
            var channels = new float[4][];
            for (int k = 0; k < 4; k++)
            {
                int ink = firstInk + k;
                ChromoGrain.Style style = ink < palette.inks.Count ? palette.inks[ink].grain : ChromoGrain.Style.Crayon;
                channels[k] = ChromoGrain.Grain(style, size, palette.seed + ink * 7919);
            }
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(Byte(channels[0][i]), Byte(channels[1][i]), Byte(channels[2][i]), Byte(channels[3][i]));
            return Texture(palette, name, size, pixels);
        }

        static Texture2D Paper(ChromoPalette palette)
        {
            int size = ChromoPalette.PaperSize;
            float[] rgba = ChromoGrain.Paper(size, palette.seed + 101);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(Byte(rgba[i * 4]), Byte(rgba[i * 4 + 1]), Byte(rgba[i * 4 + 2]), Byte(rgba[i * 4 + 3]));
            return Texture(palette, "Paper", size, pixels);
        }

        static Texture2D Texture(ChromoPalette palette, string name, int size, Color32[] pixels)
        {
            // Mipmapped, so grain seen at a glancing angle or far off softens instead of shimmering.
            Texture2D texture = SubAsset<Texture2D>(palette, name, t => t.width == size && t.height == size && t.mipmapCount > 1,
                () => new Texture2D(size, size, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.MipChain));
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(true);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        static byte Byte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        /// <summary>The palette's sub-asset with this name, reused if it fits, else replaced by a new one.</summary>
        static T SubAsset<T>(ChromoPalette palette, string name, Func<T, bool> fits, Func<T> create) where T : UnityEngine.Object
        {
            string path = AssetDatabase.GetAssetPath(palette);
            T existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault(o => o.name == name);
            if (existing != null && fits(existing)) return existing;
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing, true);
            T created = create();
            created.name = name;
            created.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(created, palette);
            return created;
        }

        [MenuItem("Tools/Chromolithograph/Set Up")]
        static void SetUp()
        {
            // 1. A palette (the first one in the project, or a new Audubon one).
            ChromoPalette palette = AssetDatabase.FindAssets("t:" + nameof(ChromoPalette))
                .Select(guid => AssetDatabase.LoadAssetAtPath<ChromoPalette>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(p => p != null);
            if (palette == null)
            {
                if (!AssetDatabase.IsValidFolder(DefaultFolder)) AssetDatabase.CreateFolder("Assets", "Chromolithograph");
                palette = ScriptableObject.CreateInstance<ChromoPalette>();
                palette.ResetToAudubon();
                AssetDatabase.CreateAsset(palette, DefaultFolder + "/Audubon Palette.asset");
                Debug.Log($"Chromolithograph: created {DefaultFolder}/Audubon Palette.asset.");
            }
            if (palette.NeedsBake) Bake(palette);

            // 2. The feature on every renderer the current pipeline asset uses.
            var pipeline = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                Debug.LogError("Chromolithograph: the project isn't using a URP pipeline asset.");
                return;
            }
            foreach (ScriptableRendererData data in pipeline.rendererDataList)
            {
                if (data == null) continue;
                ChromolithographFeature feature = data.rendererFeatures.OfType<ChromolithographFeature>().FirstOrDefault();
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<ChromolithographFeature>();
                    feature.name = "Chromolithograph";
                    feature.palette = palette;
                    AssetDatabase.AddObjectToAsset(feature, data);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

                    var serialized = new SerializedObject(data);
                    SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
                    SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
                    features.arraySize++;
                    features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    serialized.ApplyModifiedProperties();
                    Debug.Log($"Chromolithograph: added the feature to '{data.name}'.");
                }
                else if (feature.palette == null)
                {
                    Undo.RecordObject(feature, "Assign palette");
                    feature.palette = palette;
                }
                data.SetDirty();
                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(feature);
            }
            AssetDatabase.SaveAssets();

            // 3. Photographic effects that fight the print.
            foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                VolumeProfile profile = volume.sharedProfile;
                if (profile == null) continue;
                if (profile.TryGet(out Bloom bloom) && bloom.active && bloom.intensity.value > 0f)
                    Debug.LogWarning($"Chromolithograph: Bloom is on in '{profile.name}'. Untick it there for the print look (prints don't glow).", profile);
                if (profile.TryGet(out Vignette vignette) && vignette.active && vignette.intensity.value > 0f)
                    Debug.LogWarning($"Chromolithograph: Vignette is on in '{profile.name}'. Untick it there for the print look.", profile);
            }
            Selection.activeObject = palette;
        }
    }
}
