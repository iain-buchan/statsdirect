using SpreadsheetGear;
using StatsDirect.Builtins;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;

namespace StatsDirect.UI;

internal sealed record MeasurementDistributionDiagnostics(int Count, double? ShapiroWilkW, double? ShapiroWilkP)
{
    internal const int LargeSampleThreshold = 100;
}

internal sealed record IndependentSampleDiagnostics(
    MeasurementDistributionDiagnostics First, MeasurementDistributionDiagnostics Second);

internal sealed record PairedDifferenceDiagnostics(
    int Count, double? ShapiroWilkW, double? ShapiroWilkP,
    bool HasVariation, bool HasPotentialOutlier, bool HasSkewSignal, PairedDifferenceShape Shape = null);

internal sealed record PairedDifferenceShape(
    double LowerQuartile, double UpperQuartile, double? Skewness, double? ExcessKurtosis, int FarOutCount)
{
    internal bool HasHeavyTails => ExcessKurtosis >= 2.0;
}

internal partial class frmSpreadsheetGear
{
    internal PairedDifferenceDiagnostics CheckSelectedPairedDifferences()
    {
        var differences = new List<double>();
        double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
        ReadSelectedMeasurementRows("Select two adjacent, visible columns of matched measurements in one worksheet range.", (before, after) =>
        {
            if (RunRecord.Missing(before) || RunRecord.Missing(after)) return;
            double difference = before - after;
            if (RunRecord.Missing(difference))
                throw new InvalidOperationException("A paired difference is too large to check. Rescale the measurements and try again.");
            differences.Add(difference);
            minimum = Math.Min(minimum, difference);
            maximum = Math.Max(maximum, difference);
        });
        return SummarizePaired(differences, minimum, maximum);
    }

    internal IndependentSampleDiagnostics CheckSelectedIndependentSamples()
    {
        var first = new MeasurementAccumulator();
        var second = new MeasurementAccumulator();
        ReadSelectedMeasurementRows("Select the two adjacent, visible sample columns in one worksheet range.", (left, right) =>
        {
            if (!RunRecord.Missing(left)) first.Add(left);
            if (!RunRecord.Missing(right)) second.Add(right);
        });
        return new IndependentSampleDiagnostics(first.Summarize(), second.Summarize());
    }

    private void ReadSelectedMeasurementRows(string selectionError, Action<double, double> collect) => workbookView.WithLock(() =>
    {
        CheckWorksheetAnalysisReady();
        IRange selection = workbookView.RangeSelection;
        if (selection.AreaCount != 1 || selection.ColumnCount != 2
            || IsHiddenColumn(selection.Column) || IsHiddenColumn(selection.Column + 1))
            throw new InvalidOperationException(selectionError);

        IRange used = workbookView.ActiveWorksheet.UsedRange;
        int first = Math.Max(selection.Row, used.Row);
        int last = Math.Min(selection.Row + selection.RowCount, used.Row + used.RowCount) - 1;
        IGetCells grid = this;

        // Both columns use the native numeric reader and the same visible row bounds.
        // A missing value in either column cannot shift the other column for paired calculations.
        const int batchSize = 8192;
        for (int start = first; start <= last; start += batchSize)
        {
            int end = Math.Min(last, start + batchSize - 1);
            var (left, rows) = grid.GetCellValues(selection.Column, start, end);
            var (right, _) = grid.GetCellValues(selection.Column + 1, start, end);
            for (int row = 0; row < rows; row++)
                collect(left[row], right[row]);
        }
    });

    private sealed class MeasurementAccumulator
    {
        private readonly List<double> smallSample = new(MeasurementDistributionDiagnostics.LargeSampleThreshold);
        private int count;

        internal void Add(double value)
        {
            count++;
            if (count <= MeasurementDistributionDiagnostics.LargeSampleThreshold)
                smallSample.Add(value);
        }

        internal MeasurementDistributionDiagnostics Summarize()
        {
            var normality = count <= MeasurementDistributionDiagnostics.LargeSampleThreshold
                ? Parametric.ShapiroWilkDiagnostics(smallSample.ToArray()) : null;
            return new MeasurementDistributionDiagnostics(count, normality?.W, normality?.P);
        }
    }

    private static PairedDifferenceDiagnostics SummarizePaired(List<double> differences, double minimum, double maximum)
    {
        int count = differences.Count;
        bool hasVariation = maximum > minimum;
        if (!hasVariation)
            return new PairedDifferenceDiagnostics(count, null, null, false, false, false);

        bool large = count > MeasurementDistributionDiagnostics.LargeSampleThreshold;
        var sample = differences.ToArray();
        var normality = large ? null : Parametric.ShapiroWilkDiagnostics(sample);
        if (count < 4)
            return new PairedDifferenceDiagnostics(count, normality?.W, normality?.P, true, false, false);

        Array.Sort(sample);
        double lowerQuartile = InterpolatedQuartile(sample, 0.25);
        double upperQuartile = InterpolatedQuartile(sample, 0.75);
        double lower = lowerQuartile, upper = upperQuartile;

        // Rescale extreme data only for the comparisons and moments. Keep the reported quartiles in the original units.
        double span = maximum - minimum;
        if (double.IsInfinity(span))
        {
            double scale = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
            for (int i = 0; i < sample.Length; i++) sample[i] /= scale;
            lower = InterpolatedQuartile(sample, 0.25);
            upper = InterpolatedQuartile(sample, 0.75);
        }
        else if (large && double.IsSubnormal(span))
        {
            for (int i = 0; i < sample.Length; i++) sample[i] = (sample[i] - minimum) / span;
            lower = InterpolatedQuartile(sample, 0.25);
            upper = InterpolatedQuartile(sample, 0.75);
        }
        double iqr = upper - lower;

        Parametric.normality_sk(sample, 0, count, out _, out _, out double skewness, out double kurtosis, out _, out _, out _, out _, out _, out _);
        bool skew = !RunRecord.Missing(skewness) && Math.Abs(skewness) >= 1.0;
        if (!large)
        {
            bool outlier = (lower - sample[0]) / 1.5 > iqr || (sample[^1] - upper) / 1.5 > iqr;
            return new PairedDifferenceDiagnostics(count, normality?.W, normality?.P, true, outlier, skew);
        }

        int left = 0, right = count - 1;
        while (left <= right && (lower - sample[left]) / 3.0 > iqr) left++;
        while (right >= left && (sample[right] - upper) / 3.0 > iqr) right--;
        int farOut = left + (count - 1 - right);
        var shape = new PairedDifferenceShape(lowerQuartile, upperQuartile, RunRecord.Missing(skewness) ? null : skewness,
            RunRecord.Missing(kurtosis) ? null : kurtosis - 3.0, farOut);
        // Require at least 0.1% of pairs outside the outer fences so the warning does not depend on seeing just one point in a very large sample.
        return new PairedDifferenceDiagnostics(count, null, null, true, farOut * 1000L >= count, skew, shape);
    }

    private static double InterpolatedQuartile(double[] sorted, double fraction)
    {
        // Match the StatsDirect boxplot's conventional p(n + 1) quantile, using zero-based positions here.
        double position = Math.Clamp(fraction * (sorted.Length + 1) - 1.0, 0.0, sorted.Length - 1);
        int lower = (int)position;
        double remainder = position - lower;
        double first = sorted[lower];
        if (remainder == 0.0) return first;
        double next = sorted[lower + 1];
        double gap = next - first;
        return double.IsInfinity(gap) ? (1.0 - remainder) * first + remainder * next : first + remainder * gap;
    }
}
