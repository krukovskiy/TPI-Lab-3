namespace EdfBrowser.Dsp.Design;

using System.Numerics;
using EdfBrowser.Dsp.Filters;

public enum IirFilterType { LowPass, HighPass, BandPass, BandStop }

public static class ButterworthDesigner
{
    public static IFilter[] DesignCascade(IirFilterType type, int order, double freq1, double freq2, double sampleRate)
    {
        if (order < 1) throw new ArgumentOutOfRangeException(nameof(order), "Order must be >= 1");
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

        double nyquist = sampleRate / 2.0;
        double w1 = 2.0 * Math.Tan(Math.PI * freq1 / sampleRate);
        double w2 = type is IirFilterType.BandPass or IirFilterType.BandStop
            ? 2.0 * Math.Tan(Math.PI * freq2 / sampleRate)
            : w1;

        var filters = new List<IFilter>();
        int numSections = (order + 1) / 2;

        for (int i = 0; i < numSections; i++)
        {
            double angle = Math.PI * (2.0 * i + order + 1) / (2.0 * order);
            var pole = new Complex(Math.Cos(angle), Math.Sin(angle));

            double b0, b1, b2, a1, a2;

            if (i == numSections - 1 && order % 2 == 1)
            {
                // First-order section for odd-order filters
                switch (type)
                {
                    case IirFilterType.LowPass:
                        ComputeFirstOrderLowPass(w1, out b0, out b1, out a1);
                        filters.Add(new BiquadFilter(b0, b1, 0, a1, 0));
                        break;
                    case IirFilterType.HighPass:
                        ComputeFirstOrderHighPass(w1, out b0, out b1, out a1);
                        filters.Add(new BiquadFilter(b0, b1, 0, a1, 0));
                        break;
                    default:
                        ComputeFirstOrderLowPass(w1, out b0, out b1, out a1);
                        filters.Add(new BiquadFilter(b0, b1, 0, a1, 0));
                        break;
                }
            }
            else
            {
                double realPart = -pole.Real;
                switch (type)
                {
                    case IirFilterType.LowPass:
                        ComputeSecondOrderLowPass(w1, realPart, out b0, out b1, out b2, out a1, out a2);
                        break;
                    case IirFilterType.HighPass:
                        ComputeSecondOrderHighPass(w1, realPart, out b0, out b1, out b2, out a1, out a2);
                        break;
                    case IirFilterType.BandPass:
                        ComputeSecondOrderBandPass(w1, w2, realPart, out b0, out b1, out b2, out a1, out a2);
                        break;
                    case IirFilterType.BandStop:
                        ComputeSecondOrderBandStop(w1, w2, realPart, out b0, out b1, out b2, out a1, out a2);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type));
                }
                filters.Add(new BiquadFilter(b0, b1, b2, a1, a2));
            }
        }

        return filters.ToArray();
    }

    private static void ComputeFirstOrderLowPass(double w, out double b0, out double b1, out double a1)
    {
        double k = w / (w + 2.0);
        b0 = k;
        b1 = k;
        a1 = (w - 2.0) / (w + 2.0);
    }

    private static void ComputeFirstOrderHighPass(double w, out double b0, out double b1, out double a1)
    {
        double k = 2.0 / (w + 2.0);
        b0 = k;
        b1 = -k;
        a1 = (w - 2.0) / (w + 2.0);
    }

    private static void ComputeSecondOrderLowPass(double w, double q, out double b0, out double b1, out double b2, out double a1, out double a2)
    {
        double w2 = w * w;
        double d = 4.0 + 2.0 * q * w + w2;
        b0 = w2 / d;
        b1 = 2.0 * w2 / d;
        b2 = w2 / d;
        a1 = (2.0 * w2 - 8.0) / d;
        a2 = (4.0 - 2.0 * q * w + w2) / d;
    }

    private static void ComputeSecondOrderHighPass(double w, double q, out double b0, out double b1, out double b2, out double a1, out double a2)
    {
        double w2 = w * w;
        double d = 4.0 + 2.0 * q * w + w2;
        b0 = 4.0 / d;
        b1 = -8.0 / d;
        b2 = 4.0 / d;
        a1 = (2.0 * w2 - 8.0) / d;
        a2 = (4.0 - 2.0 * q * w + w2) / d;
    }

    private static void ComputeSecondOrderBandPass(double w1, double w2, double q, out double b0, out double b1, out double b2, out double a1, out double a2)
    {
        double bw = w2 - w1;
        double w0 = Math.Sqrt(w1 * w2);
        double w02 = w0 * w0;
        double d = 4.0 + 2.0 * q * bw + w02;
        b0 = 2.0 * q * bw / d;
        b1 = 0;
        b2 = -2.0 * q * bw / d;
        a1 = (2.0 * w02 - 8.0) / d;
        a2 = (4.0 - 2.0 * q * bw + w02) / d;
    }

    private static void ComputeSecondOrderBandStop(double w1, double w2, double q, out double b0, out double b1, out double b2, out double a1, out double a2)
    {
        double bw = w2 - w1;
        double w0 = Math.Sqrt(w1 * w2);
        double w02 = w0 * w0;
        double d = 4.0 + 2.0 * q * bw + w02;
        b0 = (4.0 + w02) / d;
        b1 = (2.0 * w02 - 8.0) / d;
        b2 = (4.0 + w02) / d;
        a1 = (2.0 * w02 - 8.0) / d;
        a2 = (4.0 - 2.0 * q * bw + w02) / d;
    }
}
