using System.Text.Json;
using System.Xml.Linq;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // Both the options dialog and rendering see the acquired column through an aliased chart series.
    // Sorting that series must not rewrite the selected data, its saved snapshot or the locator hash.
    private static void CheckBoxWhiskerDataOrder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-box-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        bool passed = false;
        try
        {
            foreach (int n in new[] { 5001, 5 })
            {
                double[] original = Enumerable.Range(1, n).Reverse().Select(x => (double)x).ToArray();
                double[] acquired = (double[])original.Clone();
                string originalHash = DefinedHash(original.Select(x => x.ToString("R", inv)));
                const string label = "Descending values";
                DataFrame data = SheetFrame(Workbook, "Descending fixture", 0, 0, (label, acquired));
                ChartDefinition chart = null;
                var (run, _) = Run("BoxWhiskerPlot", new() { ["data"] = data }, amend: fillable =>
                {
                    chart = (ChartDefinition)fillable;
                    // Opening the real options dialog asks for these scale parameters before it records the run.
                    ScaleParameters scales = chart.ScaleParameters;
                    scales.X.ScaleType = ScaleType.Linear;
                    scales.X.AxisScale = new LinearAxisScale(1, n, 0, n + 1, 5);
                    var options = (BoxWhiskerOptions)chart.ChartOptions;
                    options.Cco = 0.95;
                    options.XAxisTitle = "Measurements";
                });
                Check(acquired.SequenceEqual(original) && ((DoubleVariable)data.Variables[0]).Data.SequenceEqual(original)
                      && ((DoubleSeries)chart.YSeries[0]).Data.SequenceEqual(original),
                    "opening box plot options preserves the acquisition order of " + n + " selected worksheet values");
                JsonElement record = Record(run);
                ColumnPointer pointer = FramePointer.From(record.GetProperty("inputs")[0]).Columns[0];
                Check(pointer.Hash == originalHash
                      && ColumnLocator.Find(pointer, column => column == pointer.Column ? (label, originalHash) : null) == pointer.Column,
                    "the " + n + "-value box plot locator hash identifies the unchanged worksheet column in its original order");
                if (n <= RunRecord.EmbeddingThreshold)
                    Check(record.GetProperty("data").GetProperty("data").GetProperty(label).EnumerateArray().Select(x => x.GetDouble()).SequenceEqual(original),
                        "the small box plot's saved data snapshot preserves the selected row order");
                else
                    Check(!record.TryGetProperty("data", out _), "the 5,001-value box plot keeps only the worksheet pointer and original-order hash");

                Type factoryType = typeof(ChartDefinition).Assembly.GetType("StatsDirect.Charting.SvgCanvasFactory", true);
                ICanvasFactory factory = (ICanvasFactory)Activator.CreateInstance(factoryType, true);
                foreach (string view in new[] { "horizontal", "vertical", "text" })
                {
                    ChartDefinition rendering = chart.Clone();
                    rendering.IsAscii = view == "text";
                    ((BoxWhiskerOptions)rendering.ChartOptions).Orientation = view == "vertical" ? ChartOrientation.Vertical : ChartOrientation.Horizontal;
                    using IChartRenderer renderer = ChartRendererFactory.ChartRendererFor(rendering, factory);
                    renderer.Plot(null, false);
                    bool rendered;
                    if (rendering.IsAscii) rendered = !string.IsNullOrWhiteSpace(renderer.GetAscii()) && renderer.GetAscii().Contains('*');
                    else
                    {
                        using Stream picture = renderer.Canvas.DetachAndReturnImageStream();
                        rendered = XElement.Load(picture).Name.LocalName == "svg";
                    }
                    Check(rendered && acquired.SequenceEqual(original)
                          && ((DoubleVariable)data.Variables[0]).Data.SequenceEqual(original)
                          && ((DoubleSeries)chart.YSeries[0]).Data.SequenceEqual(original),
                        "rendering the native " + view + " " + n + "-value box plot sorts only its private copy and leaves the recorded input unchanged");
                }

                bool read = false;
                FrameValues Read(FramePointer frame)
                {
                    read = true;
                    // This is the workbook reader's actual hash/Changed rule applied to the unchanged original column.
                    DoubleVariable reread = new((double[])original.Clone(), label);
                    FrameValues values = new();
                    values.Columns.Add(new ColumnValues
                    {
                        Title = label, Values = RunRecord.Values(reread),
                        Changed = frame.Columns[0].Hash != null && RunRecord.Hash(reread) != frame.Columns[0].Hash
                    });
                    return values;
                }
                RScript script = RScriptWriter.Write(record, null, Read);
                Check(!script.DataUnavailable && !script.Text.Contains("has changed in the workbook")
                      && read == (n > RunRecord.EmbeddingThreshold),
                    "Continue in R reads the unchanged " + n + "-value column without a false changed-data warning");
                string rFolder = Path.Combine(folder, n.ToString(inv));
                string output = RunBoxR("svg('box-%03d.svg')\n" + script.Text + $$"""

                    stopifnot(identical(as.double(mets[[1L]]), as.double({{n}}:1)),
                              identical(box_summaries[[1L]]$x, as.double(1:{{n}})))
                    dev.off()
                    cat("ORDER VERIFIED\n")
                    """, rFolder);
                Check(output.Contains("ORDER VERIFIED") && Directory.GetFiles(rFolder, "box-*.svg").Length == 1,
                    "the " + n + "-value R recipe retains the source row order while calculating its sorted summary and one chart");
            }
            passed = true;
        }
        finally
        {
            if (passed && Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(folder, true);
            else if (!passed) Console.WriteLine("      box plot order failure artifacts: " + folder);
        }
    }
}
