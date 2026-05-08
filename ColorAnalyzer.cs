using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Color_Finder;

public record ColorMatch(OilPigment Pigment, int Percentage, Color DisplayColor);

public static class ColorAnalyzer
{
    // Precompute LAB for every pigment once at startup
    private static readonly (double L, double A, double B)[] PigmentLab =
        PigmentDatabase.Pigments
            .Select(p => ColorUtils.RgbToLab(p.R, p.G, p.B))
            .ToArray();

    private static readonly int WhiteIdx =
        Array.FindIndex(PigmentDatabase.Pigments, p => p.Name == "Titanium White");
    private static readonly int BlackIdx =
        Array.FindIndex(PigmentDatabase.Pigments, p => p.Name == "Ivory Black");

    // These pigments are expressed as Titanium White + Ivory Black ratios instead
    private static readonly HashSet<string> NeutralGreyNames =
        ["Neutral Gray Light", "Neutral Gray", "Davy's Gray"];

    public static List<ColorMatch> Analyze(Bitmap bmp, Rectangle selection, int numColors)
    {
        var pixels = SamplePixels(bmp, selection, maxSamples: 6000);
        if (pixels.Count == 0) return [];

        var labs = pixels.Select(p => ColorUtils.RgbToLab(p.R, p.G, p.B)).ToArray();
        var (centroids, assignments) = KMeans(labs, numColors);

        // Collect cluster sizes and average display colors
        var clusterR = new long[numColors];
        var clusterG = new long[numColors];
        var clusterB = new long[numColors];
        var counts   = new int[numColors];

        for (int i = 0; i < assignments.Length; i++)
        {
            int c = assignments[i];
            counts[c]++;
            clusterR[c] += pixels[i].R;
            clusterG[c] += pixels[i].G;
            clusterB[c] += pixels[i].B;
        }

        int total = pixels.Count;
        var raw = new List<ColorMatch>();

        for (int k = 0; k < numColors; k++)
        {
            if (counts[k] == 0) continue;
            var pigment    = NearestPigment(centroids[k]);
            int percentage = (int)Math.Round(counts[k] * 100.0 / total);
            var displayColor = Color.FromArgb(
                (int)(clusterR[k] / counts[k]),
                (int)(clusterG[k] / counts[k]),
                (int)(clusterB[k] / counts[k]));

            if (NeutralGreyNames.Contains(pigment.Name))
            {
                double chroma = Math.Sqrt(centroids[k].A * centroids[k].A +
                                          centroids[k].B * centroids[k].B);

                if (chroma < 8.0)
                {
                    // Truly neutral — decompose into Titanium White + Ivory Black
                    double lWhite = PigmentLab[WhiteIdx].L;
                    double lBlack = PigmentLab[BlackIdx].L;
                    double wFrac  = Math.Clamp((centroids[k].L - lBlack) / (lWhite - lBlack), 0, 1);

                    int wPct = (int)Math.Round(percentage * wFrac);
                    int bPct = percentage - wPct;

                    var whiteColor = Color.FromArgb(
                        PigmentDatabase.Pigments[WhiteIdx].R,
                        PigmentDatabase.Pigments[WhiteIdx].G,
                        PigmentDatabase.Pigments[WhiteIdx].B);
                    var blackColor = Color.FromArgb(
                        PigmentDatabase.Pigments[BlackIdx].R,
                        PigmentDatabase.Pigments[BlackIdx].G,
                        PigmentDatabase.Pigments[BlackIdx].B);

                    if (wPct > 0) raw.Add(new ColorMatch(PigmentDatabase.Pigments[WhiteIdx], wPct, whiteColor));
                    if (bPct > 0) raw.Add(new ColorMatch(PigmentDatabase.Pigments[BlackIdx], bPct, blackColor));
                }
                else
                {
                    // Chromatic color that happened to match a grey — find best coloured pigment
                    pigment = NearestNonGreyPigment(centroids[k]);
                    raw.Add(new ColorMatch(pigment, percentage, displayColor));
                }
            }
            else
            {
                raw.Add(new ColorMatch(pigment, percentage, displayColor));
            }
        }

        // Merge clusters that resolved to the same pigment
        var merged = raw
            .GroupBy(m => m.Pigment.Name)
            .Select(g =>
            {
                var best = g.OrderByDescending(m => m.Percentage).First();
                return best with { Percentage = g.Sum(m => m.Percentage) };
            })
            .OrderByDescending(m => m.Percentage)
            .ToList();

        Normalize(merged);
        return merged;
    }

    // ── Pixel extraction ────────────────────────────────────────────────────

