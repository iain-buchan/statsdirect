using System.Text.Json;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckChartRecipesC()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-chart-c-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        bool passed = false;
        try
        {
            ClassifierVariable Categories(string title, params string[] values)
            {
                string[] labels = values.Distinct().ToArray();
                return new ClassifierVariable
                {
                    Title = title, Data = values.Select(v => (double)Array.IndexOf(labels, v)).ToArray(),
                    Groups = labels.Select((s, i) => new Group(s, i) { NBin = values.Count(v => v == s) }).ToList()
                };
            }
            DataFrame raw = new();
            raw.Variables.Add(Categories("First selected column", "10", "2", "2", "A", "A", "A"));
            raw.Variables.Add(Categories("Second selected column", "2", "10", "10", "A"));
            BarOptions bar = null;
            var (barRun, output) = Run("BarPlotFrequency", new() { ["rawValues"] = raw }, amend: fillable =>
            {
                bar = (BarOptions)((ChartDefinition)fillable).ChartOptions;
                ChooseChartC(bar, "Selected categorical frequencies", "Edited categories", "Selected proportions");
                bar.Orientation = ChartOrientation.Horizontal;
                bar.SeriesTitles = new[] { "Two", "Ten", "Letter A" };
                bar.ShowLegend = true;
                bar.MaxBarWidth = 0.2;
            });
            JsonElement barRecord = Record(barRun);
            Check(barRecord.GetProperty("parameters").GetProperty("chartSettings").GetProperty("frequencyCategories").EnumerateArray().Select(x => x.GetString()).SequenceEqual(new[] { "2", "10", "A" }),
                "frequency bars retain the native numeric-before-text category order independently of edited category labels");
            GenericOptions horizontalBar = (GenericOptions)bar.Clone();
            horizontalBar.XAxisTitle = bar.YAxisTitle;
            horizontalBar.YAxisTitle = bar.XAxisTitle;
            const string frequencyChecks = """
                stopifnot(identical(frequency_levels,c("2","10","A")),
                          isTRUE(all.equal(unname(counts),matrix(c(2L,1L,3L,1L,2L,1L),nrow=3L))),
                          max(abs(frequency_values[,1L]-c(2,1,3)/6))<1e-14,
                          max(abs(frequency_values[,2L]-c(1,2,1)/4))<1e-14,
                          identical(colnames(bar_values),c("Two","Ten","Letter A")),
                          bar_horizontal,!bar_stacked,length(bar_segments)==2L,length(.b_rectangles)==2L,
                          max(abs(.b_rectangles[[1L]]$right-c(2,1,3)/6))<1e-14)
                stopifnot(length(.b_legends)==1L,.b_legends[[1L]]$result$rect$top < .b_legends[[1L]]$usr[3L])
                """;
            string frequencyScript = RScriptWriter.Write(barRecord, null, null).Text;
            CheckChartBR(frequencyScript, Path.Combine(folder, "frequency"), horizontalBar, frequencyChecks,
                "multiple categorical columns reproduce native proportions and chosen horizontal bar settings");
            CheckChartBR(frequencyScript, Path.Combine(folder, "frequency-pdf"), horizontalBar, frequencyChecks,
                "selected frequency bars open the default Mac PDF device", pdf: true, lazyDevice: true);
            BarOptions singleBar = null;
            var (singleBarRun, _) = Run("BarPlotFrequency", new() { ["rawValues"] = new DataFrame(raw.Variables[0]) }, amend: fillable =>
            {
                singleBar = (BarOptions)((ChartDefinition)fillable).ChartOptions;
                ChooseChartC(singleBar, "Selected single-column counts", "Categories", "Counts");
            });
            CheckChartBR(RScriptWriter.Write(Record(singleBarRun), null, null).Text, Path.Combine(folder, "frequency-single"), singleBar,
                "stopifnot(identical(as.double(frequency_values),c(2,1,3)),identical(as.double(bar_values),c(2,1,3)),nrow(bar_values)==1L)\n",
                "a single categorical column retains native counts instead of converting them to proportions");
            DataFrame fillRaw = new();
            string[] fillLabels = { "No fill", "Solid", "Forward", "Backward", "Crosshatch" };
            foreach (string label in fillLabels) fillRaw.Variables.Add(Categories(label, "A", "A", "B", "B"));
            foreach (bool horizontal in new[] { false, true })
            {
                BarOptions fillBar = null;
                var (fillRun, _) = Run("BarPlotFrequency", new() { ["rawValues"] = fillRaw }, amend: fillable =>
                {
                    fillBar = (BarOptions)((ChartDefinition)fillable).ChartOptions;
                    ChooseChartC(fillBar, "Selected monochrome frequencies", "Categories", "Proportions");
                    fillBar.UseColour = false;
                    fillBar.ShowLegend = true;
                    fillBar.Orientation = horizontal ? ChartOrientation.Horizontal : ChartOrientation.Vertical;
                    fillBar.SeriesTitles = new[] { "Group A", "Group B" };
                    fillBar.ShouldForceFillStyle = false;
                    fillBar.MarkerTypes = Enumerable.Range(0, 5).Select(i => new MarkerType
                    {
                        MarkerShape = MarkerShape.Circle, MarkerColor = ColorDescriptor.Black, LineColor = ColorDescriptor.Black,
                        Width = 1, MarkerSize = 6, MarkerFillStyle = (FillStyle)i
                    }).ToList();
                });
                GenericOptions fillChosen = (GenericOptions)fillBar.Clone();
                if (horizontal) { fillChosen.XAxisTitle = fillBar.YAxisTitle; fillChosen.YAxisTitle = fillBar.XAxisTitle; }
                string fillScript = TraceChartBLegendState(RScriptWriter.Write(Record(fillRun), null, null).Text);
                const string fillChecks = """
                    stopifnot(length(.b_legend_rectangles)==6L,length(.b_legend_points)==0L,
                              identical(.b_legend_before,.b_legend_after),!isTRUE(sd_chart_state$drawing_legend))
                    keys <- .b_legend_rectangles
                    stopifnot(is.na(keys[[1L]]$col),is.null(keys[[1L]]$density),
                              all(col2rgb(keys[[2L]]$col)==0L),is.null(keys[[2L]]$density),
                              identical(vapply(keys[3:6],function(z)as.double(z$density),double(1)),rep(16,4)),
                              identical(vapply(keys[3:6],function(z)as.double(z$angle),double(1)),c(45,135,45,135)))
                    stopifnot(all(vapply(keys,function(z)all(is.finite(c(z$npcLeft,z$npcRight,z$npcBottom,z$npcTop))) &&
                              z$npcLeft>=-0.2 && z$npcRight<=1.2 && z$npcTop<0,logical(1))))
                    """;
                foreach (bool pdf in new[] { false, true })
                    CheckChartBR(fillScript, Path.Combine(folder, "frequency-five-fills-" + horizontal + (pdf ? "-pdf" : "")), fillChosen, fillChecks,
                        "five monochrome frequency fills have distinct matching legend keys below the chart without leaking graphics state", pdf);
            }

            foreach (bool totals in new[] { false, true })
            {
                PyramidOptions pyramid = null;
                var (run, _) = Run("PyramidPlot", new()
                {
                    ["male"] = SheetFrame(Workbook, "Selected population", 0, 0, (totals ? "People" : "Males", totals ? new[] { 100.0, 80, Constant.MISSING, 40 } : new[] { 10.0, 20, Constant.MISSING, 30 })),
                    ["female"] = totals ? null : SheetFrame(Workbook, "Selected population", 1, 0, ("Females", new[] { 12.0, 18, 22, 25 })),
                    ["labels"] = new DataFrame(new StringVariable(new[] { "Young", "", "Missing row", "Older" }, "Chosen age groups"))
                }, amend: fillable =>
                {
                    pyramid = (PyramidOptions)((ChartDefinition)fillable).ChartOptions;
                    ChooseChartC(pyramid, totals ? "Selected total population" : "Selected population by sex", "", "");
                    pyramid.ScaleMaximum = totals ? 90 : 50;
                    pyramid.UseColour = true;
                });
                string expected = totals ? """
                    stopifnot(pyramid_totals,identical(pyramid_males,c(50,40,20)),identical(pyramid_females,c(50,40,20)),pyramid_scale==100,
                              length(.b_rectangles)==1L,identical(.b_rectangles[[1L]]$left,c(-50,-40,-20)),identical(.b_rectangles[[1L]]$right,c(50,40,20)))
                    """ : """
                    stopifnot(!pyramid_totals,identical(pyramid_males,c(10,20,30)),identical(pyramid_females,c(12,18,25)),pyramid_scale==50,
                              length(.b_rectangles)==2L,identical(.b_rectangles[[1L]]$left,c(-10,-20,-30)),identical(.b_rectangles[[2L]]$right,c(12,18,25)))
                    """;
                expected += "\nstopifnot(identical(pyramid_rows,c(1L,2L,4L)),identical(pyramid_labels,c('Young','group 2','Older')))\n";
                string script = RScriptWriter.Write(Record(run), null, null).Text;
                CheckChartBR(script, Path.Combine(folder, "pyramid-" + totals), pyramid, expected,
                    totals ? "a selected total population splits evenly about the centre and preserves labelled missing-row positions" : "a selected two-column pyramid preserves its counts, labels and chosen scale");
                CheckChartBR(script, Path.Combine(folder, "pyramid-pdf-" + totals), pyramid, expected,
                    "selected " + (totals ? "total" : "paired") + " population pyramid opens the default Mac PDF device", pdf: true, lazyDevice: true);
            }

            foreach (string mode in new[] { "first", "moments", "limits", "dates" })
            {
                ControlOptions control = null;
                double[] sequence = mode == "dates" ? new[] { 45003.0, 45001, 45002, 45004 } : new[] { 3.0, 1, 2, 4 };
                var (run, _) = Run("ControlPlot", new()
                {
                    ["Y"] = SheetFrame(Workbook, "Selected process", 0, 0, ("Selected process readings", new[] { 10.0, 12, 14, 30 })),
                    ["X"] = SheetFrame(Workbook, "Selected process", 1, 0, ("Selected observation sequence", sequence))
                }, amend: fillable =>
                {
                    control = (ControlOptions)((ChartDefinition)fillable).ChartOptions;
                    ChooseChartC(control, "Chosen process " + mode, mode == "dates" ? "Selected dates" : "Selected sequence", "Process measurement");
                    control.ObservationsToUse = 2;
                    control.UseMean = true;
                    control.Use1SD = false;
                    control.Use2SD = true;
                    control.Use3SD = false;
                    control.RightHandDecimalPlaces = 2;
                    control.UseColour = false;
                    if (mode == "moments")
                    {
                        control.HasUserSpecifiedMeanAndSD = true;
                        control.UserSpecifiedMean = 15;
                        control.UserSpecifiedSD = 3;
                    }
                    if (mode == "limits")
                    {
                        control.LowerWarningLimit = 8;
                        control.UpperWarningLimit = 22;
                        control.LowerControlLimit = 5;
                        control.UpperControlLimit = 25;
                        control.UseMean = control.Use1SD = control.Use2SD = control.Use3SD = false;
                    }
                });
                string expected = mode switch
                {
                    "moments" => "stopifnot(control_mean==15,control_sd==3,identical(control_bands,c(15,21,9)),identical(control_context,'External:'))\n",
                    "limits" => "stopifnot(control_custom_limits,identical(control_bands,c(22,8,25,5)),identical(control_band_names,c('warn','warn','ctrl','ctrl')))\n",
                    _ => "stopifnot(control_fit_n==2L,control_mean==11,abs(control_sd-sqrt(2))<1e-14,max(abs(control_bands-c(11,11+2*sqrt(2),11-2*sqrt(2))))<1e-14)\n"
                };
                expected += "stopifnot(all(control_colors=='black'),identical(control_path_y,c(12,14,10,30)),length(.b_points)==0L,length(.b_lines)==1L,any(vapply(.b_ablines,function(z)identical(z$h,control_bands),logical(1))))\n";
                if (mode == "dates")
                    expected += "stopifnot(identical(control_path_x,c(45001,45002,45003,45004)),as.integer(chart[['x']]$scale)==3L,any(vapply(.b_axes,function(z)z$side==1&&any(grepl('2023',as.character(z$labels))),logical(1))))\n";
                string script = RScriptWriter.Write(Record(run), null, null).Text;
                CheckChartBR(script, Path.Combine(folder, "control-" + mode), control, expected,
                    "selected control chart " + mode + " settings determine its native moments, visible bands and sequence");
                if (mode == "moments" || mode == "dates")
                    CheckChartBR(script, Path.Combine(folder, "control-pdf-" + mode), control, expected,
                        "selected control chart " + mode + " on the Mac PDF device", pdf: true);
                if (mode == "first")
                    CheckChartBR(script, Path.Combine(folder, "control-pdf-fresh-device"), control,
                        expected + "stopifnot(identical(control_font$family,'sans'))\n",
                        "a control chart opens the default PDF device from a null device before choosing its portable font", pdf: true, lazyDevice: true);
            }
            passed = true;
        }
        finally
        {
            if (passed && Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) Directory.Delete(folder, true);
            else if (!passed) Console.WriteLine("      chart group C failure artifacts: " + folder);
        }
    }

    private static void ChooseChartC(GenericOptions options, string title, string xTitle, string yTitle)
    {
        options.Title = title;
        options.XAxisTitle = xTitle;
        options.YAxisTitle = yTitle;
        options.TitleFontDescriptor = new FontDescriptor("Arial", 1, 22);
        options.AxisTitleFontDescriptor = new FontDescriptor("Arial", 0, 15);
        options.AxisLabelFontDescriptor = new FontDescriptor("Arial", 0, 15);
        options.LegendFontDescriptor = new FontDescriptor("Arial", 0, 15);
    }
}
