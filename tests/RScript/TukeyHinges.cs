using System.Text.Json;
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
                && !script.Contains("sd_box_summary <- function") && script.Contains("boxplot("),
                "Tukey replay survives reopening and uses a compact native boxplot recipe");
            double coefficient = (fences & 1) != 0 ? 1.5 : (fences & 2) != 0 ? 3 : 0;
            string assertions = "\nexpected<-boxplot(mets,plot=FALSE,range=" + coefficient.ToString(inv) + ")\n" + $$"""
                stopifnot(length(box_plot$n)==2L,all(box_plot$n==9L))
                stopifnot(isTRUE(all.equal(unname(box_plot$stats),unname(expected$stats))))
                stopifnot(identical(positions,if({{(horizontal ? "TRUE" : "FALSE")}})2:1 else 1:2))
                stopifnot(length(box_plot$out)==length(expected$out))
                dev.off();cat('TUKEY_REPLAY_OK\n')
                """;
            string caseFolder = Path.Combine(folder, "replay-" + horizontal + "-" + fences);
            string output = RunR("svg('tukey-%03d.svg',width=7,height=6)\n" + script + assertions, caseFolder);
            Check(output.Contains("TUKEY_REPLAY_OK") && Directory.GetFiles(caseFolder, "tukey-*.svg").Length == 1,
                $"Tukey replay gives one native R chart in selected orientation {horizontal} with fence choice {fences} and original data");
        }

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
}
