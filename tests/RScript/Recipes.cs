using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.R;

internal static partial class Program
{
    // Worked examples compare every numerical figure with their independent R reference. Graphical recipes probe the arrays they
    // actually plotted, rather than requiring the extra demonstrations and teaching text that used to accompany the selected chart.
    private static void CheckRecipesAgainstHelp()
    {
        RRecipes.Folder = Metadata("Recipes");
        string examples = Metadata("Examples");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-recipes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string[] operations = RRecipes.Operations.OrderBy(o => o, StringComparer.Ordinal).ToArray();
        Check(operations.Length >= 28 && operations.All(op => File.Exists(Path.Combine(examples, op + ".json")) && File.Exists(Path.Combine(examples, op + ".R"))),
              $"every operation of the manifest ({operations.Length}) has the help's script and its example to check against");
        Dictionary<string, string> gold = new();   // independent figures per operation, including shared-recipe continuations
        int agreed = 0;
        List<string> failures = new();
        foreach (string op in operations)
        {
            try
            {
                JsonElement example = JsonDocument.Parse(File.ReadAllText(Path.Combine(examples, op + ".json"))).RootElement;
                string record = RecordOf(example);
                RScript script = RScriptWriter.Write(JsonDocument.Parse(record).RootElement, null, null);
                string runFolder = Path.Combine(folder, op);
                string scriptText = script.Text;
                string chartProbe = SelectedChartExampleProbe(op);
                string numericProbe = chartProbe ?? SelectedAnalysisExampleProbe(op);
                if (numericProbe != null)
                    scriptText = "options(device = function(...) grDevices::svg(filename = \"selected-chart-%03d.svg\", ...))\n"
                        + "invisible(capture.output({\n" + scriptText + "\n}))\n"
                        + "emit <- function(x) cat(formatC(as.double(x), digits = 8, format = 'f'), '\\n')\n" + numericProbe;
                if (op == "BoxWhiskerPlot")
                {
                    // This recipe now draws one selected chart instead of running the help's three teaching illustrations.
                    // Probe the statistics it actually plotted so the worked-example comparison still checks every numerical value.
                    runFolder = Path.Combine(runFolder, Guid.NewGuid().ToString("N"));
                    scriptText = "options(device = function(...) grDevices::svg(filename = \"selected-box-%03d.svg\", ...))\n" + scriptText + BoxWhiskerExampleProbe;
                }
                string output = RunR(scriptText, runFolder);
                if (chartProbe != null)
                    Check(Directory.GetFiles(runFolder, "selected-chart-*.svg").Length == 1,
                          op + " worked example draws one selected chart while every plotted value is compared independently");
                if (op == "BoxWhiskerPlot")
                    Check(Directory.GetFiles(runFolder, "selected-box-*.svg").Length == 1,
                          "the Cuzick worked-example recipe draws exactly one chart from the selected columns and recorded settings");
                string ours = Figures(output);
                if (example.TryGetProperty("nocompare", out JsonElement nc) && nc.ValueKind == JsonValueKind.True)
                {
                    // a recipe the help's script cannot be compared with figure for figure (random numbers; a calculator whose help
                    // works from data): it must run and print figures; its figures are checked elsewhere (Inverse.cs)
                    if (ours.Length > 0) agreed++; else failures.Add(op + " (printed no figure)");
                    continue;
                }
                if (!gold.TryGetValue(op, out string expected))
                    gold[op] = expected = Figures(RunR(File.ReadAllText(Path.Combine(examples, op + ".R")).Replace("\r", ""), Path.Combine(folder, op + "-help")));
                // the recipe's figures are the help's, or the start of them where a second example at the end of the help's script was left
                // out ("cut"), or every one of them is among the help's where the recipe covers one part of the help's script ("subset")
                bool cut = example.TryGetProperty("cut", out JsonElement c) && c.ValueKind == JsonValueKind.True;
                bool subset = example.TryGetProperty("subset", out JsonElement sb) && sb.ValueKind == JsonValueKind.True;
                HashSet<string> helpFigures = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                if (ours == expected || (cut && ours.Length > 0 && expected.StartsWith(ours + " ", StringComparison.Ordinal))
                    || (subset && ours.Length > 0 && ours.Split(' ').All(helpFigures.Contains))) agreed++;
                else
                {
                    failures.Add(op);
                    string[] a = ours.Split(' '), b = expected.Split(' ');
                    int i = 0;
                    while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
                    Console.WriteLine($"      {op}: the figures differ from the {i + 1}th: recipe {(i < a.Length ? a[i] : "(end)")}, help {(i < b.Length ? b[i] : "(end)")} ({a.Length} and {b.Length} figures)");
                }
            }
            catch (Exception ex)
            {
                failures.Add(op);
                Console.WriteLine($"      {op}: {ex.Message.Replace("\r", "").Replace("\n", " | ").Substring(0, Math.Min(400, ex.Message.Length))}");
            }
        }
        Check(failures.Count == 0, $"every recipe matches its independent worked-example numerical reference: {agreed} of {operations.Length} agree" + (failures.Count > 0 ? "; differing: " + string.Join(", ", failures) : ""));
    }

