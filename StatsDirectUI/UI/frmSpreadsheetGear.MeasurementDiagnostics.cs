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
    bool HasVariation, bool HasPotentialOutlier, bool HasSkewSignal);

internal partial class frmSpreadsheetGear
{
    internal PairedDifferenceDiagnostics CheckSelectedPairedDifferences()
    {
        var differences = new MeasurementAccumulator();
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
        return differences.SummarizePaired(minimum, maximum);
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

        internal PairedDifferenceDiagnostics SummarizePaired(double minimum, double maximum)
        {
            bool hasVariation = maximum > minimum;
            if (!hasVariation || count > MeasurementDistributionDiagnostics.LargeSampleThreshold)
                return new PairedDifferenceDiagnostics(count, null, null, hasVariation, false, false);

            var sample = smallSample.ToArray();
            var normality = Parametric.ShapiroWilkDiagnostics(sample);
            var (outlier, skew) = PairedShapeSignals(sample, minimum, maximum);
            return new PairedDifferenceDiagnostics(count, normality?.W, normality?.P, hasVariation, outlier, skew);
        }

        private static (bool Outlier, bool Skew) PairedShapeSignals(double[] sample, double minimum, double maximum)
        {
            if (sample.Length < 4) return (false, false);

            // The fences and skewness are invariant to scale. Scale exceptionally wide differences before subtracting endpoints.
            if (double.IsInfinity(maximum - minimum))
            {
                double scale = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
                for (int i = 0; i < sample.Length; i++) sample[i] /= scale;
            }

            Array.Sort(sample);
            double lower = InterpolatedQuartile(sample, 0.25);
            double upper = InterpolatedQuartile(sample, 0.75);
            double iqr = upper - lower;
            bool outlier = (lower - sample[0]) / 1.5 > iqr || (sample[^1] - upper) / 1.5 > iqr;

            Parametric.normality_sk(sample, 0, sample.Length, out _, out _, out double skewness, out _, out _, out _, out _, out _, out _, out _);
            // Use a sizeable observed moment skew as a warning. Shape and normality remain matters of judgement.
            bool skew = !RunRecord.Missing(skewness) && Math.Abs(skewness) >= 1.0;
            return (outlier, skew);
        }

        private static double InterpolatedQuartile(double[] sorted, double fraction)
        {
            // Match the StatsDirect boxplot's conventional p(n + 1) quantile, using zero-based positions here.
            double position = Math.Clamp(fraction * (sorted.Length + 1) - 1.0, 0.0, sorted.Length - 1);
            int lower = (int)position;
            double remainder = position - lower;
            return remainder == 0.0 ? sorted[lower] : sorted[lower] + remainder * (sorted[lower + 1] - sorted[lower]);
        }
    }
}
