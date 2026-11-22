using System;
using System.Threading.Tasks;

namespace Chromolithograph
{
    /// <summary>
    /// Colour separation: for any colour, how much of each ink a printer would lay down so that the inks, printed one
    /// over another on the paper, come closest to it. Plain C# with no Unity types, so it can run on worker threads
    /// (and be tested outside Unity).
    ///
    /// Print model: each ink is a transparent colour that multiplies what is under it. An ink with coverage c (the
    /// share of the area its dots cover) lets through 1 - c (1 - ink) of the light, per channel; dots of different
    /// stones fall independently, so the expected print is paper x the product of that over every ink. The shader
    /// thresholds an equalised grain per ink, which gives the same average.
    ///
    /// The fit is made in OKLab (a perceptual colour space) against the target colour multiplied by the paper, so the
    /// frame's white becomes bare paper. A small cost per unit of ink makes the solver prefer fewer inks, as a printer
    /// would, and keeps light colours from being speckled with every stone.
    /// </summary>
    public static class ChromoSeparation
    {
        public struct Settings
        {
            /// <summary>Linear RGB of each ink, in print order.</summary>
            public float[][] Inks;
            /// <summary>Cost of using each ink (1 = normal; accents higher).</summary>
            public float[] Costs;
            /// <summary>Linear RGB of the paper.</summary>
            public float[] Paper;
            /// <summary>Weight of the ink cost against colour error (OKLab units squared).</summary>
            public float InkCost;
            /// <summary>Grid points per axis.</summary>
            public int Size;
            public int Iterations;
            /// <summary>
            /// How strongly each grid colour's mix is pulled toward its neighbours' (coverage units squared, against
            /// OKLab error squared). Without it, two nearby colours can get very different mixes that print alike, and a
            /// colour drifting slightly in the game swaps one mix for the other, which flickers.
            /// </summary>
            public float Smoothness;
            /// <summary>Rounds of smoothing; each spreads agreement one grid step further.</summary>
            public int SmoothPasses;
        }

        /// <summary>
        /// Coverages for every grid colour, indexed [((b * size + g) * size + r) * inkCount + ink]. The grid is in
        /// sRGB (gamma) steps from 0 to 1, which spaces the points more evenly to the eye than linear steps.
        /// </summary>
        public static float[] Solve(Settings s, Action<float> progress = null)
        {
            int n = s.Size;
            int inkCount = s.Inks.Length;
            float[] result = new float[n * n * n * inkCount];
            int rowsDone = 0;

            // Rows along red are solved in order, each point starting from its neighbour's answer, so the solution
            // changes smoothly across the table instead of jumping between equally good ink mixes.
            Parallel.For(0, n * n, row =>
            {
                int g = row % n, b = row / n;
                float[] c = new float[inkCount];
                float[] scratch = new float[inkCount];
                for (int r = 0; r < n; r++)
                {
                    float[] target = TargetFor(r, g, b, n, s.Paper);
                    if (r == 0) Array.Clear(c, 0, inkCount);
                    Fit(s, target, c, scratch, s.Iterations);
                    Array.Copy(c, 0, result, (((b * n + g) * n) + r) * inkCount, inkCount);
                }
                int done = System.Threading.Interlocked.Increment(ref rowsDone);
                if (progress != null && (done & 31) == 0) progress(done / (float)(n * n) * 0.5f);
            });

            // Smoothing: each round refits every point with a pull toward the average of its six neighbours, so the
            // mix changes gradually across the table instead of jumping between equally good mixes.
            int passes = Math.Max(1, s.SmoothPasses);
            float[] next = new float[result.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                float[] source = result;
                Parallel.For(0, n * n, row =>
                {
                    int g = row % n, b = row / n;
                    float[] c = new float[inkCount];
                    float[] prior = new float[inkCount];
                    float[] scratch = new float[inkCount];
                    for (int r = 0; r < n; r++)
                    {
                        Array.Clear(prior, 0, inkCount);
                        int count = 0;
                        AddNeighbour(source, prior, r - 1, g, b, n, inkCount, ref count);
                        AddNeighbour(source, prior, r + 1, g, b, n, inkCount, ref count);
                        AddNeighbour(source, prior, r, g - 1, b, n, inkCount, ref count);
                        AddNeighbour(source, prior, r, g + 1, b, n, inkCount, ref count);
                        AddNeighbour(source, prior, r, g, b - 1, n, inkCount, ref count);
                        AddNeighbour(source, prior, r, g, b + 1, n, inkCount, ref count);
                        for (int i = 0; i < inkCount; i++) prior[i] /= Math.Max(count, 1);

                        int index = (((b * n + g) * n) + r) * inkCount;
                        // Start from whichever of its own mix and the neighbours' average fits better with the pull.
                        float[] target = TargetFor(r, g, b, n, s.Paper);
                        float[] targetLab = OkLab(target[0], target[1], target[2]);
                        Array.Copy(source, index, c, 0, inkCount);
                        float own = Energy(s, targetLab, c, prior, s.Smoothness, out _, out _);
                        float shared = Energy(s, targetLab, prior, prior, s.Smoothness, out _, out _);
                        if (shared < own) Array.Copy(prior, c, inkCount);
                        Fit(s, target, c, scratch, s.Iterations / 2, prior, s.Smoothness);
                        Array.Copy(c, 0, next, index, inkCount);
                    }
                });
                float[] swap = result; result = next; next = swap;
                if (progress != null) progress(0.5f + (pass + 1) / (float)passes * 0.5f);
            }
            return result;
        }

