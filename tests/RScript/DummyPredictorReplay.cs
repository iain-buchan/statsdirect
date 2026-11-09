using System.Reflection;
using System.Text.Json;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

internal static partial class Program
{
    // Exercise the real frame request, dummy coding, acquisition snapshot and
    // recorded-column reread. A mixed predictor frame has no worksheet origin;
    // it must keep its aligned outcome/event/stratum frames at the same length.
    public class AlignedGrid : DispatchProxy, IGetCells
    {
        internal IGetCells Cells;
        internal CellSelection Selection;
        internal ITemplateHost Host;
        protected override object Invoke(MethodInfo method, object[] args)
        {
            if (method.Name == "ClearSelection") return null;
            if (method.Name != "GetCellArray" || Selection == null)
                throw new InvalidOperationException("Unexpected grid request: " + method.Name);
            args[8] = false; args[9] = false; // the grid's out flags: no pivot and no cancellation
            CellSelection selected = Selection; Selection = null;
            Type type = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.CellArrayProcessor");
            var mode = (DataAcquisitionMode)args[1];
            WorksheetOrigin reference = args.Length > 11 ? args[11] as WorksheetOrigin : null;
            if (mode != DataAcquisitionMode.NumericCodingTextToDummiesPreserveRows)
                return type.GetMethod("ProcessCellArrayWithReference", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { selected, mode, (int)args[0], false, false, (int)args[10], "Aligned model", reference });
            DataFrame frame = (DataFrame)type.GetMethod("ProcessCellArrayNumericCodingPreserveRows", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { selected, (int)args[0], false, false, (int)args[10], Host, reference });
            if (frame != null && frame.VariableCount > 1)
                foreach (IVariable v in frame.Variables.Where(v => v.Length == 0).ToList()) frame.Variables.Remove(v);
            return frame;
        }
        public string GetColumnTitle(int c) => Cells.GetColumnTitle(c);
        public bool IsFormattedLikeATitle(int c, int r) => Cells.IsFormattedLikeATitle(c, r);
        public string GetCellText(int r, int c) => Cells.GetCellText(r, c);
        public double GetCellValue(int r, int c) => Cells.GetCellValue(r, c);
        public (string[], int) GetCellTexts(int c, int f, int l) => Cells.GetCellTexts(c, f, l);
        public (double[], int) GetCellValues(int c, int f, int l) => Cells.GetCellValues(c, f, l);
        public (object[,], int) GetCellObjects(int c, int f, int l) => Cells.GetCellObjects(c, f, l);
        public (string[], int) GetCellFormulae(int c, int f, int l) => Cells.GetCellFormulae(c, f, l);
        public (DateTime[], int) GetCellDateValues(int c, int f, int l) => Cells.GetCellDateValues(c, f, l);
    }

