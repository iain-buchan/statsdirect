using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using StatsDirect.Charting;
using StatsDirect.R;
using StatsDirect.UI;

internal static partial class Program
{
    private static void CheckTukeyHinges()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-tukey-" + Guid.NewGuid().ToString("N"));
        var method = (BoxWhiskerOptions.BoxWhiskerMethod)7;
        Check(new BoxWhiskerOptions().Method == BoxWhiskerOptions.BoxWhiskerMethod.MedianQuartilesRange,
            "adding Tukey hinges preserves the existing box-plot default and method numbers");
        using (var control = new ctlBoxWhiskerOptions())
        {
            var options = new BoxWhiskerOptions { Cco = .95 };
            control.ChartDefinition = new ChartDefinition { ChartOptions = options };
            var radio = control.Controls.Find("rdoMethodTukey", true).OfType<RadioButton>().Single();
            radio.Checked = true; control.FillOptionsFromForm();
            Check(options.Method == method && options.XAxisTitle.Contains("Tukey", StringComparison.OrdinalIgnoreCase),
                "the Tukey hinges radio selects the new method and identifies its convention in the axis caption");
        }

        var cases = Enumerable.Range(1, 50).Select(n => Enumerable.Range(1, n)
            .Select(i => Math.Round(Math.Sin(i * .71) * 9 + i / 3.0, 2)).ToArray()).ToList();
        cases.AddRange(new[]
        {
            new double[] { 5, 5, 5, 5, 5 }, new double[] { 0, 0, 0, 0, 0, 1, 50 },
            new double[] { -900, -3, -2, -1, 0, 1, 2, 3, 500 },
            new double[] { 300, 201, 232, 312, 220, 256, 328, 330, 231 },
            new double[] { 312, 242, 340, 388, 296, 254, 391, 402, 290 }
        });
        string Vec(IEnumerable<double> x) => "c(" + string.Join(",", x.Select(v => v.ToString("R", inv))) + ")";
        for (int fences = 0; fences < 4; fences++)
        {
            var options = new BoxWhiskerOptions { Method = method, Cco = .95,
                UseInnerFence = (fences & 1) != 0, UseOuterFence = (fences & 2) != 0 };
            string expected = "list(" + string.Join(",", cases.Select(x => Vec(NativeBoxSummary(x, options)))) + ")";
            string source = "cases<-list(" + string.Join(",", cases.Select(Vec)) + ")\nexpected<-" + expected + "\n" + $$"""
                for(i in seq_along(cases)) {
                  x<-cases[[i]];five<-fivenum(x);q<-five[c(2,4)];limits<-range(x)
                  inner<-if({{(options.UseInnerFence ? "TRUE" : "FALSE")}})q+c(-1,1)*1.5*diff(q)else q
                  outer<-if({{(options.UseOuterFence ? "TRUE" : "FALSE")}})q+c(-1,1)*3*diff(q)else limits
                  actual<-c(five[3],mean(x),q,pmax(limits[1],pmin(limits[2],inner)),pmax(limits[1],pmin(limits[2],outer)))
                  stopifnot(isTRUE(all.equal(actual,expected[[i]],tolerance=1e-12)))
                }
                cat('TUKEY_NATIVE_OK\n')
                """;
            Check(RunR(source, Path.Combine(folder, "native-" + fences)).Contains("TUKEY_NATIVE_OK"),
                $"native Tukey hinges and fences {fences} agree with R fivenum for all 55 small, tied, constant and outlier datasets");
        }

