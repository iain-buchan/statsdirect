using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;

internal static partial class Program
{
    private static void CheckPairedMissingReplay(string folder)
    {
        double m = Constant.MISSING;
        // Every layout has the same five complete pairs. Unmatched observations
        // must never be paired by separately dropping the blanks in each column.
        var samples = new[] {
            ("complete", new[] { 10.0, 15, 21, 24, 30 }, new[] { 9.0, 12, 17, 20, 25 }),
            ("leading", new[] { m, 10, 15, 21, 24, 30 }, new[] { 99.0, 9, 12, 17, 20, 25 }),
            ("staggered", new[] { 10.0, m, 15, 77, 21, 24, m, 30, m, m }, new[] { 9.0, 55, 12, m, 17, 20, m, 25, 66, m }),
            ("trailing", new[] { 10.0, 15, 21, 24, 30, m, m }, new[] { 9.0, 12, 17, 20, 25, 77, m })
        };
        foreach (var (name, before, after) in samples)
        foreach (double confidence in new[] { 0.8, 0.95, 0.99 })
        foreach (bool agreement in new[] { false, true })
            Verify(name, before, after, confidence, agreement, false);
        // Older records lacking the option retain the recipe's previous behaviour.
        Verify("legacy", samples[2].Item2, samples[2].Item3, 0.95, true, true);

        void Verify(string name, double[] before, double[] after, double confidence, bool agreement, bool legacy)
        {
            string label = name + "-" + confidence.ToString("R", inv) + "-" + agreement;
            DataFrame data = new();
            data.Variables.Add(Column("Selected before", before));
            data.Variables.Add(Column("Selected after", after));
            var (run, _) = Run("TPaired", new() { ["data"] = data, ["gamma"] = confidence, ["doAgreement"] = agreement });
            JsonElement record = Record(run);
            JsonElement values = record.GetProperty("values");
            double[] expected = new[] { "n", "mean", "sd", "sem", "df", "t", "tail_2", "from", "to" }
                .Select(key => values.GetProperty(key).GetDouble()).ToArray();
            if (legacy)
            {
                JsonObject saved = JsonNode.Parse(record.GetRawText()).AsObject();
                saved["parameters"].AsObject().Remove("doAgreement");
                record = JsonDocument.Parse(saved.ToJsonString()).RootElement.Clone();
            }
            RScript script = RScriptWriter.Write(record, null, null);
            Check(script.HasRecipe && !script.DataUnavailable, "paired " + label + " exports complete data and settings");
            string target = Path.Combine(folder, "paired-missing-" + label);
            string svg = Path.Combine(target, "agreement.svg");
            string probe = "svg(" + RScriptWriter.Quote(svg) + ")\n" + "plot_count <- 0L\n" +
                "plot <- function(x,y,...) { plot_count <<- plot_count+1L; plotted <<- list(x=x,y=y,...); graphics::plot(x,y,...) }\n";
            string assertions = "\nexpected_before <- c(10,15,21,24,30)\nexpected_after <- c(9,12,17,20,25)\n" +
                "expected_d <- expected_before-expected_after\n" +
                "stopifnot(identical(before,expected_before),identical(after,expected_after),identical(d,expected_d),n==5L)\n" +
                "actual <- c(n,mean(d),sd(d),sd(d)/sqrt(n),unname(r$parameter),unname(r$statistic),r$p.value,r$conf.int[1:2])\n" +
                "stopifnot(isTRUE(all.equal(actual," + RScriptWriter.Vector(expected.Cast<object>().ToArray()) + ",tolerance=1e-10,check.attributes=FALSE)))\n" +
                "stopifnot(isTRUE(all.equal(pw$power,power.t.test(n=5,delta=mean(expected_d),sd=sd(expected_d),sig.level=1-confidence,type='paired',strict=TRUE)$power)))\n" +
                "stopifnot(plot_count==" + (agreement ? "1L" : "0L") + ")\n" +
                (agreement ? "stopifnot(identical(plotted$x,(expected_before+expected_after)/2),identical(plotted$y,expected_d),identical(plotted$xlab,'Mean ((Selected before + Selected after) / 2)'),identical(plotted$ylab,'Difference (Selected before - Selected after)'),isTRUE(all.equal(loa,mean(expected_d)+c(-1,1)*qnorm((1+confidence)/2)*sd(expected_d))))\n" : "stopifnot(!exists('loa'))\n") +
                "dev.off()\ncat('PAIRED_MISSING_REPLAY_OK\\n')\n";
            string output = RunR(probe + script.Text + assertions, target);
            Check(output.Contains("PAIRED_MISSING_REPLAY_OK") && !output.Contains("= NA")
                && output.Contains("Limits of agreement") == agreement,
                "paired " + label + " reproduces complete-pair native results, summaries, power and selected agreement output in R");
            if (agreement)
                Check(File.Exists(svg) && File.ReadAllText(svg).Contains("<svg"), "paired " + label + " draws the selected complete pairs as SVG");
        }
    }
}
