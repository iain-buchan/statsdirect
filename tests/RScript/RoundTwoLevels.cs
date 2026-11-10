using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    private static void CheckRoundTwoLevels(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        List<(string label, string code)> cases = new();
        JsonNode Example(string operation)
        {
            using var example = JsonDocument.Parse(File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".json")));
            return JsonNode.Parse(RecordOf(example.RootElement));
        }
        string ExampleScript(JsonNode record)
        {
            using var document = JsonDocument.Parse(record.ToJsonString());
            return RScriptWriter.Write(document.RootElement, null, null).Text;
        }
        string ActualScript(OperationRun run, RecordCells cells = null) =>
            RScriptWriter.Write(Record(run), null, cells == null ? null : cells.Read).Text;
        void Replay(string label, string script, string assertion) =>
            cases.Add((label, "printed <- capture.output({\n" + script + "\n})\n" + assertion));
        string Printed(string line) => "stopifnot(any(grepl(" + RScriptWriter.Quote(line) + ", printed, fixed = TRUE)))\n";

        foreach ((string operation, string key) in new[]
        {
            ("PropSingle", "cco"), ("PropUnPaired", "cco"), ("PropPairs", "cco"), ("ExactSign", "cco"),
            ("ExactMcNamar", "gamma"), ("ExactORCML", "gamma"), ("RatePoissonCI", "cco"),
            ("RateCompareTwo", "gamma"), ("MiscRelRisk", "cco"), ("MiscRetroRisk", "cco"),
            ("MiscDiagnostic", "cco"), ("Chi2by2", "cco"), ("ReferenceRange", "gamma")
        })
        {
            foreach (int endpoint in new[] { 0, 1 })
            {
                JsonNode saved = Example(operation);
                saved["parameters"][key] = endpoint;
                Replay(operation + "-level-" + endpoint, ExampleScript(saved), "stopifnot(identical(confidence, 0.95))\n");
            }
        }

        foreach ((string operation, string key) in new[] { ("MiscRelRisk", "cco"), ("PropPairs", "cco"), ("LogRank", "gamma") })
        {
            JsonNode saved = Example(operation);
            saved["parameters"][key] = .90;
            Replay(operation + "-printed-level", ExampleScript(saved), Printed("90 percent confidence interval:") +
                "stopifnot(!any(grepl('95 percent confidence interval:', printed, fixed = TRUE)))\n");
        }

        foreach ((double level, string pct, string lower, string upper, string student, string welch, string single) in new[]
        {
            (.90, "90", "5.711295", "8.995605", "59.68", "61.46", "49.29"),
            (.95, "95", "5.346233", "9.508147", "46.32", "48.15", "36.01"),
            (.99, "99", "4.705103", "10.520203", "22.35", "23.70", "15.08")
        })
        {
            string significance = (100 - int.Parse(pct, inv)).ToString(inv);
            var poisson = Run("PoissonConfidenceInterval", new()
            {
                ["data"] = new DataFrame(Column("Arrivals", new double[] { 3, 7, 12, 5, 9 })), ["gamma"] = level
            });
            Replay("Poisson-one-sided-" + pct, ActualScript(poisson.run),
                Printed($"Exact one sided {pct}% confidence limits: lower {lower}, upper {upper}"));

            var unpaired = Run("TUnpairedSummary", new()
            {
                ["nx1"] = 12, ["um1"] = 10.5, ["sd1"] = 2.1, ["nx2"] = 15, ["um2"] = 12.3,
                ["sd2"] = 2.6, ["gamma"] = level
            });
            Replay("TUnpairedSummary-power-" + pct, ActualScript(unpaired.run),
                Printed($"Power (for {significance}% significance) = {student}%") +
                Printed($"Power (for {significance}% significance) = {welch}%"));

            var one = Run("TSingleSummary", new() { ["nx"] = 20, ["mu"] = 52.3, ["sd1"] = 6.1, ["mu0"] = 50d, ["gamma"] = level });
            Replay("TSingleSummary-power-" + pct, ActualScript(one.run),
                Printed($"Power (for {significance}% significance) = {single}%"));
        }

        JsonNode paired = Example("TPaired");
        JsonArray pairs = paired["inputs"][0]["columns"].AsArray();
        pairs[0]["values"] = JsonSerializer.SerializeToNode(new[] { 1.1, 2.2, 3.3, 4.7, 5.9, 6.4, 7.3 });
        pairs[1]["values"] = JsonSerializer.SerializeToNode(new[] { 1.2, 2.3, 3.4, 4.8, 6.0, 6.5, 7.4 });
        Replay("TPaired-tiny-spread", ExampleScript(paired), """
            line <- grep("^Standard deviation =", printed, value = TRUE)
            stopifnot(length(line) == 1L)
            shown <- as.numeric(strsplit(line, " +")[[1]][c(4, 8)])
            expected <- c(sd(before - after), sd(before - after) / sqrt(length(before)))
            stopifnot(all(shown > 0), all(abs(shown / expected - 1) < 1e-6))
            """);

        var cells = new RecordCells(new object[] { "First", "x", "y", "x", "z", "*" },
            new object[] { "Second", "10", "2", "p", "p", "A" });
        DataFrame data = cells.Acquire(0, 0, 6, DataAcquisitionMode.CategoryReplaceMissing, title: true);
        data.Variables.Add(cells.Acquire(1, 0, 6, DataAcquisitionMode.CategoryReplaceMissing, title: true).Variables[0]);
        foreach (string by in new[] { "value", "frequency" })
        foreach (string order in new[] { "asc", "desc" })
        {
            var frequency = Run("Frequency", new() { ["data"] = data, ["sortBy"] = by, ["sortOrder"] = order });
            string[] labels = Meta539Rows(frequency.output.ParameterBag, "*variable")
                .SelectMany(v => Meta539Rows(v, "*bin").Select(bin => bin["x"].AsString)).ToArray();
            Replay("Frequency-two-columns-" + by + "-" + order, ActualScript(frequency.run, cells),
                "rows <- grep('^.+ [0-9]+ (NA NA NA|[0-9.]+ [0-9]+ [0-9.]+)[[:space:]]*$', printed, value = TRUE)\n" +
                "labels <- sub(' [0-9]+ (NA NA NA|[0-9.]+ [0-9]+ [0-9.]+)[[:space:]]*$', '', rows)\n" +
                "stopifnot(sum(grepl('^Total =', printed)) == 2L, identical(labels, " + Meta5310Strings(labels) + "))\n");
        }

        foreach (string sided in new[] { "1", "2" })
        {
            JsonNode saved = Example("KruskalWallisMulti");
            saved["parameters"]["confidence"] = .90;
            saved["parameters"]["sided"] = sided;
            Replay("Kruskal-comparisons-" + sided + "-sided", ExampleScript(saved),
                "stopifnot(abs(critical_t - qt(" + (sided == "1" ? ".90" : ".95") + ", 30)) < 1e-10, " +
                "any(grepl('Critical q', printed, fixed = TRUE)) == " + (sided == "1" ? "FALSE" : "TRUE") + ")\n");
        }

        foreach (double[] counts in new[] { new double[] { 41, 64, 216, 180 }, new double[] { 10, 20, 40, 80 } })
        {
            var cohort = Run("Chi2by2", new() { ["scrap"] = counts, ["cco"] = .90, ["study_type"] = "cohort", ["doFisher"] = true });
            ParameterBag native = Meta539Rows(cohort.output.ParameterBag, "*relrisk").Single();
            string power = Regex.Match(native["pwr"].AsString, @"[=\s]([\d.]+)%\s*$").Groups[1].Value;
            Replay("Chi2by2-cohort-" + counts[0].ToString(inv), ActualScript(cohort.run),
                Printed("90 percent confidence interval:") +
                "stopifnot(!any(grepl('95 percent confidence interval:', printed, fixed = TRUE)))\n" +
                Meta539Compare("cohort risk difference and limits", "risk_difference, risk_limits",
                    new[] { native["dif"].AsDouble, native["miettinen_from"].AsDouble, native["miettinen_to"].AsDouble }) +
                "stopifnot(abs(round(100 * power, 2) - " + power + ") < 1e-8)\n" +
                Printed("Approximate power (for 10% significance)") + Printed("Approximate (Miettinen) 90% confidence interval"));
        }

        List<string> failures = Meta539Batch(folder, cases);
        Check(failures.Count == 0, "round-two level and replay cases: " + string.Join("; ", failures));
    }
}