        foreach (bool horizontal in new[] { false, true })
        for (int fences = 0; fences < 4; fences++)
        {
            var (run, _) = Run("BoxWhiskerPlot", new() { ["data"] = SheetFrame(Workbook, "Tukey", 0, 0,
                ("PEFR After", cases[^2]), ("PEFR Before", cases[^1])) }, amend: fillable =>
            {
                var options = (BoxWhiskerOptions)((ChartDefinition)fillable).ChartOptions;
                options.Method = method; options.UseInnerFence = (fences & 1) != 0; options.UseOuterFence = (fences & 2) != 0;
                options.MarkMeanAndMedian = true; options.Orientation = horizontal ? ChartOrientation.Horizontal : ChartOrientation.Vertical;
                options.Title = "PEFR before and after"; options.XAxisTitle = "PEFR";
            });
            JsonElement record = Record(run);
            Check(record.GetProperty("parameters").GetProperty("boxWhisker").GetProperty("method").GetInt32() == 7,
                "the final Tukey selection is retained in the actual analysis record");
            string script = RScriptWriter.Write(record, null, null).Text;
            Check(script == RScriptWriter.Write(JsonDocument.Parse(record.GetRawText()).RootElement, null, null).Text
                && !script.Contains("function(") && !script.Contains("box_settings") && script.Contains("boxplot(")
                && script.Split('\n').Length <= 30,
                "Tukey replay survives reopening and emits a short selected native boxplot call without unused settings or helper functions");
            double coefficient = (fences & 1) != 0 ? 1.5 : (fences & 2) != 0 ? 3 : 0;
            string assertions = "\nexpected<-boxplot(mets,plot=FALSE,range=" + coefficient.ToString(inv) + ")\n" + $$"""
                stopifnot(length(box_plot$n)==2L,all(box_plot$n==9L))
                stopifnot(isTRUE(all.equal(unname(box_plot$stats),unname(expected$stats))))
                stopifnot(identical(as.numeric(.tukey_draw$at),if({{(horizontal ? "TRUE" : "FALSE")}})c(2,1)else c(1,2)))
                stopifnot(identical(.tukey_draw$horizontal,{{(horizontal ? "TRUE" : "FALSE")}}))
                stopifnot(length(box_plot$out)==length(expected$out))
                dev.off();cat('TUKEY_REPLAY_OK\n')
                """;
            string caseFolder = Path.Combine(folder, "replay-" + horizontal + "-" + fences);
            string output = RunR("svg('tukey-%03d.svg',width=7,height=6)\ntrace('bxp',tracer=quote(.tukey_draw<<-list(at=if(is.null(at))seq_along(z$n)else at,horizontal=horizontal)),print=FALSE,where=asNamespace('graphics'))\n" + script + assertions, caseFolder);
            Check(output.Contains("TUKEY_REPLAY_OK") && Directory.GetFiles(caseFolder, "tukey-*.svg").Length == 1,
                $"Tukey replay gives one native R chart in selected orientation {horizontal} with fence choice {fences} and original data");
        }

        CheckTukeySelectedExport(folder, cases[^1], cases[^2]);

