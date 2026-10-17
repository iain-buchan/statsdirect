using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StatsDirect.Charting;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // Private example-workbook values and isolated output folders keep these checks out of the user's R folder.
    private static void CheckBoxWhisker()
    {
        RRecipes.Folder = Metadata("Recipes");
        CheckBoxWhiskerDataOrder();
        CheckBoxWhiskerAxes();
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-box-r-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        bool passed = false;
        try
        {
            double[] michelson = MichelsonValues();
            Check(michelson.Length == 100 && michelson[0] == 299.85 && michelson[^1] == 299.87,
                "the box plot regression uses the actual selected Michelson column from Parametric, rather than the help's cell-line data");
            const string title = "Michelson — speed of light \"October\"";
            const string label = "Michelson measurements";
            const string axis = "Speed (thousands of km/s)";
            BoxWhiskerOptions chosen = null;
            var (run, _) = Run("BoxWhiskerPlot", new() { ["data"] = SheetFrame(Workbook, "Parametric", 6, 0, ("Michelson", michelson)) }, amend: fillable =>
            {
                var definition = (ChartDefinition)fillable;
                chosen = (BoxWhiskerOptions)definition.ChartOptions;
                chosen.Method = BoxWhiskerOptions.BoxWhiskerMethod.MeanConfidenceIntervalRange;
                chosen.Cco = 0.9;
                chosen.UseInnerFence = false;
                chosen.UseOuterFence = true;
                chosen.MarkMeanAndMedian = true;
                chosen.Orientation = ChartOrientation.Vertical;
                chosen.Title = title;
                chosen.XAxisTitle = axis;
                chosen.SeriesTitles[0] = label;
                chosen.ShouldBoxAxes = true;
                chosen.TitleFontDescriptor = new FontDescriptor("Arial", 3, 18);
                chosen.AxisTitleFontDescriptor = new FontDescriptor("Arial", 1, 12);
                chosen.AxisLabelFontDescriptor = new FontDescriptor("Arial", 2, 9);
                definition.ScaleParameters.X.ScaleType = ScaleType.Linear;
                definition.ScaleParameters.X.AxisScale = new LinearAxisScale(299.62, 300.07, 299.5, 300.2, 7);
                definition.ScaleParameters.X.HasGridLines = true;
                definition.ScaleParameters.X.GridLineDashStyle = DashStyleDescriptor.Dot;
                definition.ScaleParameters.X.MarkerLineValue = 299.85;
            });
            JsonElement record = Record(run);
            JsonElement chart = record.GetProperty("parameters").GetProperty("boxWhisker");
            Check(chart.GetProperty("method").GetInt32() == 3 && chart.GetProperty("confidence").GetDouble() == 0.9
                  && !chart.GetProperty("innerFence").GetBoolean() && chart.GetProperty("outerFence").GetBoolean()
                  && chart.GetProperty("markMeanAndMedian").GetBoolean() && !chart.GetProperty("horizontal").GetBoolean()
                  && chart.GetProperty("title").GetString() == title && chart.GetProperty("axisTitle").GetString() == axis
                  && chart.GetProperty("seriesTitles")[0].GetString() == label && chart.GetProperty("boxAxes").GetBoolean(),
                "the report's run record captures the final box plot dialog choices, edited title and series label");
            Check(chart.GetProperty("valueMinimum").GetDouble() == 299.5
                  && chart.GetProperty("valueMaximum").GetDouble() == 300.2 && chart.GetProperty("valueGrid").GetBoolean()
                  && chart.GetProperty("valueGridStyle").GetInt32() == (int)DashStyleDescriptor.Dot && chart.GetProperty("valueMarker").GetDouble() == 299.85,
                "the chosen numeric-axis limits, grid and marker survive capture for a vertical box plot");
            RScript script = RScriptWriter.Write(record, null, null);
            Check(script.HasRecipe && !script.DataUnavailable && script.Text.Contains("mets <- list(\"Michelson\" = c(")
                  && !script.Text.Contains("Number of metastases") && !script.Text.Contains("boxplot() of the same columns"),
                "Continue in R binds the selected Michelson values and omits the help's extra comparison charts and title");
            CheckBoxR(script.Text, Path.Combine(folder, "michelson"), michelson, chosen, title, label, axis, "the selected Michelson plot");
            CheckBoxR(script.Text, Path.Combine(folder, "michelson-pdf"), michelson, chosen, title, label, axis,
                "the same selected Michelson plot on Mac's default PDF device", pdf: true);

            const string longTitle = "Michelson measurements of the speed of light: selected observations and their confidence interval";
            const string longLabel = "Michelson measurements recorded in the Parametric worksheet";
            const string longAxis = "Speed of light in thousands of kilometres per second, with the selected confidence interval and fences";
            JsonNode longText = JsonNode.Parse(record.GetRawText());
            longText["parameters"]["boxWhisker"]["title"] = longTitle;
            longText["parameters"]["boxWhisker"]["axisTitle"] = longAxis;
            longText["parameters"]["boxWhisker"]["seriesTitles"] = new JsonArray(longLabel);
            string longScript = RScriptWriter.Write(JsonDocument.Parse(longText.ToJsonString()).RootElement, null, null).Text;
            CheckBoxR(longScript, Path.Combine(folder, "long-text-svg"), michelson, chosen, longTitle, longLabel, longAxis,
                "long selected text on one seven-inch SVG chart");
            CheckBoxR(longScript, Path.Combine(folder, "long-text-pdf"), michelson, chosen, longTitle, longLabel, longAxis,
                "long selected text on one seven-inch PDF page", pdf: true);
            BoxWhiskerOptions automatic = null;
            var (automaticRun, _) = Run("BoxWhiskerPlot", new() { ["data"] = SheetFrame(Workbook, "Parametric", 6, 0, ("Michelson", (double[])michelson.Clone())) }, amend: fillable =>
            {
                automatic = (BoxWhiskerOptions)((ChartDefinition)fillable).ChartOptions;
                automatic.Method = BoxWhiskerOptions.BoxWhiskerMethod.MeanConfidenceIntervalRange;
                automatic.Cco = 0.99;
                automatic.UseInnerFence = true;
                automatic.UseOuterFence = true;
                automatic.Orientation = ChartOrientation.Horizontal;
                automatic.Title = "Michelson 99% confidence interval";
                automatic.SetDefaultXAxisTitle();
            });
            Check(automatic.XAxisTitle.Contains('\u00b1') && !automatic.XAxisTitle.Contains('?'),
                "the automatic confidence-interval axis title uses the intended plus/minus symbol");
            CheckBoxR(RScriptWriter.Write(Record(automaticRun), null, null).Text, Path.Combine(folder, "automatic-ci99-pdf"),
                michelson, automatic, automatic.Title, "Michelson", automatic.XAxisTitle,
                "the automatic 99% confidence-interval title on one seven-inch PDF page", pdf: true);

            // A saved record is self-contained; later changes to the live chart cannot rewrite the earlier report's settings.
            string recordPath = Path.Combine(folder, "saved-run.json");
            File.WriteAllText(recordPath, record.GetRawText());
            chosen.Title = "Later chart";
            chosen.Cco = 0.99;
            chosen.SeriesTitles[0] = "Later label";
            JsonElement reopened = JsonDocument.Parse(File.ReadAllText(recordPath)).RootElement.Clone();
            Check(RScriptWriter.Write(reopened, null, null).Text == script.Text,
                "saving and reopening the run preserves its R script after the live chart options change");

            // Changing a plain R setting must recalculate the chart, rather than replaying stored coordinates.
            JsonNode edited = JsonNode.Parse(record.GetRawText());
            edited["parameters"]["boxWhisker"]["confidence"] = 0.99;
            string changed = RScriptWriter.Write(JsonDocument.Parse(edited.ToJsonString()).RootElement, null, null).Text;
            chosen.Title = title;
            chosen.SeriesTitles[0] = label;
            CheckBoxR(changed, Path.Combine(folder, "edited-confidence"), michelson, chosen, title, label, axis,
                "the saved plot after changing its confidence setting");

            double[] sparse = { -150, -20, -3, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 40, 120, 900 };
            void ChartCase(BoxWhiskerOptions.BoxWhiskerMethod method, int fences, double[] data, string caseName)
            {
                    BoxWhiskerOptions options = null;
                    string caption = caseName + ": " + method + ", fences " + fences;
                    bool horizontal = fences % 2 == 0;
                    bool mark = fences >= 2;
                    (run, _) = Run("BoxWhiskerPlot", new() { ["data"] = SheetFrame(Workbook, "Synthetic", 0, 0, (caseName, data)) }, amend: fillable =>
                    {
                        options = (BoxWhiskerOptions)((ChartDefinition)fillable).ChartOptions;
                        options.Method = method;
                        options.UseInnerFence = (fences & 1) != 0;
                        options.UseOuterFence = (fences & 2) != 0;
                        options.MarkMeanAndMedian = mark;
                        options.Cco = 0.9;
                        options.Orientation = horizontal ? ChartOrientation.Horizontal : ChartOrientation.Vertical;
                        options.Title = caption;
                        options.XAxisTitle = "Chosen units";
                        options.SeriesTitles[0] = "Selected group";
                    });
                    CheckBoxR(RScriptWriter.Write(Record(run), null, null).Text, Path.Combine(folder, caseName + "-method-" + (int)method + "-fences-" + fences),
                        data, options, caption, "Selected group", "Chosen units", caption);
            }
            foreach (BoxWhiskerOptions.BoxWhiskerMethod method in Enum.GetValues<BoxWhiskerOptions.BoxWhiskerMethod>())
            {
                for (int fences = 0; fences < 4; fences++) ChartCase(method, fences, sparse, "Sparse");
                ChartCase(method, 3, new[] { 7.0, 7, 7, 7, 7 }, "Constant");
            }
            double[] zeroIqr = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 100 };
            for (int fences = 0; fences < 4; fences++)
                ChartCase(BoxWhiskerOptions.BoxWhiskerMethod.MedianQuartilesRange, fences, zeroIqr, "ZeroIqr");

            BoxWhiskerOptions grouped = null;
            (run, _) = Run("BoxWhiskerPlot", new() { ["data"] = SheetFrame(Workbook, "Selected groups", 2, 0, ("Original first", sparse), ("Original second", michelson)) }, amend: fillable =>
            {
                grouped = (BoxWhiskerOptions)((ChartDefinition)fillable).ChartOptions;
                grouped.Method = BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary;
                grouped.Cco = 0.95;
                grouped.MarkMeanAndMedian = true;
                grouped.Orientation = ChartOrientation.Horizontal;
                grouped.Title = "Two selected columns";
                grouped.XAxisTitle = "Data units";
                grouped.SeriesTitles = new[] { "Edited first label", "Edited second label" };
            });
            CheckBoxR(RScriptWriter.Write(Record(run), null, null).Text, Path.Combine(folder, "two-series"),
                new[] { sparse, michelson }, grouped, grouped.Title, grouped.SeriesTitles, grouped.XAxisTitle,
                "two selected columns with edited labels in their original order");

            // A portable saved record can contain blank cells; empty custom titles are meaningful and must not become example text.
            JsonNode blanks = JsonNode.Parse(record.GetRawText());
            blanks["inputs"] = JsonNode.Parse("""[{"name":"data","prompt":"Data","kind":"entered","columns":[{"title":"Original label","values":[1,null,2,null,3]}]}]""");
            blanks.AsObject().Remove("data");
            blanks.AsObject().Remove("source");
            blanks["parameters"]["boxWhisker"]["title"] = "";
            blanks["parameters"]["boxWhisker"]["axisTitle"] = "";
            blanks["parameters"]["boxWhisker"]["seriesTitles"] = JsonNode.Parse("""[""]""");
            string blankScript = RScriptWriter.Write(JsonDocument.Parse(blanks.ToJsonString()).RootElement, null, null).Text;
            string blankFolder = Path.Combine(folder, "blank-cells-titles");
            string blankOutput = RunBoxR("svg('box-%03d.svg')\n" + blankScript + """

                stopifnot(length(box_summaries) == 1L, box_summaries[[1L]]$n == 3L,
                          identical(box_summaries[[1L]]$x, c(1, 2, 3)),
                          identical(box_title, ""), identical(box_axis_title, ""),
                          identical(as.character(box_series_titles), ""))
                dev.off()
                cat("BLANKS VERIFIED\n")
                """, blankFolder);
            Check(blankOutput.Contains("BLANKS VERIFIED") && Directory.GetFiles(blankFolder, "box-*.svg").Length == 1,
                "recorded blank cells are skipped and empty custom chart, axis and series titles remain empty");

            // Older reports have data but no chart options. They must still produce one chart, without inventing a saved choice.
            JsonNode legacy = JsonNode.Parse(record.GetRawText());
            legacy["parameters"].AsObject().Remove("boxWhisker");
            string oldScript = RScriptWriter.Write(JsonDocument.Parse(legacy.ToJsonString()).RootElement, null, null).Text;
            string oldFolder = Path.Combine(folder, "legacy");
            string oldOutput = RunBoxR("svg('box-%03d.svg')\n" + oldScript + "\ndev.off()\ncat('LEGACY', length(box_summaries), '\\n')\n", oldFolder);
            Check(oldOutput.Contains("LEGACY 1") && Directory.GetFiles(oldFolder, "box-*.svg").Length == 1,
                "an older report with no chart settings still draws one chart from its recorded data");
            passed = true;
        }
        finally
        {
            if (passed && Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(folder, true);
            else if (!passed) Console.WriteLine("      box plot failure artifacts: " + folder);
        }
    }

    private static double[] MichelsonValues()
    {
        using ZipArchive workbook = ZipFile.OpenRead(Workbook);
        using Stream sheet = workbook.GetEntry("xl/worksheets/sheet1.xml").Open();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return XDocument.Load(sheet).Descendants(ns + "c")
            .Where(c => (string)c.Attribute("r") is string address && address.StartsWith("G", StringComparison.Ordinal)
                        && int.TryParse(address.AsSpan(1), out int row) && row > 1 && c.Element(ns + "v") != null && (string)c.Attribute("t") != "s")
            .Select(c => double.Parse(c.Element(ns + "v").Value, inv)).ToArray();
    }

    private static double[] NativeBoxSummary(double[] values, BoxWhiskerOptions options)
    {
        Type type = typeof(ChartDefinition).Assembly.GetType("StatsDirect.Charting.Renderer.BoxWhiskerChartRenderer", true);
        object renderer = Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.Instance, null, new object[] { new ChartDefinition(), null }, inv);
        MethodInfo calculate = type.GetMethod("PlotBoxWhiskerCalc", BindingFlags.NonPublic | BindingFlags.Instance);
        object[] args = { new DoubleSeries(values.Order().ToArray(), "test"), options.Method, (1 - options.Cco) / 2,
            null, null, null, null, null, options.UseInnerFence, null, null, options.UseOuterFence, null, null };
        calculate.Invoke(renderer, args);
        return new[] { (double)args[3], (double)args[12], (double)args[4], (double)args[5], (double)args[6], (double)args[7], (double)args[9], (double)args[10] };
    }

    private static string RunBoxR(string script, string folder)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "box-source.R");
        File.WriteAllText(path, script);
        // Codex can inherit a POSIX locale name that Windows R does not recognise. Use this PC's default locale,
        // in this R child only, before parsing the Unicode title; the source encoding is explicit as in the application.
        return RunR("invisible(Sys.setlocale('LC_CTYPE', ''))\nsource(" + RScriptWriter.Quote(path.Replace('\\', '/')) + ", encoding = 'UTF-8')\n", folder);
    }

    private static void CheckBoxR(string script, string folder, double[] values, BoxWhiskerOptions options,
        string title, string label, string axis, string description, bool pdf = false)
        => CheckBoxR(script, folder, new[] { values }, options, title, new[] { label }, axis, description, pdf);

    private static void CheckBoxR(string script, string folder, double[][] values, BoxWhiskerOptions options,
        string title, string[] labels, string axis, string description, bool pdf = false)
    {
        string expected = "list(" + string.Join(",", values.Select(v => "c(" + string.Join(",", NativeBoxSummary(v, options).Select(x => x.ToString("R", inv))) + ")")) + ")";
        string names = "c(" + string.Join(",", labels.Select(RScriptWriter.Quote)) + ")";
        string counts = "c(" + string.Join(",", values.Select(v => v.Length)) + ")";
        string observations = "list(" + string.Join(",", values.Select(v => "c(" + string.Join(",", v.Select(x => x.ToString("R", inv))) + ")")) + ")";
        string preamble = """
            .box_pages <- 0L
            .box_titles <- list()
            .box_axes <- list()
            .box_marks <- list()
            .box_lines <- list()
            .box_boxes <- 0L
            trace('plot.new', where = asNamespace('graphics'), print = FALSE,
                  tracer = quote(.GlobalEnv$.box_pages <- .GlobalEnv$.box_pages + 1L))
            title_trace <- quote(.GlobalEnv$.box_titles[[length(.GlobalEnv$.box_titles) + 1L]] <-
              list(main = main, xlab = xlab, ylab = ylab, details = list(...), settings = par(c("ps", "usr", "lwd", "cex", "cex.main", "cex.lab", "cex.axis"))))
            trace('title', where = asNamespace('graphics'), print = FALSE, tracer = title_trace)
            trace('title', where = as.environment('package:graphics'), print = FALSE, tracer = title_trace)
            trace('axis', where = as.environment('package:graphics'), print = FALSE,
                  tracer = quote(.GlobalEnv$.box_axes[[length(.GlobalEnv$.box_axes) + 1L]] <-
                    list(side = side, at = at, labels = labels, details = list(...), settings = par(c("ps", "usr", "lwd", "cex", "cex.main", "cex.lab", "cex.axis")))))
            trace('abline', where = as.environment('package:graphics'), print = FALSE,
                  tracer = quote(.GlobalEnv$.box_lines[[length(.GlobalEnv$.box_lines) + 1L]] <- list(h = h, v = v, details = list(...))))
            trace('box', where = as.environment('package:graphics'), print = FALSE,
                  tracer = quote(.GlobalEnv$.box_boxes <- .GlobalEnv$.box_boxes + 1L))
            trace('points.default', where = asNamespace('graphics'), print = FALSE,
                  tracer = quote(.GlobalEnv$.box_marks[[length(.GlobalEnv$.box_marks) + 1L]] <- list(x = x, y = y, pch = list(...)$pch)))
            """;
        preamble += pdf ? "\npdf('box.pdf', width = 7, height = 7)\n" : "\nsvg('box-%03d.svg', width = 7, height = 7)\n";
        string assertions = $$"""

            stopifnot(.box_pages == 1L, length(box_summaries) == {{values.Length}}L)
            same_text <- function(x, y) identical(trimws(gsub("[[:space:]]+", " ", paste(x, collapse = " "))),
                                                  trimws(gsub("[[:space:]]+", " ", paste(y, collapse = " "))))
            stopifnot(identical(box_title, {{RScriptWriter.Quote(title)}}), identical(box_axis_title, {{RScriptWriter.Quote(axis)}}))
            expected_groups <- {{expected}}
            expected_counts <- {{counts}}
            expected_data <- {{observations}}
            for (i in seq_along(box_summaries)) {
              s <- box_summaries[[i]]
              actual <- c(s$centre, s$other, s$box, s$inner, s$outer)
              expected <- expected_groups[[i]]
              stopifnot(length(actual) == length(expected), all(abs(actual - expected) <= 1e-9 * pmax(1, abs(expected))))
              stopifnot(s$n == expected_counts[i])
              x <- sort(as.double(expected_data[[i]]))
              stopifnot(identical(as.double(s$x), x))
              centile <- {{(int)options.Method}}L %in% c(4L, 5L)
              inner <- {{(options.UseInnerFence ? "TRUE" : "FALSE")}}
              outer <- {{(options.UseOuterFence ? "TRUE" : "FALSE")}}
              fence <- if (inner) expected[5:6] else if (outer) expected[7:8] else range(x)
              whiskers <- if (centile) expected[7:8] else c(min(x[x >= fence[1]]), max(x[x <= fence[2]]))
              stopifnot(isTRUE(all.equal(s$whiskers, whiskers, check.attributes = FALSE)))
              outside_inner <- !centile & inner & (x < expected[5] | x > expected[6])
              outside_outer <- (centile | outer) & (x < expected[7] | x > expected[8])
              stopifnot(identical(as.double(s$hollow), x[outside_inner & !outside_outer]),
                        identical(as.double(s$filled), x[outside_outer]))
            }
            stopifnot(any(vapply(.box_titles, function(z) same_text(z$main, {{RScriptWriter.Quote(title)}}), logical(1))))
            stopifnot(any(vapply(.box_titles, function(z) same_text(z[[{{RScriptWriter.Quote(options.Orientation == ChartOrientation.Horizontal ? "xlab" : "ylab")}}]], {{RScriptWriter.Quote(axis)}}), logical(1))))
            same_labels <- function(x) length(x) == length({{names}}) &&
              all(mapply(same_text, as.character(x), {{names}}))
            stopifnot(any(vapply(.box_axes, function(z) identical(as.integer(z$side), {{(options.Orientation == ChartOrientation.Horizontal ? 2 : 1)}}L) && same_labels(z$labels), logical(1))))
            font_at_most <- function(z, prefix, maximum) {
              scale <- z$details[[paste0("cex.", prefix)]]
              if (is.null(scale)) scale <- z$settings[[paste0("cex.", prefix)]]
              size <- scale * z$settings$ps * z$settings$cex
              length(size) == 1L && is.finite(size) && size > 0 && size <= maximum + 1e-8
            }
            stopifnot(any(vapply(.box_titles, function(z) same_text(z$main, {{RScriptWriter.Quote(title)}}) &&
              font_at_most(z, "main", 12), logical(1))))
            stopifnot(any(vapply(.box_titles, function(z) same_text(z[[{{RScriptWriter.Quote(options.Orientation == ChartOrientation.Horizontal ? "xlab" : "ylab")}}]], {{RScriptWriter.Quote(axis)}}) &&
              font_at_most(z, "lab", 10), logical(1))))
            stopifnot(any(vapply(.box_axes, function(z) same_labels(z$labels) &&
              font_at_most(z, "axis", 9), logical(1))))
            stopifnot(.box_boxes == {{(options.ShouldBoxAxes ? 1 : 0)}}L)
            if (!is.null(box_settings$valueMinimum)) {
              # This selected Michelson case deliberately fixes the numeric scale, ticks, grid and marker in the native dialog.
              value_side <- if (box_horizontal) 1L else 2L
              value_axis <- Filter(function(z) as.integer(z$side) == value_side, .box_axes)[[1L]]
              stopifnot(isTRUE(all.equal(value_axis$settings$usr[if (box_horizontal) 1:2 else 3:4], c(299.5, 300.2))),
                        isTRUE(all.equal(as.double(value_axis$at), seq(299.5, 300.2, by = 0.1))))
              grid_lines <- Filter(function(z) identical(z$details$col, "grey80") && identical(as.integer(z$details$lty), 3L), .box_lines)
              stopifnot(length(grid_lines) == 1L,
                        isTRUE(all.equal(as.double(grid_lines[[1L]][[if (box_horizontal) "v" else "h"]]), seq(299.5, 300.2, by = 0.1))))
              marker_lines <- Filter(function(z) identical(z[[if (box_horizontal) "v" else "h"]], 299.85), .box_lines)
              stopifnot(length(marker_lines) == 1L)
            }
            crosses <- Filter(function(z) !is.null(z$pch) && any(z$pch == 4), .box_marks)
            stopifnot(length(crosses) == {{(options.MarkMeanAndMedian ? values.Length : 0)}}L)
            dev.off()
            cat('BOX VERIFIED\n')
            """;
        string output = RunBoxR(preamble + "\n" + script + "\n" + assertions, folder);
        bool oneFile = pdf
            ? File.Exists(Path.Combine(folder, "box.pdf")) && System.Text.RegularExpressions.Regex.Matches(
                System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(folder, "box.pdf"))), @"/Type\s*/Page\b").Count == 1
            : Directory.GetFiles(folder, "box-*.svg").Length == 1;
        Check(output.Contains("BOX VERIFIED") && oneFile,
            description + ": one R chart with the chosen title, orientation, label and marks; its summary agrees with the native chart calculation");
    }
}
