using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using StatsDirect.Builtins;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // Provenance describes the user's selection before an algorithm sorts or removes incomplete records.
    private static void CheckAcquisition()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-acquisition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        foreach (int count in new[] { 12, 5001 })
            foreach (bool missing in new[] { false, true }) CheckPolynomialAcquisition(count, missing, folder);
        CheckAcquisitionCopies();
        CheckOtherChartOrder();
        Console.WriteLine("      acquisition regression artifacts: " + folder);
    }

    private static JsonElement AcquisitionRecord(OperationRun run, ParameterBag bag) => JsonDocument.Parse(RunRecord.Write(run, bag)).RootElement.Clone();
    private static object[] AcquisitionValues(double[] values) => values.Select(value => RunRecord.Missing(value) ? null : (object)value).ToArray();
    private static string AcquisitionHash(double[] values) => DefinedHash(AcquisitionValues(values).Select(RunRecord.Text));
    private static JsonElement AcquisitionInput(JsonElement record, string name) => record.GetProperty("inputs").EnumerateArray().First(input => input.GetProperty("name").GetString() == name);
    private static double[] AcquisitionCoefficients(ParameterBag bag)
    {
        IList<ParameterBag> Block(ParameterBag b, string name) => (IList<ParameterBag>)b[b.ContainsKey(name) ? name : "*" + name].AsObject;
        return Block(Block(bag, "row")[0], "col").Select(column => Convert.ToDouble(column["val_b"].AsObject, inv)).ToArray();
    }

    private static void CheckPolynomialAcquisition(int count, bool missing, string folder)
    {
        string label = count + (missing ? "-missing" : "-complete");
        double[] originalX = Enumerable.Range(1, count).Reverse().Select(i => i / (double)count * 4).ToArray();
        double[] originalY = originalX.Select((x, i) => 2 + 3 * x + 0.4 * x * x + Math.Sin(i * 1.7) * 0.2).ToArray();
        if (missing) { originalX[2] = Constant.MISSING; originalY[7] = Constant.MISSING; }
        var x = SheetFrame(Workbook, "Polynomial source", 0, 0, ("X", (double[])originalX.Clone()));
        var y = SheetFrame(Workbook, "Polynomial source", 1, 0, ("Y", (double[])originalY.Clone()));
        var (run, output) = Run("PolynomialRegression", new() { ["x"] = x, ["y"] = y, ["degree"] = "2" });
        JsonElement record = Record(run);
        bool originalPointers = AcquisitionInput(record, "x").GetProperty("columns")[0].GetProperty("hash").GetString() == AcquisitionHash(originalX)
            && AcquisitionInput(record, "y").GetProperty("columns")[0].GetProperty("hash").GetString() == AcquisitionHash(originalY);
        Check(originalPointers && AcquisitionInput(record, "x").GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == count + 1,
            "polynomial " + label + " records original row ranges and paired acquisition hashes before sorting/dropping");
        bool embedded = count * 2 <= RunRecord.EmbeddingThreshold;
        Check(record.TryGetProperty("data", out JsonElement data) == embedded && (!embedded ||
              data.GetProperty("x").GetProperty("X").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Null ? null : (object)value.GetDouble()).SequenceEqual(AcquisitionValues(originalX))),
            "polynomial " + label + " preserves small original snapshots and stores no large worksheet copy");
        bool changed = false, read = false;
        FrameValues Read(FramePointer pointer)
        {
            read = true;
            double[] values = (double[])(pointer.Name == "x" ? originalX : originalY).Clone();
            if (changed && pointer.Name == "x") values[0] += 0.5;
            FrameValues result = new();
            result.Columns.Add(new ColumnValues { Title = pointer.Columns[0].Title, Values = AcquisitionValues(values), Changed = AcquisitionHash(values) != pointer.Columns[0].Hash });
            return result;
        }
        string saved = Path.Combine(folder, label + ".json");
        File.WriteAllText(saved, record.GetRawText());
        JsonElement reopened = JsonDocument.Parse(File.ReadAllText(saved)).RootElement.Clone();
        RScript script = RScriptWriter.Write(reopened, null, Read);
        Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook") && read == !embedded,
            "polynomial " + label + " saved/reopened R script recognises the unchanged original worksheet");
        double[] expected = AcquisitionCoefficients(output.ParameterBag);
        var (_, anovaOutput) = Run("MultipleLinearRegressionAnova", new() { ["ci"] = 0.95 }, output.ParameterBag);
        double dw = Convert.ToDouble(anovaOutput.ParameterBag["dw"].AsObject, inv);
        var (area, areaOutput) = Run("PolynomialAreaUnderCurve", new(), output.ParameterBag);
        double trapezoid = Convert.ToDouble(areaOutput.ParameterBag["trap_auc"].AsObject, inv);
        string outputR = RunR(script.Text + $$"""

            expected <- c({{string.Join(",", expected.Select(value => value.ToString("R", inv)))}})
            stopifnot(length(size) == {{count - (missing ? 2 : 0)}}L,
                      all(diff(size) >= 0), all(abs(coef(fit) - expected) < 1e-7 * pmax(1, abs(expected))),
                      abs(sum(diff(residuals(fit))^2) / sum(residuals(fit)^2) - {{dw.ToString("R", inv)}}) < 1e-8,
                      abs(trapezia - {{trapezoid.ToString("R", inv)}}) < 1e-8 * max(1, abs(trapezia)))
            cat("POLYNOMIAL ACQUISITION VERIFIED\n")
            """, Path.Combine(folder, label));
        Check(outputR.Contains("POLYNOMIAL ACQUISITION VERIFIED"),
            "polynomial " + label + " explicitly preprocesses originals in R and matches native coefficients, Durbin-Watson and trapezoidal area");
        var (follow, _) = Run("PolynomialRegressionChangeDegree", new() { ["degree"] = "3" }, output.ParameterBag);
        JsonElement followRecord = Record(follow);
        Check(AcquisitionInput(followRecord, "x").GetProperty("inherited").GetBoolean()
              && AcquisitionInput(followRecord, "x").GetProperty("columns")[0].GetProperty("hash").GetString() == AcquisitionHash(originalX)
              && !RScriptWriter.Write(followRecord, null, Read).Text.Contains("has changed in the workbook"),
            "polynomial " + label + " degree-change follow-on carries the original acquisition rather than transformed fit arrays");
        if (!embedded)
        {
            changed = true;
            Check(RScriptWriter.Write(reopened, null, Read).Text.Contains("\"X\" has changed in the workbook"),
                "polynomial " + label + " still warns when the worksheet values really change");
        }
    }

    private static void CheckAcquisitionCopies()
    {
        Operation operation = Load("TPaired");
        var values = new[] { 3.0, 1.0, 2.0 };
        var frame = SheetFrame(Workbook, "Original sheet", 2, 4, ("Original title", values));
        ParameterBag bag = new(); bag.AddInput("data", frame);
        OperationRun run = new(operation);
        run.Note(new FrameParameter { Name = "data" }, "Selected column", value: bag["data"]);
        values[0] = 999;
        frame.Variables[0].Title = "Later title";
        ((WorksheetOrigin)frame.Variables[0].Origin).WorksheetName = "Later sheet";
        JsonElement record = AcquisitionRecord(run, bag);
        Check(record.GetProperty("data").GetProperty("data").GetProperty("Original title")[0].GetDouble() == 3
              && AcquisitionInput(record, "data").GetProperty("sheet").GetString() == "Original sheet",
            "acquired titles, worksheet origins and values are immutable after their live objects change");
        OperationRun inherited = new(operation, bag);
        values[1] = 888;
        JsonElement inheritedRecord = AcquisitionRecord(inherited, bag);
        Check(AcquisitionInput(inheritedRecord, "data").GetProperty("inherited").GetBoolean()
              && inheritedRecord.GetProperty("data").GetProperty("data").GetProperty("Original title")[1].GetDouble() == 1,
            "an inherited frame reuses its original acquisition snapshot rather than rereading altered analysis arrays");
        DataFrame replacement = new(); replacement.Variables.Add(Column("Replacement", new[] { 9.0, 8.0 }));
        bag["data"] = FilledParameterFactory.Input(replacement);
        inherited.Note(new FrameParameter { Name = "data" }, "Fresh selection", value: bag["data"]);
        JsonElement replacementRecord = AcquisitionRecord(inherited, bag);
        Check(AcquisitionInput(replacementRecord, "data").GetProperty("kind").GetString() == "entered"
              && AcquisitionInput(replacementRecord, "data").GetProperty("columns")[0].GetProperty("values")[0].GetDouble() == 9,
            "a fresh acquisition of an inherited parameter overrides its earlier snapshot");

        DataFrame2D nested = new(Column("Nested", new[] { 4.0, 3.0 }));
        ParameterBag nestedBag = new(); nestedBag.AddInput("data", nested);
        OperationRun nestedRun = new(operation);
        nestedRun.Note(new Frame2DParameter { Name = "data" }, "Nested selection", value: nestedBag["data"]);
        ((DoubleVariable)nested.Variables[0][0]).Data[0] = 777;
        JsonElement nestedRecord = AcquisitionRecord(nestedRun, nestedBag);
        Check(AcquisitionInput(nestedRecord, "data").GetProperty("value").GetProperty("frames")[0][0].GetProperty("values")[0].GetDouble() == 4
              && nestedRecord.GetProperty("parameters").GetProperty("data").GetProperty("frames")[0][0].GetProperty("values")[0].GetDouble() == 4,
            "nested acquired data stay immutable in both input history and recipe parameters");
        OperationRun loops = new(operation);
        double[] loopValues = { 10, 20 }; var loopFrame = SheetFrame(Workbook, "Loops", 0, 0, ("Loop", loopValues));
        ParameterBag loopBag = new(); loopBag.AddInput("data", loopFrame);
        loops.Note(new FrameParameter { Name = "data" }, "First", "0", loopBag["data"]);
        loopValues[0] = 30;
        loops.Note(new FrameParameter { Name = "data" }, "Second", "1", loopBag["data"]);
        JsonElement loopRecord = AcquisitionRecord(loops, loopBag);
        Check(loopRecord.GetProperty("data").GetProperty("data").GetProperty("Loop")[0].GetDouble() == 10
              && loopRecord.GetProperty("data").GetProperty("data#2").GetProperty("Loop")[0].GetDouble() == 30,
            "repeated loop inputs keep distinct acquisition-time copies even when the host reuses a variable");
        OperationRun table = new(operation);
        ParameterBag cells = new(); foreach (string name in new[] { "a", "b", "c", "d" }) cells.AddInput(name, 2.0);
        table.Note(new Double2By2Parameter { Name = "counts", TopLeftName = "a", TopRightName = "b", BottomLeftName = "c", BottomRightName = "d" },
            "Table", acquisitionParameters: cells);
        cells["a"] = FilledParameterFactory.Input(99.0);
        Check(AcquisitionRecord(table, cells).GetProperty("inputs")[0].GetProperty("counts")[0][0].GetDouble() == 2,
            "a typed two-by-two table preserves its acquired cells when later parameters change");
    }

    private static void CheckOtherChartOrder()
    {
        foreach (string operation in new[] { "SpreadPlot", "HistogramPlot", "HistogramPlotText" })
        {
            double[] original = { 8, 3, 7, 2, 6, 1, 5, 4, 4, 3, 2, 5 };
            double[] acquired = (double[])original.Clone();
            var frame = SheetFrame(Workbook, "Chart source", 0, 0, ("Selected", acquired));
            ChartDefinition chart = null;
            var (run, _) = Run(operation, new() { ["data"] = frame }, amend: fillable =>
            {
                chart = (ChartDefinition)fillable;
                _ = chart.ScaleParameters;
            });
            Check(acquired.SequenceEqual(original) && AcquisitionInput(Record(run), "data").GetProperty("columns")[0].GetProperty("hash").GetString() == AcquisitionHash(original),
                operation + " scale discovery and acquisition preserve worksheet order");
            Type type = typeof(ChartDefinition).Assembly.GetType("StatsDirect.Charting.SvgCanvasFactory", true);
            ICanvasFactory factory = (ICanvasFactory)Activator.CreateInstance(type, true);
            foreach (bool vertical in operation == "SpreadPlot" ? new[] { false, true } : new[] { false })
            {
                ChartDefinition drawing = chart.Clone(); drawing.IsAscii = operation.EndsWith("Text");
                if (drawing.ChartOptions is SpreadOptions options) options.Orientation = vertical ? ChartOrientation.Vertical : ChartOrientation.Horizontal;
                using IChartRenderer renderer = ChartRendererFactory.ChartRendererFor(drawing, factory);
                renderer.Plot(null, false);
                bool rendered;
                if (drawing.IsAscii) rendered = !string.IsNullOrWhiteSpace(renderer.GetAscii());
                else { using Stream image = renderer.Canvas.DetachAndReturnImageStream(); rendered = XElement.Load(image).Name.LocalName == "svg"; }
                Check(rendered && acquired.SequenceEqual(original) && ((DoubleSeries)chart.YSeries[0]).Data.SequenceEqual(original),
                    operation + (vertical ? " vertical" : " horizontal/text") + " renders successfully using private sort copies");
            }
        }
    }
}