        static void AddNeighbour(float[] result, float[] sum, int r, int g, int b, int n, int inkCount, ref int count)
        {
            if (r < 0 || g < 0 || b < 0 || r >= n || g >= n || b >= n) return;
            int index = (((b * n + g) * n) + r) * inkCount;
            for (int i = 0; i < inkCount; i++) sum[i] += result[index + i];
            count++;
        }

        static float[] TargetFor(int r, int g, int b, int n, float[] paper)
        {
            float step = 1f / (n - 1);
            return new[]
            {
                SrgbToLinear(r * step) * paper[0],
                SrgbToLinear(g * step) * paper[1],
                SrgbToLinear(b * step) * paper[2],
            };
        }

        /// <summary>Projected gradient descent on the coverages (kept within 0..1), with an adaptive step.</summary>
        public static void Fit(Settings s, float[] target, float[] c, float[] scratch, int iterations, float[] prior = null, float pull = 0f)
        {
            int inkCount = c.Length;
            float[] targetLab = OkLab(target[0], target[1], target[2]);
            float energy = Energy(s, targetLab, c, prior, pull, out float[] print, out float[] lab);
            float step = 0.2f;
            float[] gradient = new float[inkCount];

            for (int it = 0; it < iterations && step > 1e-5f; it++)
            {
                // Jacobian of OKLab at the current print colour (numerical; three extra conversions).
                const float h = 1e-4f;
                float[] j0 = OkLab(print[0] + h, print[1], print[2]);
                float[] j1 = OkLab(print[0], print[1] + h, print[2]);
                float[] j2 = OkLab(print[0], print[1], print[2] + h);
                float e0 = lab[0] - targetLab[0], e1 = lab[1] - targetLab[1], e2 = lab[2] - targetLab[2];
                // d(error^2)/d(print channel)
                float g0 = 2f * (e0 * (j0[0] - lab[0]) + e1 * (j0[1] - lab[1]) + e2 * (j0[2] - lab[2])) / h;
                float g1 = 2f * (e0 * (j1[0] - lab[0]) + e1 * (j1[1] - lab[1]) + e2 * (j1[2] - lab[2])) / h;
                float g2 = 2f * (e0 * (j2[0] - lab[0]) + e1 * (j2[1] - lab[1]) + e2 * (j2[2] - lab[2])) / h;

                float norm = 0f;
                for (int k = 0; k < inkCount; k++)
                {
                    // d(print)/d(c_k) = -paper * a_k * product of the other inks' transmissions.
                    float[] ink = s.Inks[k];
                    float p0 = s.Paper[0], p1 = s.Paper[1], p2 = s.Paper[2];
                    for (int i = 0; i < inkCount; i++)
                    {
                        if (i == k) continue;
                        float[] other = s.Inks[i];
                        p0 *= 1f - c[i] * (1f - other[0]);
                        p1 *= 1f - c[i] * (1f - other[1]);
                        p2 *= 1f - c[i] * (1f - other[2]);
                    }
                    float d = -(g0 * p0 * (1f - ink[0]) + g1 * p1 * (1f - ink[1]) + g2 * p2 * (1f - ink[2]));
                    d += s.InkCost * Cost(s, k);
                    if (prior != null) d += 2f * pull * (c[k] - prior[k]);
                    gradient[k] = d;
                    norm += d * d;
                }
                norm = (float)Math.Sqrt(norm);
                if (norm < 1e-7f) break;

                for (int k = 0; k < inkCount; k++)
                {
                    scratch[k] = c[k] - step * gradient[k] / norm;
                    if (scratch[k] < 0f) scratch[k] = 0f;
                    else if (scratch[k] > 1f) scratch[k] = 1f;
                }
                float next = Energy(s, targetLab, scratch, prior, pull, out float[] nextPrint, out float[] nextLab);
                if (next < energy)
                {
                    Array.Copy(scratch, c, inkCount);
                    energy = next;
                    print = nextPrint;
                    lab = nextLab;
                    step *= 1.3f;
                }
                else step *= 0.5f;
            }
        }