    private static string SelectedAnalysisExampleProbe(string operation) => operation switch
    {
        "Friedman" or "CochranQ" or "FriedmanSimulateExactP" => "emit(friedman_result)\nemit(R/b)\n",
        "FriedmanMulti" => "emit(c(critical_t,critical_difference))\nemit(c(friedman_comparisons$difference,friedman_comparisons$p,as.integer(friedman_comparisons$significant)))\n",
        "Ranv" => "emit(regression_anova)\n",
        "LinearizedEstimates" or "LinearizedEstimatesWithModel" or "LinearizedEstimatePlot" or "LinearizedEstimateInterpolation" => "emit(linearized_result)\n",
        _ => null
    };

    private static string SelectedChartExampleProbe(string operation) => operation switch
    {
        "BarPlot" => "emit(c(dim(bar_values), as.vector(bar_values)))\n",
        "BarPlotFrequency" => "emit(c(dim(counts), as.vector(counts), as.vector(frequency_values), dim(bar_values), as.vector(bar_values)))\n",
        "HistogramPlot" => "emit(length(histograms))\nfor (h in histograms) emit(c(h$n, h$breaks, h$counts, h$mids, h$heights))\n",
        "NormalPlot" => "emit(c(length(observed), normal_ranks, normal_scores, observed))\n",
        "SpreadPlot" => "stopifnot(spread_tolerance > 0, spread_tolerance < 1)\nemit(length(groups))\nfor (i in seq_along(groups)) { emit(c(length(groups[[i]]), groups[[i]])); emit(unlist(spread_stacks[[i]], use.names = FALSE)) }\n",
        "LinePlot" or "ScatterPlot" => "emit(length(xy_series))\nfor (s in xy_series) emit(c(length(s$x), s$x, s$y))\n",
        "LadderPlot" => "emit(c(length(ladder_before), ladder_before, ladder_after, ladder_difference))\n",
        "ErrorPlot" => "emit(length(error_series))\nfor (s in error_series) emit(c(length(s$x), s$x, s$y, s$lower, s$upper, s$plot_x))\n",
        "PyramidPlot" => "emit(c(length(pyramid_rows), pyramid_rows, pyramid_males, pyramid_females, pyramid_scale))\n",
        "ControlPlot" => "emit(c(length(control_y), control_x, control_y, control_mean, control_sd, control_bands))\n",
        "SurvivalPlot" => "emit(length(survival_curves))\nfor(s in survival_curves) emit(c(length(s$x),s$x,s$y,s$lower,s$upper,s$event))\n",
        "CochranePlot" => "emit(c(nrow(forest_rows),forest_rows$effect,forest_rows$lower,forest_rows$upper,forest_rows$weight,forest_rows$pooling,forest_rows$y))\n",
        "ROCPlot" => "emit(length(roc_series))\nfor(s in roc_series) emit(c(length(s$cuts),s$cuts,s$sensitivity,s$specificity,s$auc,s$theta,s$se,s$confidence,s$comparison,s$weight,s$cutoff,s$optimum,s$a,s$b,s$c,s$d,as.vector(s$proportions)))\n",
        _ => null
    };

    // Use the recipe's calculated summaries, rather than recomputing a second set for the numeric comparison.
    private const string BoxWhiskerExampleProbe = """

stopifnot(box_method == 1L, box_inner_fence, box_outer_fence,
          box_horizontal, !box_mark_mean_median,
          identical(box_title, "Selected Cuzick cell lines"),
          identical(box_axis_title, "Selected box-and-whisker settings"),
          identical(box_series_titles, names(mets)))
six <- function(x) formatC(x, digits = 6, format = "f", drop0trailing = TRUE)
for (name in names(box_summaries)) {
  s <- box_summaries[[name]]
  cat(paste0(name, ":"), "n =", s$n, " min =", six(s$x[1]), " LQ =", six(s$box[1]),
      " median =", six(s$centre), " UQ =", six(s$box[2]), " max =", six(s$x[s$n]), "\n")
  beyond <- s$x[s$x < s$inner[1] | s$x > s$inner[2]]
  if (length(beyond))
    cat("   inner fences", six(s$inner[1]), "to", six(s$inner[2]),
        " outer fences", six(s$outer[1]), "to", six(s$outer[2]),
        " beyond the inner fence:", six(beyond), "\n")
}

""";

