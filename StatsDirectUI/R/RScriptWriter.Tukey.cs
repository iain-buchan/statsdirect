using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StatsDirect.R
{
    public static partial class RScriptWriter
    {
        /// <summary>Write the chosen Tukey plot as the short native R call a user can edit.</summary>
        private static void AppendTukeyBoxPlot(StringBuilder script, JsonElement settings, List<Column> columns)
        {
            bool Flag(string name, bool fallback = false) => settings.TryGetProperty(name, out JsonElement v) &&
                v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;
            double? Numeric(string name) => settings.TryGetProperty(name, out JsonElement v) &&
                v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out double n) && double.IsFinite(n) ? n : null;
            string R(double n) => n.ToString("R", inv);
            string Values(IEnumerable<double> values) => "c(" + string.Join(", ", values.Select(R)) + ")";
            string Wrap(string text, int width)
            {
                List<string> lines = new();
                foreach (string paragraph in (text ?? "").Replace("\r", "").Split('\n'))
                {
                    string line = "";
                    foreach (string word in Regex.Split(paragraph.Trim(), @"\s+").Where(word => word.Length > 0))
                    {
                        if (line.Length > 0 && line.Length + word.Length + 1 > width) { lines.Add(line); line = ""; }
                        line += (line.Length == 0 ? "" : " ") + word;
                    }
                    lines.Add(line);
                }
                return string.Join("\n", lines);
            }

            bool horizontal = Flag("horizontal", true), inner = Flag("innerFence", true), outer = Flag("outerFence", true);
            int count = columns.Count, side = horizontal ? 1 : 2, scale = (int)(Numeric("valueScale") ?? 0);
            string positions = horizontal && count > 1 ? count + ":1" : count > 1 ? "1:" + count : "1";
            string valueAxis = horizontal ? "x" : "y";
            string main = Text(settings, "title") ?? "Tukey hinges box plot";
            string valueTitle = Text(settings, "axisTitle") ?? "Value";
            string oldCaption = "Tukey hinges: min < lower hinge < median" + (Flag("markMeanAndMedian") ? " & mean(x)" : "") + " > upper hinge > max" +
                (inner && outer ? ", fences (1.5 & 3.0 IQR)" : inner ? ", fence (1.5 IQR)" : outer ? ", fence (3.0 IQR)" : "");
            if (valueTitle == oldCaption)
                valueTitle = "Tukey hinges" + (Flag("markMeanAndMedian") ? " and mean" : "") +
                    (inner && outer ? " (1.5 and 3 IQR fences)" : inner ? " (1.5 IQR fence)" : outer ? " (3 IQR fence)" : "");
            string[] labels = settings.TryGetProperty("seriesTitles", out JsonElement titles) && titles.ValueKind == JsonValueKind.Array &&
                titles.GetArrayLength() == count ? titles.EnumerateArray().Select(t => t.GetString() ?? "").ToArray()
                : columns.Select(c => c.Title).ToArray();
            string[] displayedLabels = labels.Select(label => Wrap(label, horizontal ? 24 : Math.Max(8, 60 / Math.Max(1, count)))).ToArray();
            int Lines(string text) => text.Count(c => c == '\n') + 1;
            int bottom = horizontal ? 4 + Lines(valueTitle) : 3 + displayedLabels.Select(Lines).DefaultIfEmpty(1).Max();
            int left = horizontal ? 8 : 4 + Lines(valueTitle);
            script.Append("\nold_par <- par(mar = c(").Append(bottom).Append(", ").Append(left).Append(", ")
                .Append(2 + Lines(main)).Append(", 1), family = \"sans\",\n")
                .Append("  cex.main = 12 / par(\"ps\"), cex.lab = 10 / par(\"ps\"), cex.axis = 9 / par(\"ps\"))\n");
            script.Append("main <- ").Append(Quote(main)).Append("\nvalue_title <- ").Append(Quote(valueTitle)).Append('\n');

            List<string> arguments = new() { "mets", "horizontal = " + (horizontal ? "TRUE" : "FALSE") };
            if (horizontal && count > 1) arguments.Add("at = " + positions);
            if (!displayedLabels.SequenceEqual(columns.Select(c => c.Title)))
                arguments.Add("names = c(" + string.Join(", ", displayedLabels.Select(Quote)) + ")");
            if (!inner) arguments.Add("range = " + (outer ? "3" : "0")); // 1.5 is boxplot's default.
            if (outer && !inner) arguments.Add("outpch = 16");
            arguments.Add("main = main");
            arguments.Add((horizontal ? "xlab = " : "ylab = ") + "value_title");
            if (main.Length > 0)
                arguments.Add("cex.main = min(12 / par(\"ps\"), 0.95 * par(\"pin\")[1] / strwidth(main, units = \"inches\", font = 2))");
            if (valueTitle.Length > 0)
                arguments.Add("cex.lab = min(10 / par(\"ps\"), 0.95 * par(\"pin\")[" + (horizontal ? 1 : 2) + "] / strwidth(value_title, units = \"inches\"))");
            double? minimum = Numeric("valueMinimum"), maximum = Numeric("valueMaximum");
            bool limits = minimum.HasValue && maximum.HasValue && minimum < maximum;
            if (limits)
            {
                arguments.Add("ylim = " + Values(new[] { minimum.Value, maximum.Value }));
                arguments.Add(valueAxis + "axs = \"i\"");
            }
            if (scale is 1 or 2) arguments.Add("log = \"" + valueAxis + "\"");

            string ticks = null;
            double? interval = Numeric("valueInterval"), first = Numeric("valueFirstTick");
            if (limits && interval > 0 && first.HasValue)
            {
                double from = Math.Ceiling((minimum.Value - first.Value) / interval.Value - 1e-10);
                double to = Math.Floor((maximum.Value - first.Value) / interval.Value + 1e-10);
                if (double.IsFinite(from) && double.IsFinite(to) && to >= from && to - from <= 10000)
                    ticks = "seq(" + R(first.Value + from * interval.Value) + ", " + R(first.Value + to * interval.Value) + ", by = " + R(interval.Value) + ")";
            }
            if (ticks == null && scale == 1 && limits && minimum > 0)
            {
                int from = (int)Math.Ceiling(Math.Log2(minimum.Value)), to = (int)Math.Floor(Math.Log2(maximum.Value));
                if (to >= from) ticks = "2^(" + from + ":" + to + ")";
            }
            if (ticks != null) arguments.Add(valueAxis + "axt = \"n\"");
            arguments.Add("las = 1");
            if (!Flag("boxAxes")) arguments.Add("frame.plot = FALSE");
            arguments.Add("boxfill = NA");
            arguments.Add("boxwex = 0.6");
            arguments.Add("medlwd = 1");
            arguments.Add("whisklty = 1");
            double lineWidth = Numeric("lineWidth") ?? 1;
            // Older/headless records can contain the uninitialised zero. R rejects it; use its normal width in that case.
            if (lineWidth > 0 && lineWidth != 1) arguments.Add("lwd = " + R(lineWidth));
            string line = "box_plot <- boxplot(";
            foreach (string argument in arguments)
            {
                if (line.Length > 20 && line.Length + argument.Length > 100) { script.Append(line.TrimEnd()).Append('\n'); line = "  "; }
                line += argument + ", ";
            }
            script.Append(line.TrimEnd(' ', ',')).Append(")\n");
            if (ticks != null)
            {
                script.Append("ticks <- ").Append(ticks).Append('\n');
                script.Append("axis(").Append(side).Append(", at = ticks, labels = format(ticks, trim = TRUE, drop0trailing = TRUE), las = 1)\n");
            }
            if (Flag("valueGrid"))
            {
                int style = (int)(Numeric("valueGridStyle") ?? 0);
                script.Append("abline(").Append(horizontal ? "v" : "h").Append(" = ").Append(ticks == null ? "axTicks(" + side + ")" : "ticks")
                    .Append(", col = \"grey80\", lty = ").Append(style == 4 ? "\"311111\"" : (style + 1).ToString(inv)).Append(")\n");
            }
            double? marker = Numeric("valueMarker");
            if (marker.HasValue && (scale is not (1 or 2) || marker > 0))
                script.Append("abline(").Append(horizontal ? "v" : "h").Append(" = ").Append(R(marker.Value)).Append(")\n");

            // Native boxplot already draws the inner-fence outliers. Add filled marks only when a selected outer fence
            // actually distinguishes observations in these data; an ordinary Tukey chart needs no custom outlier code.
            bool HasOuterOutlier(Column column)
            {
                double[] x = column.Values.OfType<double>().Where(double.IsFinite).OrderBy(n => n).ToArray();
                if (x.Length == 0) return false;
                double hinge = Math.Floor((x.Length + 3) / 2d) / 2;
                double At(double index) => (x[(int)Math.Floor(index) - 1] + x[(int)Math.Ceiling(index) - 1]) / 2;
                double low = At(hinge), high = At(x.Length + 1 - hinge), spread = high - low;
                return x[0] < low - 3 * spread || x[^1] > high + 3 * spread;
            }
            if (inner && outer && columns.Any(HasOuterOutlier))
            {
                script.Append("# Filled points distinguish observations beyond the selected 3-IQR outer fences.\n")
                    .Append("hinges <- box_plot$stats[c(2, 4), box_plot$group, drop = FALSE]\n")
                    .Append("far <- box_plot$out < hinges[1, ] - 3 * (hinges[2, ] - hinges[1, ]) |\n")
                    .Append("       box_plot$out > hinges[2, ] + 3 * (hinges[2, ] - hinges[1, ])\n");
                string values = "box_plot$out[far]", at = "(" + positions + ")[box_plot$group[far]]";
                script.Append("points(").Append(horizontal ? values + ", " + at : at + ", " + values).Append(", pch = 16)\n");
            }
            if (Flag("markMeanAndMedian"))
            {
                script.Append("# The line marks the median; crosses mark the means.\n");
                string means = "vapply(mets, mean, numeric(1), na.rm = TRUE)";
                script.Append("points(").Append(horizontal ? means + ", " + positions : positions + ", " + means).Append(", pch = 4)\n");
            }
            script.Append("par(old_par)\n");
        }
    }
}