        static float Cost(Settings s, int ink) => s.Costs != null && ink < s.Costs.Length ? s.Costs[ink] : 1f;

        static float Energy(Settings s, float[] targetLab, float[] c, float[] prior, float pull, out float[] print, out float[] lab)
        {
            print = Print(s, c);
            lab = OkLab(print[0], print[1], print[2]);
            float e0 = lab[0] - targetLab[0], e1 = lab[1] - targetLab[1], e2 = lab[2] - targetLab[2];
            float energy = e0 * e0 + e1 * e1 + e2 * e2;
            for (int k = 0; k < c.Length; k++) energy += s.InkCost * Cost(s, k) * c[k];
            if (prior != null)
                for (int k = 0; k < c.Length; k++) energy += pull * (c[k] - prior[k]) * (c[k] - prior[k]);
            return energy;
        }

        /// <summary>Linear RGB of the paper with these coverages printed on it.</summary>
        public static float[] Print(Settings s, float[] c)
        {
            float r = s.Paper[0], g = s.Paper[1], b = s.Paper[2];
            for (int i = 0; i < c.Length; i++)
            {
                float[] ink = s.Inks[i];
                r *= 1f - c[i] * (1f - ink[0]);
                g *= 1f - c[i] * (1f - ink[1]);
                b *= 1f - c[i] * (1f - ink[2]);
            }
            return new[] { r, g, b };
        }

        /// <summary>OKLab from linear sRGB (Björn Ottosson's matrices).</summary>
        public static float[] OkLab(float r, float g, float b)
        {
            double l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
            double m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
            double s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;
            l = Cbrt(l); m = Cbrt(m); s = Cbrt(s);
            return new[]
            {
                (float)(0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s),
                (float)(1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s),
                (float)(0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s),
            };
        }

        static double Cbrt(double x) => x < 0 ? -Math.Pow(-x, 1.0 / 3.0) : Math.Pow(x, 1.0 / 3.0);

        public static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);

        public static float LinearToSrgb(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * (float)Math.Pow(c, 1f / 2.4f) - 0.055f;
    }
}
