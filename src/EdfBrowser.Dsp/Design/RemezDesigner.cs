namespace EdfBrowser.Dsp.Design;

/// <summary>
/// Parks-McClellan equiripple FIR filter design using the Remez exchange algorithm.
/// Produces optimal (minimax) linear-phase FIR filters.
/// </summary>
public static class RemezDesigner
{
    private const int GridDensity = 16;
    private const int MaxIterations = 40;
    private const double ConvergenceTolerance = 1e-6;

    /// <summary>
    /// Designs an equiripple FIR filter using the Parks-McClellan (Remez exchange) algorithm.
    /// </summary>
    /// <param name="numTaps">Number of filter coefficients (taps). Must be at least 3.</param>
    /// <param name="numBands">Number of frequency bands.</param>
    /// <param name="bands">
    /// Band edge frequencies normalized to [0, 0.5] (Nyquist = 0.5).
    /// Array of length 2 * numBands: [low1, high1, low2, high2, ...].
    /// </param>
    /// <param name="desired">
    /// Desired magnitude response at each band edge.
    /// Array of length 2 * numBands: [desLow1, desHigh1, desLow2, desHigh2, ...].
    /// </param>
    /// <param name="weights">
    /// Weight for each band. Array of length numBands.
    /// </param>
    /// <returns>Array of filter coefficients of length numTaps.</returns>
    public static double[] Design(int numTaps, int numBands, double[] bands, double[] desired, double[] weights)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numTaps, 3);
        ArgumentOutOfRangeException.ThrowIfLessThan(numBands, 1);
        ArgumentNullException.ThrowIfNull(bands);
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(weights);

        if (bands.Length != 2 * numBands)
            throw new ArgumentException($"bands must have {2 * numBands} elements.", nameof(bands));
        if (desired.Length != 2 * numBands)
            throw new ArgumentException($"desired must have {2 * numBands} elements.", nameof(desired));
        if (weights.Length != numBands)
            throw new ArgumentException($"weights must have {numBands} elements.", nameof(weights));

        // Determine filter symmetry: Type I (odd taps) or Type II (even taps)
        bool isOddLength = (numTaps % 2) != 0;
        int filterLength = (numTaps + 1) / 2; // Number of unique coefficients (including center for odd)
        int numExtremals = filterLength + 1;   // L + 1 extremal frequencies

        // Build the dense frequency grid
        BuildGrid(numBands, bands, desired, weights, filterLength, isOddLength,
            out double[] grid, out double[] desiredOnGrid, out double[] weightOnGrid, out int gridSize);

        if (gridSize < numExtremals)
            throw new InvalidOperationException(
                "Grid is too small for the specified number of taps and bands.");

        // Initialize extremal frequency indices uniformly on the grid
        int[] extremalIndices = new int[numExtremals + 1]; // extra slot for search
        InitializeExtremals(extremalIndices, numExtremals, gridSize);

        // Remez exchange iteration
        double[] x = new double[numExtremals];
        double[] y = new double[numExtremals];
        double[] ad = new double[numExtremals];
        double[] errorOnGrid = new double[gridSize];

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            // Step 1: Get cosine values at extremal frequencies
            for (int i = 0; i < numExtremals; i++)
            {
                x[i] = Math.Cos(2.0 * Math.PI * grid[extremalIndices[i]]);
            }

            // Step 2: Compute Lagrange interpolation weights (barycentric)
            ComputeBarycentricWeights(ad, x, numExtremals);

            // Step 3: Compute delta (the equiripple deviation)
            double delta = ComputeDelta(ad, x, y, desiredOnGrid, weightOnGrid,
                extremalIndices, numExtremals);

            // Step 4: Compute desired response on extremal set adjusted by delta
            for (int i = 0; i < numExtremals; i++)
            {
                double sign = (i % 2 == 0) ? 1.0 : -1.0;
                y[i] = desiredOnGrid[extremalIndices[i]] - sign * delta / weightOnGrid[extremalIndices[i]];
            }

            // Step 5: Evaluate the error on the entire grid
            for (int i = 0; i < gridSize; i++)
            {
                double cosVal = Math.Cos(2.0 * Math.PI * grid[i]);
                double interpolated = LagrangeInterpolate(cosVal, x, y, ad, numExtremals);
                errorOnGrid[i] = weightOnGrid[i] * (desiredOnGrid[i] - interpolated);
            }

            // Step 6: Find new extremals (Remez exchange)
            int[] newExtremals = FindNewExtremals(errorOnGrid, gridSize, numExtremals);

            // Step 7: Check convergence
            double maxChange = 0.0;
            for (int i = 0; i < numExtremals; i++)
            {
                maxChange = Math.Max(maxChange, Math.Abs(grid[newExtremals[i]] - grid[extremalIndices[i]]));
            }

            Array.Copy(newExtremals, extremalIndices, numExtremals);

            if (maxChange < ConvergenceTolerance)
                break;
        }

        // Final: Compute the filter coefficients from the converged extremals
        for (int i = 0; i < numExtremals; i++)
        {
            x[i] = Math.Cos(2.0 * Math.PI * grid[extremalIndices[i]]);
        }

        ComputeBarycentricWeights(ad, x, numExtremals);
        double finalDelta = ComputeDelta(ad, x, y, desiredOnGrid, weightOnGrid,
            extremalIndices, numExtremals);

        for (int i = 0; i < numExtremals; i++)
        {
            double sign = (i % 2 == 0) ? 1.0 : -1.0;
            y[i] = desiredOnGrid[extremalIndices[i]] - sign * finalDelta / weightOnGrid[extremalIndices[i]];
        }

        return ComputeCoefficients(x, y, ad, numExtremals, numTaps, filterLength, isOddLength);
    }

    /// <summary>
    /// Builds the dense frequency grid across all specified bands.
    /// </summary>
    private static void BuildGrid(int numBands, double[] bands, double[] desired, double[] weights,
        int filterLength, bool isOddLength,
        out double[] grid, out double[] desiredOnGrid, out double[] weightOnGrid, out int gridSize)
    {
        int maxGridSize = GridDensity * filterLength * 2;
        double[] tempGrid = new double[maxGridSize];
        double[] tempDesired = new double[maxGridSize];
        double[] tempWeight = new double[maxGridSize];
        int idx = 0;

        for (int band = 0; band < numBands; band++)
        {
            double fLow = bands[2 * band];
            double fHigh = bands[2 * band + 1];
            double dLow = desired[2 * band];
            double dHigh = desired[2 * band + 1];
            double w = weights[band];

            // For Type II filters (even taps), skip frequency 0.5
            if (!isOddLength && band == numBands - 1 && fHigh > 0.5 - 1e-10)
                fHigh = 0.5 - 1e-6;

            double step = (fHigh - fLow) / (GridDensity * filterLength / numBands);
            if (step <= 0)
                step = 1e-8;

            for (double f = fLow; f <= fHigh + step * 0.5 && idx < maxGridSize; f += step)
            {
                double clampedF = Math.Min(f, fHigh);
                tempGrid[idx] = clampedF;

                // Linearly interpolate the desired response within the band
                double t = (fHigh > fLow) ? (clampedF - fLow) / (fHigh - fLow) : 0.0;
                tempDesired[idx] = dLow + t * (dHigh - dLow);
                tempWeight[idx] = w;
                idx++;
            }
        }

        gridSize = idx;
        grid = new double[gridSize];
        desiredOnGrid = new double[gridSize];
        weightOnGrid = new double[gridSize];

        Array.Copy(tempGrid, grid, gridSize);
        Array.Copy(tempDesired, desiredOnGrid, gridSize);
        Array.Copy(tempWeight, weightOnGrid, gridSize);
    }

    /// <summary>
    /// Initialize extremal indices uniformly across the grid.
    /// </summary>
    private static void InitializeExtremals(int[] extremalIndices, int numExtremals, int gridSize)
    {
        for (int i = 0; i < numExtremals; i++)
        {
            extremalIndices[i] = (int)((long)i * (gridSize - 1) / (numExtremals - 1));
        }
    }

    /// <summary>
    /// Computes barycentric weights for Lagrange interpolation.
    /// </summary>
    private static void ComputeBarycentricWeights(double[] ad, double[] x, int n)
    {
        for (int i = 0; i < n; i++)
        {
            double product = 1.0;
            for (int j = 0; j < n; j++)
            {
                if (i != j)
                {
                    double diff = x[i] - x[j];
                    if (Math.Abs(diff) < 1e-15)
                        diff = 1e-15;
                    product *= diff;
                }
            }
            ad[i] = 1.0 / product;
        }
    }

    /// <summary>
    /// Computes the equiripple deviation delta from the current extremal set.
    /// </summary>
    private static double ComputeDelta(double[] ad, double[] x, double[] y,
        double[] desiredOnGrid, double[] weightOnGrid, int[] extremalIndices, int numExtremals)
    {
        double numerator = 0.0;
        double denominator = 0.0;

        for (int i = 0; i < numExtremals; i++)
        {
            int gridIdx = extremalIndices[i];
            double sign = (i % 2 == 0) ? 1.0 : -1.0;
            numerator += ad[i] * desiredOnGrid[gridIdx];
            denominator += sign * ad[i] / weightOnGrid[gridIdx];
        }

        return denominator != 0.0 ? numerator / denominator : 0.0;
    }

    /// <summary>
    /// Lagrange interpolation at a given point using barycentric form.
    /// </summary>
    private static double LagrangeInterpolate(double freq, double[] x, double[] y,
        double[] ad, int n)
    {
        double numerator = 0.0;
        double denominator = 0.0;

        for (int i = 0; i < n; i++)
        {
            double diff = freq - x[i];
            if (Math.Abs(diff) < 1e-12)
                return y[i];

            double term = ad[i] / diff;
            numerator += term * y[i];
            denominator += term;
        }

        return denominator != 0.0 ? numerator / denominator : 0.0;
    }

    /// <summary>
    /// Finds new extremal indices from the weighted error function.
    /// Uses alternation theorem: finds local extrema with alternating signs.
    /// </summary>
    private static int[] FindNewExtremals(double[] error, int gridSize, int numExtremals)
    {
        // Collect all local extrema
        List<int> localExtrema = new();

        // Check first point
        if (gridSize > 1 && Math.Abs(error[0]) >= Math.Abs(error[1]))
            localExtrema.Add(0);

        // Check interior points
        for (int i = 1; i < gridSize - 1; i++)
        {
            if ((error[i] >= error[i - 1] && error[i] >= error[i + 1]) ||
                (error[i] <= error[i - 1] && error[i] <= error[i + 1]))
            {
                localExtrema.Add(i);
            }
        }

        // Check last point
        if (gridSize > 1 && Math.Abs(error[gridSize - 1]) >= Math.Abs(error[gridSize - 2]))
            localExtrema.Add(gridSize - 1);

        // Select numExtremals points with alternating signs and maximum |error|
        int[] result = SelectAlternatingExtremals(error, localExtrema, numExtremals, gridSize);

        return result;
    }

    /// <summary>
    /// Selects extremals with alternating error signs from the local extrema list.
    /// </summary>
    private static int[] SelectAlternatingExtremals(double[] error, List<int> localExtrema,
        int numExtremals, int gridSize)
    {
        int[] selected = new int[numExtremals];

        if (localExtrema.Count < numExtremals)
        {
            // Fall back to uniform distribution if not enough extrema found
            for (int i = 0; i < numExtremals; i++)
            {
                selected[i] = (int)((long)i * (gridSize - 1) / (numExtremals - 1));
            }
            return selected;
        }

        // Greedy selection: pick extrema with alternating signs
        selected[0] = localExtrema[0];
        int selectedCount = 1;
        int searchStart = 1;

        while (selectedCount < numExtremals && searchStart < localExtrema.Count)
        {
            bool needPositive = error[selected[selectedCount - 1]] < 0;

            int bestIdx = -1;
            double bestAbsError = -1.0;

            // Find the best next extremum with the correct sign
            for (int j = searchStart; j < localExtrema.Count; j++)
            {
                int gridIdx = localExtrema[j];
                bool isPositive = error[gridIdx] > 0;

                if (isPositive == needPositive)
                {
                    if (Math.Abs(error[gridIdx]) > bestAbsError)
                    {
                        bestAbsError = Math.Abs(error[gridIdx]);
                        bestIdx = j;
                    }
                    // Only look at consecutive same-sign extrema
                    if (j + 1 < localExtrema.Count &&
                        (error[localExtrema[j + 1]] > 0) != isPositive)
                    {
                        break;
                    }
                }
                else if (bestIdx >= 0)
                {
                    break; // Sign changed and we already have a candidate
                }
            }

            if (bestIdx < 0)
            {
                // Could not find alternating extremum, try skipping
                searchStart++;
                continue;
            }

            selected[selectedCount] = localExtrema[bestIdx];
            selectedCount++;
            searchStart = bestIdx + 1;
        }

        // If we didn't find enough alternating extremals, pad with uniform spacing
        if (selectedCount < numExtremals)
        {
            for (int i = selectedCount; i < numExtremals; i++)
            {
                selected[i] = (int)((long)i * (gridSize - 1) / (numExtremals - 1));
            }
        }

        // Sort selected indices
        Array.Sort(selected, 0, numExtremals);

        return selected;
    }

    /// <summary>
    /// Computes the final filter coefficients from the converged Lagrange interpolation.
    /// </summary>
    private static double[] ComputeCoefficients(double[] x, double[] y, double[] ad,
        int numExtremals, int numTaps, int filterLength, bool isOddLength)
    {
        // Evaluate the frequency response on a dense grid using inverse DFT
        int dftSize = Math.Max(numTaps * 4, 512);
        if (dftSize < numTaps)
            dftSize = numTaps;

        // Make dftSize a power of 2 for convenience (not strictly required)
        int pow2 = 1;
        while (pow2 < dftSize) pow2 *= 2;
        dftSize = pow2;

        double[] response = new double[dftSize];

        // Evaluate the polynomial on a uniform frequency grid [0, 0.5]
        for (int i = 0; i <= dftSize / 2; i++)
        {
            double freq = (double)i / dftSize;
            double cosVal = Math.Cos(2.0 * Math.PI * freq);
            response[i] = LagrangeInterpolate(cosVal, x, y, ad, numExtremals);
        }

        // Use the symmetry for inverse DFT
        double[] coefficients;

        if (isOddLength)
        {
            // Type I: symmetric, odd length
            coefficients = new double[numTaps];
            int M = (numTaps - 1) / 2;

            // Inverse DFT to get coefficients
            double[] a = new double[M + 1];
            for (int i = 0; i <= M; i++)
            {
                double sum = response[0];
                for (int k = 1; k <= dftSize / 2; k++)
                {
                    double freq = (double)k / dftSize;
                    double cosVal = Math.Cos(2.0 * Math.PI * freq);
                    double val = LagrangeInterpolate(cosVal, x, y, ad, numExtremals);
                    double mult = (k == dftSize / 2) ? 1.0 : 2.0;
                    sum += mult * val * Math.Cos(2.0 * Math.PI * k * i / dftSize);
                }
                a[i] = sum / dftSize;
            }

            // Build symmetric coefficients
            for (int i = 0; i <= M; i++)
            {
                coefficients[M + i] = a[i];
                coefficients[M - i] = a[i];
            }
        }
        else
        {
            // Type II: symmetric, even length
            coefficients = new double[numTaps];
            int M = numTaps / 2;

            double[] a = new double[M];
            for (int i = 0; i < M; i++)
            {
                double sum = 0.0;
                for (int k = 0; k <= dftSize / 2; k++)
                {
                    double freq = (double)k / dftSize;
                    double cosVal = Math.Cos(2.0 * Math.PI * freq);
                    double val = LagrangeInterpolate(cosVal, x, y, ad, numExtremals);
                    double mult = (k == 0 || k == dftSize / 2) ? 1.0 : 2.0;
                    sum += mult * val * Math.Cos(2.0 * Math.PI * k * (i + 0.5) / dftSize);
                }
                a[i] = sum / dftSize;
            }

            // Build symmetric coefficients
            for (int i = 0; i < M; i++)
            {
                coefficients[M + i] = a[i];
                coefficients[M - 1 - i] = a[i];
            }
        }

        return coefficients;
    }
}
