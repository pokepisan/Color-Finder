namespace Color_Finder;

public static class ColorUtils
{
    public static (double L, double A, double B) RgbToLab(byte r, byte g, byte b)
    {
        // sRGB → linear
        double rl = Linearize(r / 255.0);
        double gl = Linearize(g / 255.0);
        double bl = Linearize(b / 255.0);

        // Linear RGB → XYZ (D65)
        double x = (rl * 0.4124564 + gl * 0.3575761 + bl * 0.1804375) / 0.95047;
        double y = (rl * 0.2126729 + gl * 0.7151522 + bl * 0.0721750) / 1.00000;
        double z = (rl * 0.0193339 + gl * 0.1191920 + bl * 0.9503041) / 1.08883;

        // XYZ → LAB
        x = LabF(x);
        y = LabF(y);
        z = LabF(z);

        return (116.0 * y - 16.0, 500.0 * (x - y), 200.0 * (y - z));
    }

    public static double DeltaE(
        (double L, double A, double B) a,
        (double L, double A, double B) b)
    {
        double dL = a.L - b.L;
        double dA = a.A - b.A;
        double dB = a.B - b.B;
        return Math.Sqrt(dL * dL + dA * dA + dB * dB);
    }

    private static double Linearize(double v) =>
        v > 0.04045 ? Math.Pow((v + 0.055) / 1.055, 2.4) : v / 12.92;

    private static double LabF(double t) =>
        t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116.0;
}
