using System;

namespace Chromolithograph
{
    /// <summary>
    /// Makes the tiling textures the print is built from. Plain C# so it can be tested outside Unity.
    ///
    /// Grain: one per ink, the pattern a stone's ink breaks into. The shader lays a dot wherever the ink's coverage is
    /// above the grain value, so every grain is equalised (its values spread evenly over 0..1) and a coverage of 0.3
    /// covers 30% of the paper whatever the pattern looks like.
    /// - Crayon: litho crayon on grained stone. Clumpy noise at two sizes, the look of most Bien tones.
    /// - Stipple: fine, even dots (noise with the low frequencies taken out), for smooth flesh and sky tints.
    /// - Ruled: wavering horizontal lines, like a ruled tint stone; coverage sets how thick the lines are.
    ///
    /// Paper: R fibre, G slow mottling (uneven ink take-up), B foxing (rare brown spots), A slow wobble (lines).
    /// </summary>
    public static class ChromoGrain
    {
        public enum Style { Crayon, Stipple, Ruled }

        public static float[] Grain(Style style, int size, int seed)
        {
            var random = new Random(seed);
            float[] white = WhiteNoise(size, random);
            float[] values;
            switch (style)
            {
                case Style.Stipple:
                {
                    float[] low = Blur(white, size, 2.5f);
                    values = new float[white.Length];
                    for (int i = 0; i < values.Length; i++) values[i] = white[i] - low[i];
                    values = Blur(values, size, 0.6f);
                    break;
                }
                case Style.Ruled:
                {
                    float[] waver = Blur(WhiteNoise(size, random), size, 6f);
                    Normalise(waver);
                    const float period = 4f; // pixels between rules at grain scale 1 (divides 256)
                    values = new float[white.Length];
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int i = y * size + x;
                        float v = y / period + (waver[i] - 0.5f) * 0.6f;
                        float line = Math.Abs(v - (float)Math.Floor(v) - 0.5f) * 2f; // 0 on a rule, 1 between
                        values[i] = line + (white[i] - 0.5f) * 0.25f;
                    }
                    break;
                }
                default:
                {
                    float[] fine = Blur(white, size, 1.1f);
                    float[] coarse = Blur(WhiteNoise(size, random), size, 3.5f);
                    Normalise(fine);
                    Normalise(coarse);
                    values = new float[white.Length];
                    for (int i = 0; i < values.Length; i++) values[i] = fine[i] + coarse[i] * 0.45f + (white[i] - 0.5f) * 0.15f;
                    break;
                }
            }
            Equalise(values);
            return values;
        }

        /// <summary>Four channels, interleaved RGBA.</summary>
        public static float[] Paper(int size, int seed)
        {
            var random = new Random(seed);
            int n = size * size;

            // Fibre: short streaks, blurred more along x than y.
            float[] fibre = BlurXY(WhiteNoise(size, random), size, 2.5f, 0.7f);
            Normalise(fibre);
            float[] mottle = Blur(WhiteNoise(size, random), size, 14f);
            Normalise(mottle);
            float[] fox = Blur(WhiteNoise(size, random), size, 5f);
            Normalise(fox);
            float[] wobble = Blur(WhiteNoise(size, random), size, 9f);
            Normalise(wobble);

            float[] rgba = new float[n * 4];
            for (int i = 0; i < n; i++)
            {
                rgba[i * 4 + 0] = fibre[i];
                rgba[i * 4 + 1] = mottle[i];
                // Only the highest peaks become spots.
                rgba[i * 4 + 2] = Math.Max(0f, (fox[i] - 0.8f) / 0.2f);
                rgba[i * 4 + 3] = wobble[i];
            }
            return rgba;
        }

        static float[] WhiteNoise(int size, Random random)
        {
            float[] values = new float[size * size];
            for (int i = 0; i < values.Length; i++) values[i] = (float)random.NextDouble();
            return values;
        }

        static float[] Blur(float[] source, int size, float radius) => BlurXY(source, size, radius, radius);

        /// <summary>Gaussian blur that wraps round the edges, so the result still tiles.</summary>
        static float[] BlurXY(float[] source, int size, float radiusX, float radiusY)
        {
            float[] temp = BlurAxis(source, size, radiusX, true);
            return BlurAxis(temp, size, radiusY, false);
        }

        static float[] BlurAxis(float[] source, int size, float sigma, bool horizontal)
        {
            int reach = Math.Max(1, (int)Math.Ceiling(sigma * 3f));
            float[] kernel = new float[reach * 2 + 1];
            float sum = 0f;
            for (int k = -reach; k <= reach; k++)
            {
                float w = (float)Math.Exp(-(k * k) / (2f * sigma * sigma));
                kernel[k + reach] = w;
                sum += w;
            }
            for (int k = 0; k < kernel.Length; k++) kernel[k] /= sum;

            float[] result = new float[source.Length];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float v = 0f;
                for (int k = -reach; k <= reach; k++)
                {
                    int sx = horizontal ? Wrap(x + k, size) : x;
                    int sy = horizontal ? y : Wrap(y + k, size);
                    v += source[sy * size + sx] * kernel[k + reach];
                }
                result[y * size + x] = v;
            }
            return result;
        }

        static int Wrap(int i, int size) => ((i % size) + size) % size;

        static void Normalise(float[] values)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (float v in values) { if (v < min) min = v; if (v > max) max = v; }
            float range = Math.Max(max - min, 1e-6f);
            for (int i = 0; i < values.Length; i++) values[i] = (values[i] - min) / range;
        }

        /// <summary>Replaces each value by its rank, so the values are spread evenly over 0..1.</summary>
        static void Equalise(float[] values)
        {
            int n = values.Length;
            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            float[] keys = (float[])values.Clone();
            Array.Sort(keys, order);
            for (int rank = 0; rank < n; rank++) values[order[rank]] = (rank + 0.5f) / n;
        }
    }
}