    /// <summary>The record a run on the help's example would leave, from the example as the generator captured it.</summary>
    private static string RecordOf(JsonElement example)
    {
        List<string> inputs = new();
        if (example.TryGetProperty("inputs", out JsonElement named) && named.ValueKind == JsonValueKind.Array)
            foreach (JsonElement input in named.EnumerateArray())   // the example's frames under the names the operation asks them by
                inputs.Add("{\"name\":" + input.GetProperty("name").GetRawText() + ",\"prompt\":" + input.GetProperty("name").GetRawText() + ",\"kind\":\"entered\",\"columns\":" + input.GetProperty("columns").GetRawText() + "}");
        if (example.TryGetProperty("nested", out JsonElement nested) && nested.ValueKind == JsonValueKind.Array)
            foreach (JsonElement input in nested.EnumerateArray())   // a two-level input: its blocks or groups, each a list of columns, as the record holds a frame2d
                inputs.Add("{\"name\":" + input.GetProperty("name").GetRawText() + ",\"prompt\":" + input.GetProperty("name").GetRawText() + ",\"kind\":\"frame2d\",\"value\":{\"frames\":" + input.GetProperty("frames").GetRawText() + "}}");
        if (example.TryGetProperty("data", out JsonElement data))
            inputs.Add("{\"name\":\"data\",\"prompt\":\"Select data\",\"kind\":\"entered\",\"columns\":" + data.GetRawText() + "}");
        if (example.TryGetProperty("list", out JsonElement list))
            inputs.Add("{\"name\":\"data\",\"prompt\":\"Select data\",\"kind\":\"entered\",\"columns\":" + list.GetRawText() + "}");
        if (example.TryGetProperty("table", out JsonElement table) && table.ValueKind == JsonValueKind.Object)
        {
            JsonElement counts = table.GetProperty("counts");
            string columnsOf = "[" + string.Join(",", Enumerable.Range(0, counts[0].GetArrayLength()).Select(c => "{\"title\":" + (table.GetProperty("columnLabels").GetArrayLength() > c ? table.GetProperty("columnLabels")[c].GetRawText() : "\"\"") + ",\"values\":[" + string.Join(",", counts.EnumerateArray().Select(r => r[c].GetRawText())) + "]}")) + "]";
            string titles = (table.TryGetProperty("rowsTitle", out JsonElement rt) ? ",\"rowsTitle\":" + rt.GetRawText() : "") + (table.TryGetProperty("columnsTitle", out JsonElement ct) ? ",\"columnsTitle\":" + ct.GetRawText() : "");
            inputs.Add("{\"name\":\"scrap\",\"prompt\":\"Table\",\"kind\":\"table\",\"columns\":" + columnsOf + ",\"counts\":" + counts.GetRawText() + ",\"rowLabels\":" + table.GetProperty("rowLabels").GetRawText() + ",\"columnLabels\":" + table.GetProperty("columnLabels").GetRawText() + titles + "}");
        }
        if (example.TryGetProperty("matrix", out JsonElement matrix) && matrix.ValueKind == JsonValueKind.Object)
        {
            JsonElement counts = matrix.GetProperty("counts");
            string columnsOf = "[" + string.Join(",", Enumerable.Range(0, counts[0].GetArrayLength()).Select(c => "{\"title\":" + (matrix.GetProperty("columnLabels").GetArrayLength() > c ? matrix.GetProperty("columnLabels")[c].GetRawText() : "\"V" + (c + 1) + "\"") + ",\"values\":[" + string.Join(",", counts.EnumerateArray().Select(r => r[c].GetRawText())) + "]}")) + "]";
            inputs.Add("{\"name\":\"data\",\"prompt\":\"Select the table\",\"kind\":\"entered\",\"columns\":" + columnsOf + "}");
        }
        string parameters = example.TryGetProperty("settings", out JsonElement settings) ? settings.GetRawText() : "{}";
        string root = example.TryGetProperty("rootOperation", out JsonElement origin) ? ",\"rootOperation\":" + origin.GetRawText() : "";
        return "{\"version\":1,\"operation\":" + example.GetProperty("operation").GetRawText() + root + ",\"title\":\"Example\",\"run\":\"2026-10-11T03:00:00+01:00\",\"inputs\":[" + string.Join(",", inputs) + "],\"parameters\":" + parameters + ",\"values\":{}}";
    }

    /// <summary>The figures of an output, in order: every number, so that a difference in a label alone does not count; the row and
    /// column indices R prints for an unlabelled matrix ("[1,]") are not figures.</summary>
    private static string Figures(string output) => string.Join(" ", Regex.Matches(Regex.Replace(output, "\\[\\d*,\\d*\\]", " "), "-?\\d+(?:\\.\\d+)?(?:[eE][-+]?\\d+)?").Select(m => m.Value));
}
