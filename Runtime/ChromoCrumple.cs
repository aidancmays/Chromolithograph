using UnityEngine;

namespace Chromolithograph
{
    /// <summary>
    /// The texture corruption is printed with (Corruption Plan 3.3), made once per session in code: the look of
    /// crumpled film pressed into wet paint. Tileable, linear, with mips.
    /// R = crease lines (straight-edged, angular, where pigment pools), G = facet tone (one value per facet with a slight
    /// slope, so facets read as separate planes), B = bloom field (soft blotches; the print cuts hard rims from it),
    /// A = dendrite field (branching, feathery noise that breaks up the edge of corruption).
    /// </summary>
    public static class ChromoCrumple
    {
        public const int Size = 256;
        static Texture2D texture;

        /// <summary>The texture, made on first use.</summary>
        public static Texture2D Texture
        {
            get
            {
                if (texture == null) texture = Make(Size, 1337);
                return texture;
            }
        }

        static Texture2D Make(int size, int seed)
        {
            var pixels = new Color32[size * size];
            const int coarseCells = 7, fineCells = 17;
            Vector2[] coarse = Points(coarseCells, seed);
            Vector2[] fine = Points(fineCells, seed + 1);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    // Creases: the borders between Voronoi cells are straight, so the network is angular like folded
                    // film. A finer, fainter network adds the small creases between the big ones.
                    Cell(coarse, coarseCells, p, out float coarseEdge, out int coarseId, out Vector2 coarseOffset);
                    Cell(fine, fineCells, p, out float fineEdge, out int fineId, out _);
                    float big = Line(coarseEdge * coarseCells, 0.06f);
                    float small = Line(fineEdge * fineCells, 0.05f) * 0.45f;
                    float pooled = Mathf.Exp(-coarseEdge * coarseCells * 6f) * 0.35f; // pigment drifts into the fold
                    float crease = Mathf.Clamp01(Mathf.Max(big, small) + pooled);

                    // Facets: a random tone per coarse cell, tilted a little across it, and a little from the fine cells.
                    float tone = Hash(coarseId, seed + 7);
                    Vector2 tilt = new Vector2(Hash(coarseId, seed + 11) - 0.5f, Hash(coarseId, seed + 13) - 0.5f);
                    float facet = Mathf.Clamp01(0.5f + (tone - 0.5f) * 0.7f + Vector2.Dot(coarseOffset * coarseCells, tilt) * 0.35f
                                                + (Hash(fineId, seed + 17) - 0.5f) * 0.15f);

                    float bloom = Fbm(p, 3, 3, seed + 23);
                    float dendrite = Ridged(p, 5, 4, seed + 29);

                    pixels[y * size + x] = new Color32(Byte(crease), Byte(facet), Byte(bloom), Byte(dendrite));
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "Chromo Crumple",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        // One jittered point per cell of an n x n grid on the unit torus.
        static Vector2[] Points(int n, int seed)
        {
            var points = new Vector2[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int id = j * n + i;
                    points[id] = new Vector2((i + 0.15f + 0.7f * Hash(id, seed)) / n, (j + 0.15f + 0.7f * Hash(id, seed + 3)) / n);
                }
            return points;
        }

        // Distance to the nearest cell border (half of F2 - F1 along the line between the two nearest points), which
        // cell this is, and the offset from its point, all wrapping.
        static void Cell(Vector2[] points, int n, Vector2 p, out float edge, out int id, out Vector2 offset)
        {
            int ci = Mathf.FloorToInt(p.x * n), cj = Mathf.FloorToInt(p.y * n);
            float best = float.MaxValue;
            Vector2 bestPoint = Vector2.zero;
            id = 0;
            for (int dj = -2; dj <= 2; dj++)
                for (int di = -2; di <= 2; di++)
                {
                    int i = ci + di, j = cj + dj;
                    int wi = ((i % n) + n) % n, wj = ((j % n) + n) % n;
                    Vector2 q = points[wj * n + wi] + new Vector2(Mathf.Floor((float)i / n), Mathf.Floor((float)j / n));
                    float d = (q - p).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        bestPoint = q;
                        id = wj * n + wi;
                    }
                }
            // The border distance to every other neighbour: the nearest one is the crease.
            edge = float.MaxValue;
            for (int dj = -2; dj <= 2; dj++)
                for (int di = -2; di <= 2; di++)
                {
                    int i = ci + di, j = cj + dj;
                    int wi = ((i % n) + n) % n, wj = ((j % n) + n) % n;
                    Vector2 q = points[wj * n + wi] + new Vector2(Mathf.Floor((float)i / n), Mathf.Floor((float)j / n));
                    if ((q - bestPoint).sqrMagnitude < 1e-12f) continue;
                    Vector2 mid = (q + bestPoint) * 0.5f;
                    Vector2 dir = (q - bestPoint).normalized;
                    edge = Mathf.Min(edge, Vector2.Dot(mid - p, dir));
                }
            offset = p - bestPoint;
        }

        // A sharp line of the given half width (in cell units), soft at its edge.
        static float Line(float distance, float halfWidth) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distance - halfWidth * 0.4f) / halfWidth));

        static float Fbm(Vector2 p, int baseCells, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, total = 0f;
            int cells = baseCells;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(p, cells, seed + o * 31) * amp;
                total += amp;
                amp *= 0.5f;
                cells *= 2;
            }
            return sum / total;
        }

        // Ridged noise: 1 - |n| folded at each octave, so it makes thin branching veins.
        static float Ridged(Vector2 p, int baseCells, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, total = 0f;
            int cells = baseCells;
            for (int o = 0; o < octaves; o++)
            {
                float n = Value(p, cells, seed + o * 37) * 2f - 1f;
                sum += (1f - Mathf.Abs(n)) * amp;
                total += amp;
                amp *= 0.55f;
                cells *= 2;
            }
            float r = sum / total;
            return Mathf.Clamp01((r - 0.45f) / 0.5f);
        }

        // Smooth value noise on an n x n lattice that wraps.
        static float Value(Vector2 p, int n, int seed)
        {
            float x = p.x * n, y = p.y * n;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Lattice(x0, y0, n, seed), b = Lattice(x0 + 1, y0, n, seed);
            float c = Lattice(x0, y0 + 1, n, seed), d = Lattice(x0 + 1, y0 + 1, n, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Lattice(int x, int y, int n, int seed) => Hash(((y % n + n) % n) * n + ((x % n + n) % n), seed);

        static float Hash(int i, int seed)
        {
            unchecked
            {
                uint h = (uint)i * 747796405u + (uint)seed * 2891336453u + 12345u;
                h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
                h = (h >> 22) ^ h;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static byte Byte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }
}