    private static void CheckDummyPredictorReplay(string folder)
    {
        Directory.CreateDirectory(folder);
        IList<ParameterBag> Block(ParameterBag b, string key) => (IList<ParameterBag>)b[b.ContainsKey(key) ? key : "*" + key].AsObject;
        double Number(ParameterBag b, string key) => Convert.ToDouble(b[key].AsObject, inv);
        string Vec(IEnumerable<double> values) => RScriptWriter.Vector(values.Cast<object>().ToArray());
        string Match(IEnumerable<double> expected) => "\nstopifnot(isTRUE(all.equal(unname(coef(fit))," + Vec(expected) + ",tolerance=1e-5,check.attributes=FALSE)))\n";
        foreach (string model in new[] { "CoxRegression", "MultipleLinearRegression", "LogisticRegression", "PoissonRegression", "ConditionalLogisticRegression" })
        foreach (int total in new[] { 60, 2500, 10000 })
            Exercise(model, total, 30, false);
        foreach (string model in new[] { "CoxRegression", "MultipleLinearRegression", "LogisticRegression", "PoissonRegression" })
        foreach (int total in new[] { 26, 6000 })
            Exercise(model, total, 25, true);
        Exercise("ConditionalLogisticRegression", 60, 24, true);
        Exercise("ConditionalLogisticRegression", 6000, 2600, true);

        void Exercise(string model, int total, int observations, bool missing)
        {
            string label = model + "-" + total + "-" + observations + "-" + missing;
            object[][] raw = Enumerable.Range(0, 4).Select(_ => new object[total]).ToArray();
            raw[0][0] = model == "ConditionalLogisticRegression" ? "Matched set" : "Outcome";
            raw[1][0] = model == "CoxRegression" ? "Event" : model == "ConditionalLogisticRegression" ? "Case" : "Age";
            raw[2][0] = "Group";
            string[] levels = { "control", "low", "high" };
            for (int k = 0; k < observations; k++)
            {
                double age = 20 + k * .371 + (k * 7 % 23);
                int group = (k * 5 + k / 4) % 3;
                raw[1][k + 1] = age; raw[2][k + 1] = levels[group];
                raw[0][k + 1] = model switch
                {
                    "LogisticRegression" => k * 17 % 13 < 5 + group ? 1d : 0d,
                    "PoissonRegression" => (double)(2 + k * 11 % 9 + group),
                    _ => 3 + .4 * age + group * 1.7 + (k * 13 % 7) * .31
                };
                if (model == "CoxRegression")
                {
                    raw[0][k + 1] = 4 + k * 13 % 37 + .25 * (k % 3);
                    raw[1][k + 1] = k % 5 != 4 ? 1d : 0d;
                    raw[2][k + 1] = levels[(k * 7 + k / 3) % 3];
                }
                if (model == "ConditionalLogisticRegression")
                {
                    raw[0][k + 1] = "S" + (k / 2 + 1);
                    raw[1][k + 1] = k % 2 == 0 ? 1d : 0d;
                    raw[2][k + 1] = levels[(k % 2 == 0 ? k * 5 / 3 : k * 2 / 5) % 3];
                }
            }
            if (missing && model != "ConditionalLogisticRegression")
                for (int k = observations - 2; k <= observations; k++) raw[0][k] = null;
            if (missing && model == "ConditionalLogisticRegression")
                for (int k = observations + 1; k < Math.Min(total, observations + 11); k++) raw[2][k] = levels[k % 3];
            var cells = new RecordCells(raw);
            Dictionary<string, (int length, object[][] values)> acquired = new();
            object Select(string name, params int[] columns) => new Func<ITemplateProcessor, Parameter, ParameterBag, ITemplateHost, ParameterBag>((processor, parameter, context, host) =>
            {
                IGrid grid = DispatchProxy.Create<IGrid, AlignedGrid>();
                var proxy = (AlignedGrid)(object)grid; proxy.Cells = cells; proxy.Host = host;
                proxy.Selection = new CellSelection { LongestRowCount = total };
                foreach (int c in columns) proxy.Selection.ColumnSelections.Add(new CellColumnSelection(cells) { ColumnIndex = c, RowIndex = 0, RowCount = total, WorkbookPath = Workbook, WorksheetName = "Aligned models" });
                Type type = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
                ParameterBag filled;
                try { filled = (ParameterBag)type.GetMethod("FillFrameParameter", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Activator.CreateInstance(type, grid), new object[] { parameter, processor, context }); }
                catch (TargetInvocationException ex) { throw new InvalidOperationException(ex.GetBaseException().ToString(), ex); }
                DataFrame frame = filled[parameter.Name].AsDataFrame;
                acquired[name] = (frame.MaxRows, frame.Variables.Select(RunRecord.Values).ToArray());
                return filled;
            });
            Dictionary<string, object> answers;
            if (model == "CoxRegression") answers = new() { ["times"] = Select("times", 0), ["events"] = Select("events", 1), ["predictors"] = Select("predictors", 2), ["strata"] = null, ["accuracy"] = 1e-9, ["centre-continuous-covariates"] = true, ["useTimesAdjustment"] = false };
            else if (model == "ConditionalLogisticRegression") answers = new() { ["stratum"] = Select("stratum", 0), ["case-control"] = Select("case-control", 1), ["predictors"] = Select("predictors", 2), ["gamma"] = .95, ["show-counts"] = true, ["accuracy"] = "0.0000001" };
            else if (model == "MultipleLinearRegression") answers = new() { ["outcome"] = Select("outcome", 0), ["predictors"] = Select("predictors", 1, 2), ["ci"] = .95, ["calculateIntercept"] = true, ["useWeights"] = false };
            else answers = new() { ["response"] = Select("response", 0), ["predictors"] = Select("predictors", 1, 2), ["gamma"] = .95, ["grouping"] = "individual", ["accuracy"] = "0.000000001", ["intercept"] = true, ["weights"] = false, ["has-exposure"] = "false" };
            var (run, output) = Run(model, answers);
            int kept = acquired.Values.SelectMany(a => a.values).Select(v =>
            { int count = v.Length; while (count > 0 && v[count - 1] == null) count--; return count; }).Max();
            JsonElement record = Record(run);
            ValidateRecord(record, false);
            double[] coefficients;
            double nativeN;
            var bag = output.ParameterBag;
            if (model == "CoxRegression") { coefficients = Block(bag, "pred").Select(b => Number(b, "b")).ToArray(); nativeN = Number(bag, "n"); }
            else if (model == "ConditionalLogisticRegression")
            {
                coefficients = Block(bag, "est").Select(b => Number(b, "b")).ToArray();
                nativeN = Block(bag, "counts").SelectMany(b => Block(b, "count")).Sum(b => Number(b, "ca") + Number(b, "ct"));
            }
            else if (model == "MultipleLinearRegression")
            {
                coefficients = AcquisitionCoefficients(bag);
                var (_, anova) = Run("MultipleLinearRegressionAnova", new() { ["ci"] = .95 }, bag);
                nativeN = Number(anova.ParameterBag, "tot_df") + 1;
            }
            else
            {
                var (_, modelOutput) = Run(model == "LogisticRegression" ? "LogisticRegressionModel" : "PoissonRegressionModel", new(), bag);
                var modelBag = modelOutput.ParameterBag;
                coefficients = Block(modelBag, model == "LogisticRegression" ? "parameters" : "unscaled").Select(b => Number(b, "coef")).ToArray();
                nativeN = Number(modelBag, "idf") + Number(modelBag, "rank");
            }
            int expectedN = observations - (missing && model != "ConditionalLogisticRegression" ? 3 : 0);
            Check(nativeN == expectedN, label + ": native model fits the expected complete observations");
            Execute(record, coefficients, nativeN, label);
            var pointerRecord = JsonDocument.Parse(RunRecord.Write(run, bag, threshold: 0)).RootElement.Clone();
            ValidateRecord(pointerRecord, false);
            Execute(pointerRecord, coefficients, nativeN, label + "-pointer-only");
            if (model is "CoxRegression" or "MultipleLinearRegression")
            {
                var (follow, _) = Run(model == "CoxRegression" ? "CoxModelAnalysis" : "MultipleLinearRegressionAnova", model == "CoxRegression" ? new() : new() { ["ci"] = .95 }, bag);
                JsonElement inherited = Record(follow); ValidateRecord(inherited, true);
                Execute(inherited, coefficients, nativeN, label + "-follow-on");
            }

            void ValidateRecord(JsonElement saved, bool inherited)
            {
                var predictors = AcquisitionInput(saved, "predictors");
                Check(predictors.GetProperty("kind").GetString() == "entered" && predictors.GetProperty("columns").EnumerateArray().All(c => c.GetProperty("values").GetArrayLength() == kept), label + ": dummy-coded predictor frame shares the same observed-row cutoff");
                foreach (var pair in acquired.Where(p => p.Key != "predictors"))
                {
                    var input = AcquisitionInput(saved, pair.Key);
                    Check(input.GetProperty("columns").EnumerateArray().Select((c, i) => c.GetProperty("rows")[1].GetInt32() == kept + 1 && c.GetProperty("hash").GetString() == RunRecord.Hash(pair.Value.values[i].Take(kept).ToArray())).All(x => x), label + ": " + pair.Key + " retains physical bounds and hash of the original aligned prefix");
                    if (saved.TryGetProperty("data", out var held) && held.TryGetProperty(pair.Key, out var data))
                        Check(data.EnumerateObject().All(p => p.Value.GetArrayLength() == kept), label + ": held pointer values remain aligned");
                }
                if (inherited) Check(predictors.GetProperty("inherited").GetBoolean(), label + ": follow-on inherits aligned dummy predictor acquisition");
                Check(!saved.GetRawText().Contains("AlignedEntered"), label + ": runtime safeguard does not change saved record format");
            }
            FrameValues Reread(FramePointer pointer)
            {
                CellSelection selection = new();
                var mode = Enum.Parse<DataAcquisitionMode>(pointer.Mode);
                var origins = pointer.Columns.Select(c => new WorksheetOrigin(Workbook, c.Sheet, c.Column - 1, c.FirstRow - 1, c.LastRow - c.FirstRow + 1, mode, c.HasTitle, false, 0)).ToList();
                foreach (var o in origins) { selection.ColumnSelections.Add(new CellColumnSelection(cells) { ColumnIndex = o.Column, RowIndex = o.TopRow, RowCount = o.Rows }); selection.LongestRowCount = o.Rows; }
                DataFrame frame = (DataFrame)typeof(CellSelection).Assembly.GetType("StatsDirect.UI.frmSpreadsheetGear").GetMethod("ProcessRecordedColumns", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { selection, origins, "Aligned models" });
                FrameValues result = new();
                for (int i = 0; i < frame.VariableCount; i++) result.Columns.Add(new ColumnValues { Title = pointer.Columns[i].Title, Values = RunRecord.Values(frame.Variables[i]), Changed = RunRecord.Hash(frame.Variables[i]) != pointer.Columns[i].Hash });
                Check(result.Columns.All(c => !c.Changed && c.Values.Length == kept), label + ": recorded columns reread with identical hash and aligned length");
                return result;
            }
            void Execute(JsonElement saved, double[] expected, double n, string file)
            {
                var script = RScriptWriter.Write(saved, null, Reread);
                Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), file + ": replay recognises unchanged acquisition");
                string count = model is "CoxRegression" or "ConditionalLogisticRegression" ? "fit$n" : "nobs(fit)";
                Check(RunR(script.Text + Match(expected) + "stopifnot(" + count + "==" + n.ToString("R", inv) + ")\ncat('ALIGNED_MODEL_OK\\n')\n", Path.Combine(folder, file)).Contains("ALIGNED_MODEL_OK"), file + ": native and R sample size and coefficients agree");
            }
        }
        // Helpers such as predictor titles are entered frames too, but are not
        // acquired row-aligned observations and must not suppress normal trimming.
        var control = new RecordCells(new object[] { "Outcome", 2d, 4d, null, null, null });
        DataFrame aligned = control.Acquire(0, 0, 6, DataAcquisitionMode.NumericPreserveRows);
        ParameterBag values = new(); values.AddInput("outcome", aligned);
        DataFrame titles = new(); titles.Variables.Add(new StringVariable(new[] { "Age" }, "Titles")); values.AddInput("predictorTitles", titles);
        OperationRun helper = new(new Operation { Name = "AlignedHelperControl" });
        helper.Note(new FrameParameter { Name = "outcome", PreserveRowAlignment = true }, "Outcome", value: values["outcome"]);
        helper.Note(new FrameParameter { Name = "predictorTitles" }, "Titles", value: values["predictorTitles"]);
        var trimmed = AcquisitionRecord(helper, values);
        Check(AcquisitionInput(trimmed, "outcome").GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == 3, "entered helper frame does not disable blank-tail trimming");
        string defense = "stratum<-1:2\ncase<-c(1,0,1,0)\npredictors<-list(x=1:4)\nconfidence<-.95\ntryCatch({\n" + File.ReadAllText(Path.Combine(Metadata("Recipes"), "recipes", "conditional-logistic.R")) + "\nstop('accepted unequal lengths')\n},error=function(e){stopifnot(grepl('must have equal lengths',conditionMessage(e),fixed=TRUE));cat('LENGTH_DEFENSE_OK\\n')})\n";
        Check(RunR(defense, Path.Combine(folder, "conditional-length-defense")).Contains("LENGTH_DEFENSE_OK"), "conditional logistic refuses recyclable unequal input lengths before constructing data");
    }
}
