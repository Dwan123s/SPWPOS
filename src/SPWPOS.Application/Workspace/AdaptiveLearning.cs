namespace SPWPOS.Application.Workspace;

public static class AdaptiveLearning
{
    // Policy parameters, not experimentally calibrated population constants.
    public const double Alpha = 0.2;
    public const double MinimumBias = 0.5;
    public const double MaximumBias = 3.0;
    public static double Ema(IEnumerable<double> chronologicalSamples, double fallback = 1,
        double minimum = double.NegativeInfinity, double maximum = double.PositiveInfinity)
    {
        double? value = null;
        foreach (var sample in chronologicalSamples.Where(double.IsFinite))
            value = Math.Clamp(value is null ? sample : Alpha * sample + (1 - Alpha) * value.Value, minimum, maximum);
        return value ?? fallback;
    }
}