    private static List<(byte R, byte G, byte B)> SamplePixels(
        Bitmap bmp, Rectangle sel, int maxSamples)
    {
        sel.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));
        if (sel.Width <= 0 || sel.Height <= 0) return [];

        // Extract region as 32bpp ARGB for reliable LockBits access
        using var region = new Bitmap(sel.Width, sel.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(region))
            g.DrawImage(bmp, new Rectangle(0, 0, sel.Width, sel.Height), sel, GraphicsUnit.Pixel);

        var data = region.LockBits(
            new Rectangle(0, 0, region.Width, region.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        byte[] buf = new byte[data.Stride * region.Height];
        Marshal.Copy(data.Scan0, buf, 0, buf.Length);
        region.UnlockBits(data);

        int step = Math.Max(1, (int)Math.Sqrt(region.Width * region.Height / (double)maxSamples));
        var pixels = new List<(byte, byte, byte)>(maxSamples);

        for (int y = 0; y < region.Height; y += step)
        for (int x = 0; x < region.Width;  x += step)
        {
            int offset = y * data.Stride + x * 4; // BGRA layout
            pixels.Add((buf[offset + 2], buf[offset + 1], buf[offset]));
        }

        return pixels;
    }

    // ── K-means ─────────────────────────────────────────────────────────────

    private static ((double L, double A, double B)[] centroids, int[] assignments)
        KMeans((double L, double A, double B)[] points, int k)
    {
        var centroids   = KMeansPlusPlus(points, k);
        var assignments = new int[points.Length];

        for (int iter = 0; iter < 60; iter++)
        {
            bool changed = false;

            // Assignment step
            for (int i = 0; i < points.Length; i++)
            {
                int best = 0;
                double bestDist = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double d = ColorUtils.DeltaE(points[i], centroids[c]);
                    if (d < bestDist) { bestDist = d; best = c; }
                }
                if (assignments[i] != best) { assignments[i] = best; changed = true; }
            }
            if (!changed) break;

            // Update step
            var sumL   = new double[k];
            var sumA   = new double[k];
            var sumB   = new double[k];
            var counts = new int[k];

            for (int i = 0; i < points.Length; i++)
            {
                int c = assignments[i];
                sumL[c] += points[i].L;
                sumA[c] += points[i].A;
                sumB[c] += points[i].B;
                counts[c]++;
            }
            for (int c = 0; c < k; c++)
            {
                if (counts[c] > 0)
                    centroids[c] = (sumL[c] / counts[c], sumA[c] / counts[c], sumB[c] / counts[c]);
            }
        }

        return (centroids, assignments);
    }

    private static (double L, double A, double B)[] KMeansPlusPlus(
        (double L, double A, double B)[] points, int k)
    {
        var rng = new Random(42);
        var centroids = new (double L, double A, double B)[k];
        centroids[0] = points[rng.Next(points.Length)];

        for (int c = 1; c < k; c++)
        {
            double[] dists = points.Select(p =>
            {
                double minD = double.MaxValue;
                for (int j = 0; j < c; j++)
                    minD = Math.Min(minD, ColorUtils.DeltaE(p, centroids[j]));
                return minD * minD;
            }).ToArray();

            double total = dists.Sum();
            double r = rng.NextDouble() * total;
            double cumul = 0;
            int idx = 0;
            for (int i = 0; i < dists.Length; i++)
            {
                cumul += dists[i];
                if (cumul >= r) { idx = i; break; }
            }
            centroids[c] = points[idx];
        }

        return centroids;
    }

    // ── Pigment matching ────────────────────────────────────────────────────

    private static OilPigment NearestPigment((double L, double A, double B) lab)
    {
        int best = 0;
        double bestDist = double.MaxValue;
        for (int i = 0; i < PigmentLab.Length; i++)
        {
            double d = ColorUtils.DeltaE(lab, PigmentLab[i]);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return PigmentDatabase.Pigments[best];
    }

    private static OilPigment NearestNonGreyPigment((double L, double A, double B) lab)
    {
        int best = 0;
        double bestDist = double.MaxValue;
        for (int i = 0; i < PigmentLab.Length; i++)
        {
            if (NeutralGreyNames.Contains(PigmentDatabase.Pigments[i].Name)) continue;
            double d = ColorUtils.DeltaE(lab, PigmentLab[i]);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return PigmentDatabase.Pigments[best];
    }

    private static void Normalize(List<ColorMatch> list)
    {
        int sum = list.Sum(m => m.Percentage);
        if (sum == 0 || sum == 100) return;
        int diff = 100 - sum;
        list[0] = list[0] with { Percentage = list[0].Percentage + diff };
    }
}
