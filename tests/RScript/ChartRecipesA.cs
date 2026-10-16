using System.Text.Json;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.R;

internal static partial class Program
{
    // Different data, titles and settings from the help examples prevent teaching
    // illustrations from accidentally becoming the analysis's R replay again.
    private static void CheckChartRecipesA()
    {
        RRecipes.Folder = Metadata("Recipes");
        object Input(string name, params object[] columns) => new { name, kind = "entered", columns };
        object Column(string title, params object[] values) => new { title, values };
        Dictionary<string, object> Options() => new()
        {
            ["title"] = "Selected chart — October", ["xTitle"] = "Selected X", ["yTitle"] = "Selected Y",
            ["boxAxes"] = true, ["dataTitles"] = new[] { "Selected measurements" }
        };
        void Replay(string operation, object[] inputs, Dictionary<string, object> options, string assertions, int panels = 1)
        {
            string folder = Path.Combine(Path.GetTempPath(), "statsdirect-chart-a-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string json = JsonSerializer.Serialize(new
            {
                version = 1, operation, title = "Selected result", run = "2026-10-16T12:00:00Z", inputs,
                parameters = new Dictionary<string, object> { ["chartSettings"] = options }
            });
            string script = RScriptWriter.Write(JsonDocument.Parse(json).RootElement, null, null).Text;
            string prelude = "options(device = function(...) grDevices::svg(filename = 'chart-%03d.svg', ...))\n" +
                "plot_count <- 0L\ntrace(graphics::plot.new, tracer=quote(plot_count <<- plot_count + 1L), print=FALSE)\n";
            string output = RunR(prelude + script + "\n" + assertions + "\nstopifnot(plot_count == " + panels + "L)\ncat('CHART_A_OK\\n')\n", folder);
            Check(output.Contains("CHART_A_OK") && Directory.GetFiles(folder, "chart-*.svg").Length == 1,
                operation + " replays the selected data and choices on exactly one SVG page");
            if (panels >= 4)
            {
                string pdfPrefix = "options(device=function(...)grDevices::pdf(file='multi.pdf',...))\nplot_count<-0L\ntrace(graphics::plot.new,tracer=quote(plot_count<<-plot_count+1L),print=FALSE)\n";
                string pdfOutput = RunR(pdfPrefix + script + "\n" + assertions + "\nstopifnot(plot_count==" + panels + "L)\ncat('PDF_MULTI_OK\\n')\n", Path.Combine(folder, "pdf"));
                string pdf = Path.Combine(folder, "pdf", "multi.pdf");
                Check(pdfOutput.Contains("PDF_MULTI_OK") && System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(pdf)), @"/Type /Page\s").Count == 1,
                    "six selected histograms fit on one automatically extended PDF page");
                Console.WriteLine("      six-panel chart QA: " + folder);
            }
        }
        object[] bars = { Input("values", Column("First", 2, 4, 6), Column("Second", 1, 5, 3)), Input("labels", Column("Categories", "A", "B", "C")) };
        foreach (bool horizontal in new[] { false, true })
            foreach (bool stacked in new[] { false, true })
                foreach (bool percent in stacked ? new[] { false, true } : new[] { false })
                    foreach (bool rotate in stacked ? new[] { false, true } : new[] { false })
                    {
                        var settings = Options();
                        settings["horizontal"] = horizontal; settings["stacked"] = stacked;
                        settings["stacked100Percent"] = percent; settings["rotateWhenStacked"] = rotate;
                        settings["seriesTitles"] = new[] { "North", "South", "West" };
                        string reference = rotate ? "t(rbind(c(2,4,6),c(1,5,3)))" : "rbind(c(2,4,6),c(1,5,3))";
                        Replay("BarPlot", bars, settings,
                            "expected <- " + reference + "\n" +
                            (percent ? "expected <- sweep(expected,2,colSums(expected),'/') * 100\n" : "") +
                            "stopifnot(isTRUE(all.equal(unname(bar_values), unname(expected))), identical(chart$title, 'Selected chart — October'))");
                    }
        object[] normals = { Input("X", Column("Selected measurements", 6, 2, 2, 7, 9)) };
        foreach (int method in new[] { 1, 2, 3 })
            foreach (bool scaled in new[] { false, true })
            {
                var settings = Options(); settings["method"] = method; settings["scaleZ"] = scaled;
                Replay("NormalPlot", normals, settings, """
expected_ranks <- rank(c(6,2,2,7,9))
expected <- if (normal_method == 1) qnorm(expected_ranks/6) else if(normal_method == 2) qnorm((expected_ranks-.375)/5.25) else {
  en <- function(r) integrate(function(z) z * dnorm(z) * exp((r-1)*pnorm(z,log.p=TRUE) + (5-r)*pnorm(z,lower.tail=FALSE,log.p=TRUE) - lbeta(r,6-r)), -10, 10, rel.tol=1e-9)$value
  vapply(expected_ranks, function(r) (en(floor(r))+en(ceiling(r)))/2, 0.0)
}
if (normal_scaled) expected <- mean(c(6,2,2,7,9)) + expected * sqrt(mean((c(6,2,2,7,9)-mean(c(6,2,2,7,9)))^2))
stopifnot(max(abs(normal_scores-expected)) < 1e-7, normal_scores[2] == normal_scores[3], identical(chart$title,'Selected chart — October'))
""");
            }
        object[] histograms = { Input("data", Column("Negative and zero", -3, -2, -1, 0, 1, 2, 3), Column("Other selected column", 1, 1, 2, 3, 4, 5, 6)) };
        foreach (bool relative in new[] { false, true })
            foreach (bool overlay in new[] { false, true })
            {
                var settings = Options(); settings["relativeFrequencies"] = relative; settings["overlayNormalCurve"] = overlay;
                settings["histograms"] = new[]
                {
                    new { title = "Selected negatives", xTitle="Negative values", yTitle="Counts", edges = new[] { -3.0, -1, 1, 3 } },
                    new { title = "Selected positives", xTitle="Positive values", yTitle="Counts", edges = new[] { 1.0, 3, 5, 7 } }
                };
                Replay("HistogramPlot", histograms, settings, """
stopifnot(length(histograms)==2L, identical(histograms[[1]]$counts,c(3L,2L,2L)), identical(histograms[[2]]$counts,c(4L,2L,1L)))
stopifnot(sum(histograms[[1]]$counts)==7L, sum(histograms[[2]]$counts)==7L)
if(histogram_relative) stopifnot(abs(sum(histograms[[1]]$heights)-1)<1e-14)
""", 2);
            }
        object[] spreads = { Input("data", Column("First", 1, 1, 1.0001, 2, 8), Column("Second", 0, 0, 3, 3, 4)) };
        foreach (bool horizontal in new[] { false, true })
        {
            var settings = Options(); settings["horizontal"] = horizontal; settings["seriesTitles"] = new[] { "Selected first", "Selected second" };
            Replay("SpreadPlot", spreads, settings, """
stopifnot(length(spread_stacks)==2L, sum(spread_stacks[[1]]$count)==5L, sum(spread_stacks[[2]]$count)==5L,
          spread_stacks[[1]]$count[1]==3L, identical(spread_labels,c('Selected first','Selected second')))
""");
        }
        Replay("HistogramPlot", new[] { Input("data", Column("All equal", 0, 0, 0)) }, null,
            "stopifnot(length(histograms)==1L,sum(histograms[[1]]$counts)==3L, all(is.finite(histograms[[1]]$breaks)))");
        Replay("NormalPlot", normals, null, "stopifnot(normal_method==1L,!normal_scaled,all(is.finite(normal_scores)))");
        Replay("SpreadPlot", spreads, null, "stopifnot(length(spread_stacks)==2L)");
        Replay("BarPlot", bars, null, "stopifnot(!bar_stacked,identical(dim(bar_values),c(2L,3L)))");
        object[] sixHistograms = { Input("data", Enumerable.Range(1, 6).Select(i => Column("Selected column " + i, -2 * i, -i, 0, i, 2 * i)).ToArray()) };
        Replay("HistogramPlot", sixHistograms, null,
            "stopifnot(length(histograms)==6L,dev.size('in')[2]>=42-1e-8,all(vapply(histograms,function(h)sum(h$counts),0L)==5L))", 6);
        CheckChartRecipesARealRuns();
    }

    private static void CheckChartRecipesARealRuns()
    {
        void Common(GenericOptions options)
        {
            options.Title = "Actual selected October chart"; options.UseColour = true; options.ShouldBoxAxes = true;
            options.TitleFontDescriptor = new FontDescriptor("Arial", 1, 18);
            options.AxisTitleFontDescriptor = new FontDescriptor("Arial", 1, 12);
            options.AxisLabelFontDescriptor = new FontDescriptor("Arial", 0, 10);
        }
        void Replay(OperationRun run, string assertions, string name)
        {
            JsonElement record = Record(run);
            JsonElement settings = record.GetProperty("parameters").GetProperty("chartSettings");
            Check(settings.GetProperty("title").GetString() == "Actual selected October chart"
                && settings.GetProperty("titleFontSize").GetDouble() == 18,
                name + " records actual dialog title and fonts through the engine");
            string script = RScriptWriter.Write(record, null, null).Text;
            string folder = Path.Combine(Path.GetTempPath(), "statsdirect-chart-a-real-" + name + "-" + Guid.NewGuid().ToString("N"));
            string prefix = "options(device=function(...)grDevices::svg(filename='actual-%03d.svg',...))\n";
            string output = RunR(prefix + script + "\n" + assertions + "\ncat('REAL_A_OK\\n')\n", folder);
            Check(output.Contains("REAL_A_OK") && Directory.GetFiles(folder, "actual-*.svg").Length == 1,
                name + " engine record replays selected values and options on one SVG page");
            string pdfPrefix = "options(device=function(...)grDevices::pdf(file='actual.pdf',...))\n";
            output = RunR(pdfPrefix + script + "\n" + assertions + "\ncat('REAL_A_OK\\n')\n", Path.Combine(folder, "pdf"));
            string pdf = Path.Combine(folder, "pdf", "actual.pdf");
            Check(output.Contains("REAL_A_OK") && File.Exists(pdf)
                && System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(pdf)), @"/Type /Page\s").Count == 1,
                name + " replay also draws exactly one page on the Mac-style ordinary PDF device");
            Console.WriteLine("      actual chart QA: " + folder);
        }
        DataFrame Labels() { DataFrame f = new(); f.Variables.Add(new StringVariable(new[] { "A", "B", "C" }, "Categories")); return f; }
        var (barRun, _) = Run("BarPlot", new()
        {
            ["values"] = SheetFrame(Workbook, "Chart A", 0, 0, ("First", new[] { 2.0, 4, 6 }), ("Second", new[] { 1.0, 5, 3 })),
            ["labels"] = Labels()
        }, amend: fillable =>
        {
            var d = (ChartDefinition)fillable; var o = (BarOptions)d.ChartOptions; Common(o);
            o.Stacked = true; o.RotateWhenStacked = false; o.Stacked100Percent = true; o.Orientation = ChartOrientation.Horizontal;
            o.SeriesTitles = new[] { "North", "South", "West" }; o.XAxisTitle = "Selected regions"; o.YAxisTitle = "Percent";
            d.ScaleParameters.Y.AxisScale = new LinearAxisScale(0, 100, 0, 100, 5);
            d.ScaleParameters.Y.HasGridLines = true;
        });
        Replay(barRun, "stopifnot(bar_stacked,bar_percent,bar_horizontal,!bar_rotated,max(abs(colSums(bar_values)-100))<1e-12,identical(colnames(bar_values),c('North','South','West')))", "bar");

        var (rotatedRun, _) = Run("BarPlot", new()
        {
            ["values"] = SheetFrame(Workbook, "Chart A", 0, 0, ("First", new[] { 2.0, 4, 6 }), ("Second", new[] { 1.0, 5, 3 })),
            ["labels"] = Labels()
        }, amend: fillable =>
        {
            var o = (BarOptions)((ChartDefinition)fillable).ChartOptions; Common(o);
            o.Stacked = true; o.RotateWhenStacked = true; o.Orientation = ChartOrientation.Vertical;
            o.SeriesTitles = new[] { "North", "South", "West" };
            o.ShouldForceIsFilled = true; o.ForcedIsFilled = true;
            o.ShouldForceFillStyle = true; o.ForcedFillStyle = FillStyle.Solid;
        });
        JsonElement rotatedMarkers = Record(rotatedRun).GetProperty("parameters").GetProperty("chartSettings").GetProperty("markers");
        Check(rotatedMarkers.GetArrayLength() == 3 && rotatedMarkers.EnumerateArray().All(m => m.GetProperty("filled").GetBoolean() && m.GetProperty("fillStyle").GetInt32() == 1),
            "rotated stacked bars record three effective segment markers and forced fill settings without duplicate keys");
        Replay(rotatedRun, "stopifnot(bar_stacked,bar_rotated,identical(dim(bar_values),c(3L,2L)),length(chart$markers)==3L,identical(rownames(bar_values),c('North','South','West')))", "rotated-bar");

        var (normalRun, _) = Run("NormalPlot", new() { ["X"] = SheetFrame(Workbook, "Chart A", 0, 0, ("Measured response", new[] { 6.0, 2, 2, 7, 9 })) }, amend: fillable =>
        {
            var d = (ChartDefinition)fillable; var o = (NormalOptions)d.ChartOptions; Common(o);
            o.Method = NormalOptions.ScoreMethod.ExpectedNormalOrder; o.ShouldScaleZ = true;
            d.ScaleParameters.X.HasGridLines = true; d.ScaleParameters.Y.MarkerLineValue = 5;
        });
        double[] ranks = { 3, 1.5, 1.5, 4, 5 };
        double[] nativeScores = ranks.Select(r => 0.5 * (StatsDirect.Numerics.PDF.expnos((int)Math.Floor(r), 5) + StatsDirect.Numerics.PDF.expnos((int)Math.Ceiling(r), 5))).ToArray();
        double[] observed = { 6, 2, 2, 7, 9 }; double mean = observed.Average(); double sigma = Math.Sqrt(observed.Select(v => (v - mean) * (v - mean)).Average());
        string native = "c(" + string.Join(",", nativeScores.Select(v => (mean + v * sigma).ToString("R", inv))) + ")";
        Replay(normalRun, "stopifnot(normal_method==3L,normal_scaled,normal_scores[2]==normal_scores[3],identical(data_title,'Measured response'))\n" +
            "native_difference <- max(abs(normal_scores-" + native + "))\ncat('native normal-score maximum difference:',native_difference,'\\n')\nstopifnot(native_difference<1e-6)", "normal");

        var (histogramRun, _) = Run("HistogramPlot", new() { ["data"] = SheetFrame(Workbook, "Chart A", 0, 0,
            ("Negative values", new[] { -3.0, -2, -1, 0, 1, 2, 3 }), ("Positive values", new[] { 1.0, 1, 2, 3, 4, 5, 6 })) }, amend: fillable =>
        {
            var d = (ChartDefinition)fillable; var o = (HistogramOptions)d.ChartOptions; Common(o);
            o.ShowRelativeFrequencies = true; o.OverlayNormalCurve = true; o.PoolVariablesForBins = true;
            o.HistoSeriesOptions[0].ChartTitle = "Selected negative observations"; o.HistoSeriesOptions[0].XAxisTitle = "Measured negative values";
            o.HistoSeriesOptions[1].ChartTitle = "Selected positive observations"; o.HistoSeriesOptions[1].XAxisTitle = "Measured positive values";
            o.HistoSeriesOptions[0].BinsDescriptor = new BinsDescriptor { Edges = new[] { -3.0, -1, 1, 3 }, Counts = new[] { 3, 2, 2 } };
            o.HistoSeriesOptions[1].BinsDescriptor = new BinsDescriptor { Edges = new[] { 1.0, 3, 5, 7 }, Counts = new[] { 4, 2, 1 } };
        });
        Replay(histogramRun, "stopifnot(length(histograms)==2L,histogram_relative,histogram_overlay,identical(histograms[[1]]$counts,c(3L,2L,2L)),identical(histograms[[2]]$counts,c(4L,2L,1L)),identical(histogram_options[[1]]$title,'Selected negative observations'))", "histogram");

        // The auto device grows for multiple panels, while an explicitly opened
        // standard-size device must keep the caller's dimensions and one page.
        string histogramScript = RScriptWriter.Write(Record(histogramRun), null, null).Text;
        string multiFolder = Path.Combine(Path.GetTempPath(), "statsdirect-chart-a-multihist-" + Guid.NewGuid().ToString("N"));
        string fixedOutput = RunR("grDevices::pdf('fixed.pdf',width=7,height=7)\n" + histogramScript +
            "\nstopifnot(max(abs(dev.size('in')-c(7,7)))<1e-8)\ncat('FIXED_OK\\n')\n", multiFolder);
        Check(fixedOutput.Contains("FIXED_OK"), "two histogram panels respect an explicitly opened fixed-size PDF device");

        var (spreadRun, _) = Run("SpreadPlot", new() { ["data"] = SheetFrame(Workbook, "Chart A", 0, 0,
            ("First observed group", new[] { 1.0, 1, 1.0001, 2, 8 }), ("Second observed group", new[] { 0.0, 0, 3, 3, 4 })) }, amend: fillable =>
        {
            var d = (ChartDefinition)fillable; var o = (SpreadOptions)d.ChartOptions; Common(o);
            o.Orientation = ChartOrientation.Vertical; o.SeriesTitles = new[] { "Selected first group", "Selected second group" }; o.XAxisTitle = "Measured response";
            o.MarkerTypes[0].MarkerSize = 12;
            d.ScaleParameters.X.AxisScale = new LinearAxisScale(0, 8, 0, 10, 5); d.ScaleParameters.X.HasGridLines = true;
        });
        Replay(spreadRun, "stopifnot(!spread_horizontal,identical(spread_labels,c('Selected first group','Selected second group')),sum(spread_stacks[[1]]$count)==5L,sum(spread_stacks[[2]]$count)==5L)", "spread");
    }
}