        foreach (bool mean in new[] { false, true })
        {
            var (run, _) = Run("BoxWhiskerPlotText", new() { ["data"] = SheetFrame(Workbook, "Tukey text", 0, 0,
                ("PEFR After", cases[^2])) }, amend: fillable =>
            {
                var options = (BoxWhiskerOptions)((ChartDefinition)fillable).ChartOptions;
                options.Method = method; options.MarkMeanAndMedian = mean;
                options.UseInnerFence = false; options.UseOuterFence = true;
            });
            string script = RScriptWriter.Write(Record(run), null, null).Text;
            string result = RunR("svg('text.svg')\n" + script + $$"""

                stopifnot(tukey,!use_inner,use_outer,box_settings$markMeanAndMedian=={{(mean ? "TRUE" : "FALSE")}})
                stopifnot(isTRUE(all.equal(as.numeric(z$stats[2:4,1]),fivenum(groups[[1]])[2:4])))
                dev.off();cat('TUKEY_TEXT_OK\n')
                """, Path.Combine(folder, "text-" + mean));
            Check(result.Contains("TUKEY_TEXT_OK"), "Tukey text-plot replay retains its actual method, hinges, chosen fences and mean-marker setting");
        }
    }

    private static void CheckTukeySelectedExport(string folder, double[] before, double[] after)
    {
        // This is the user's actual PEFR example: the long interpreter and font/settings dump must not return.
        var (run, _) = Run("BoxWhiskerPlot", new() { ["data"] = SheetFrame(Workbook, "Parametric", 0, 0,
            ("PEFR Before", before), ("PEFR After", after)) }, amend: fillable =>
        {
            var definition = (ChartDefinition)fillable;
            var options = (BoxWhiskerOptions)definition.ChartOptions;
            options.Method = (BoxWhiskerOptions.BoxWhiskerMethod)7;
            options.UseInnerFence = true; options.UseOuterFence = true; options.MarkMeanAndMedian = false;
            options.Orientation = ChartOrientation.Horizontal;
            options.Title = "Box & whisker plot from Example workbook";
            options.XAxisTitle = "Tukey hinges: min < lower hinge < median > upper hinge > max, fences (1.5 & 3.0 IQR)";
            definition.ScaleParameters.X.AxisScale = new StatsDirect.Templates.LinearAxisScale(201, 402, 200, 402, 8);
        });
        JsonNode actualPefr = JsonNode.Parse(Record(run).GetRawText());
        actualPefr["parameters"]["boxWhisker"]["valueMinimum"] = 200;
        actualPefr["parameters"]["boxWhisker"]["valueMaximum"] = 402;
        actualPefr["parameters"]["boxWhisker"]["valueInterval"] = 25;
        actualPefr["parameters"]["boxWhisker"]["valueFirstTick"] = 200;
        JsonElement record = JsonDocument.Parse(actualPefr.ToJsonString()).RootElement;
        string script = RScriptWriter.Write(record, null, null).Text;
        Check(script.Split('\n').Length <= 26 && !script.Contains("function(") && !script.Contains("box_settings")
            && !script.Contains("innerFence") && !script.Contains("titleFont") && !script.Contains("confidence")
            && !script.Contains("far <-") && !script.Contains("points(") && !script.Contains("\\nTukey")
            && script.Contains("value_title <- \"Tukey hinges (1.5 and 3 IQR fences)\""),
            "the actual PEFR Tukey export is one direct boxplot call with a concise one-line caption and no unused feature code");
        foreach (double unsetWidth in new[] { 0.0, -1.0 })
        {
            JsonNode unset = JsonNode.Parse(record.GetRawText());
            unset["parameters"]["boxWhisker"]["lineWidth"] = unsetWidth;
            string fallback = RScriptWriter.Write(JsonDocument.Parse(unset.ToJsonString()).RootElement, null, null).Text;
            Check(!fallback.Contains(", lwd = "), "an uninitialised or invalid saved line width uses R's normal width");
        }
        foreach (bool pdf in new[] { false, true })
        foreach (double width in new[] { 4.0, 7.0 })
        {
            string device = pdf ? $"pdf('selected.pdf',width={width.ToString(inv)},height=6)" : $"svg('selected.svg',width={width.ToString(inv)},height=6)";
            string output = RunR(device + "\ntrace('title',tracer=quote({if(!missing(main)&&length(main)&&is.character(main)&&nzchar(main[1])) .main_fit<<-strwidth(main[1],units='inches',cex=if(is.null(list(...)$cex.main))par('cex.main')else list(...)$cex.main,font=2)/par('pin')[1];if(!missing(xlab)&&length(xlab)&&is.character(xlab)&&nzchar(xlab[1])) .caption_fit<<-strwidth(xlab[1],units='inches',cex=if(is.null(list(...)$cex.lab))par('cex.lab')else list(...)$cex.lab)/par('pin')[1]}),print=FALSE)\n" + script + "\nstopifnot(.main_fit<=1,.caption_fit<=1);dev.off();cat('TUKEY_TEXT_FITS\\n')\n",
                Path.Combine(folder, "short-pefr-" + pdf + "-" + width.ToString(inv)));
            Check(output.Contains("TUKEY_TEXT_FITS"), $"the concise PEFR title and caption fit on one line on a {width}-inch {(pdf ? "PDF" : "SVG")} plot");
        }

        foreach (bool horizontal in new[] { false, true })
        foreach (int scale in new[] { 0, 1, 2 })
        {
            JsonNode selected = JsonNode.Parse(record.GetRawText());
            JsonObject settings = selected["parameters"]["boxWhisker"].AsObject();
            settings["horizontal"] = horizontal; settings["boxAxes"] = true; settings["lineWidth"] = 2;
            settings["valueScale"] = scale; settings["valueGrid"] = true; settings["valueGridStyle"] = 4;
            settings["valueMarker"] = 299; settings["valueMinimum"] = 200; settings["valueMaximum"] = 410;
            settings["valueInterval"] = 25; settings["valueFirstTick"] = 200;
            settings["axisTitle"] = "Custom measurement units";
            settings["seriesTitles"] = JsonNode.Parse("[\"Edited Before\",\"Edited After\"]");
            string selectedScript = RScriptWriter.Write(JsonDocument.Parse(selected.ToJsonString()).RootElement, null, null).Text;
            Check(selectedScript.Contains("value_title <- \"Custom measurement units\"") && selectedScript.Contains("names = c(\"Edited Before\", \"Edited After\")")
                && selectedScript.Contains("lwd = 2") && !selectedScript.Contains("frame.plot = FALSE")
                && selectedScript.Contains("abline(" + (horizontal ? "v" : "h") + " = 299)"),
                "the concise Tukey call preserves selected labels, boxed axes, line width, marker and orientation");
            string output = RunR("svg('selected.svg')\ntrace('bxp',tracer=quote(.tukey_draw<<-list(at=if(is.null(at))seq_along(z$n)else at,horizontal=horizontal)),print=FALSE,where=asNamespace('graphics'))\n" + selectedScript + $$"""

                stopifnot(identical(as.numeric(ticks),seq(200,400,by=25)))
                stopifnot(identical(box_plot$names,c('Edited Before','Edited After')))
                stopifnot(identical(.tukey_draw$horizontal,{{(horizontal ? "TRUE" : "FALSE")}}))
                dev.off();cat('TUKEY_SELECTED_OK\n')
                """, Path.Combine(folder, "selected-" + horizontal + "-" + scale));
            Check(output.Contains("TUKEY_SELECTED_OK"), "selected Tukey numeric/log scale, ticks and grid run as one ordinary R chart");
        }

        JsonNode sparse = JsonNode.Parse(record.GetRawText());
        sparse["inputs"] = JsonNode.Parse("""[{"name":"data","kind":"entered","columns":[{"title":"Sparse","values":[0,1,2,3,4,5,6,7,8,9,10,20,100]}]}]""");
        sparse.AsObject().Remove("data");
        sparse["parameters"]["boxWhisker"]["seriesTitles"] = JsonNode.Parse("[\"Sparse\"]");
        sparse["parameters"]["boxWhisker"]["valueMinimum"] = 0;
        sparse["parameters"]["boxWhisker"]["valueMaximum"] = 100;
        string sparseScript = RScriptWriter.Write(JsonDocument.Parse(sparse.ToJsonString()).RootElement, null, null).Text;
        string sparseOutput = RunR("svg('sparse.svg')\n" + sparseScript + "\nstopifnot(identical(box_plot$out,c(20,100)),identical(far,c(FALSE,TRUE)));dev.off();cat('TUKEY_OUTER_OK\\n')\n", Path.Combine(folder, "selected-sparse"));
        Check(sparseScript.Contains("far <-") && sparseOutput.Contains("TUKEY_OUTER_OK"),
            "additional outer-fence marks are emitted only when selected and distinguish actual sparse-data outliers");
    }
}
