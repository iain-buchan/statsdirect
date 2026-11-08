using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckDeferredMatched538(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        CheckDeferredAgreement538(Path.Combine(folder, "agreement-538"));
        CheckDeferredSignedRanks538(Path.Combine(folder, "signed-ranks-538"));
        CheckDeferredSpearman538(Path.Combine(folder, "spearman-538"));
        CheckDeferredPca538(Path.Combine(folder, "pca-538"));
    }

    private static DataFrame DeferredFrame538(params (string title, double[] values)[] columns)
    {
        DataFrame frame = new();
        foreach (var column in columns) frame.Variables.Add(Column(column.title, column.values));
        return frame;
    }
    private static IEnumerable<ParameterBag> DeferredBags538(ParameterBag bag)
    {
        yield return bag;
        foreach (var pair in bag)
            if (pair.Value?.AsObject is IList<ParameterBag> children)
                foreach (var child in children) foreach (var item in DeferredBags538(child)) yield return item;
    }
    private static double DeferredNumber538(ParameterBag bag, string key) => Convert.ToDouble(DeferredBags538(bag).First(b => b.ContainsKey(key) && b[key]?.AsObject != null)[key].AsObject, inv);
    private static string DeferredNative538(ParameterBag bag, params string[] keys) => RScriptWriter.Vector(keys.Select(k => (object)DeferredNumber538(bag, k)).ToArray());
    private static string DeferredEqual538(string expression, string expected, double tolerance = 1e-9) =>
        "stopifnot(isTRUE(all.equal(" + expression + "," + expected + ",tolerance=" + tolerance.ToString("R", inv) + ",check.attributes=FALSE)))\n";

    // An accidental qsignrank(1100) must fail the test promptly, not stall CI.
    // Kill only the R process this invocation started, with its own temporary files.
    private static string DeferredR538(string code, string folder, int timeoutSeconds = 60)
    {
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "script.R");
        File.WriteAllText(file, code);
        ProcessStartInfo start = new(Rscript()) { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--vanilla"); start.ArgumentList.Add("--encoding=UTF-8"); start.ArgumentList.Add(file);
        foreach (string name in new[] { "LC_ALL", "LC_CTYPE" })
            if (start.Environment.TryGetValue(name, out string locale) && locale == "C.UTF-8") start.Environment.Remove(name);
        Stopwatch elapsed = Stopwatch.StartNew();
        using Process process = Process.Start(start);
        Task<string> output = process.StandardOutput.ReadToEndAsync(), errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutSeconds * 1000))
        {
            process.Kill(true); process.WaitForExit();
            throw new Exception("FAIL R exceeded " + timeoutSeconds + " seconds in " + folder);
        }
        elapsed.Stop();
        File.WriteAllText(Path.Combine(folder, "elapsed-ms.txt"), elapsed.ElapsedMilliseconds.ToString(inv));
        File.WriteAllText(Path.Combine(folder, "stdout.txt"), output.Result);
        File.WriteAllText(Path.Combine(folder, "stderr.txt"), errors.Result);
        if (process.ExitCode != 0) throw new Exception("FAIL R in " + folder + ":\n" + output.Result + "\n" + errors.Result);
        return output.Result;
    }
    private static string DeferredVerify538(OperationRun run, string assertion, string folder)
    {
        RScript script = RScriptWriter.Write(Record(run), null, null);
        Check(script.HasRecipe && !script.DataUnavailable, Path.GetFileName(folder) + " records actual data and settings");
        string output = DeferredR538(script.Text + "\n" + assertion + "\ncat('DEFERRED_MATCHED_OK\\n')\n", folder);
        Check(output.Contains("DEFERRED_MATCHED_OK"), Path.GetFileName(folder) + " reproduces native results within the timeout");
        return output;
    }

    private static void CheckDeferredAgreement538(string folder)
    {
        int[] Permutation(int n, int inversions)
        {
            List<int> available = Enumerable.Range(1, n).ToList(); int[] values = new int[n];
            for (int j = 0; j < n; j++) { int k = Math.Min(inversions, n - 1 - j); values[j] = available[k]; available.RemoveAt(k); inversions -= k; }
            return values;
        }
        // Independent integer counts, truncated at the requested discordance.
        // Logarithms preserve a tiny tail without converting n! to a double.
        double ExactLogP(int n, int discordance)
        {
            BigInteger[] counts = new BigInteger[discordance + 1]; counts[0] = 1; int most = 0;
            for (int j = 2; j <= n; j++)
            {
                int nextMost = Math.Min(discordance, most + j - 1); BigInteger[] next = new BigInteger[discordance + 1]; BigInteger window = 0;
                for (int d = 0; d <= nextMost; d++) { window += counts[d]; if (d >= j) window -= counts[d - j]; next[d] = window; }
                counts = next; most = nextMost;
            }
            return Math.Log(2) + BigInteger.Log(counts.Aggregate(BigInteger.Zero, (a, b) => a + b)) - Enumerable.Range(1, n).Sum(i => Math.Log(i));
        }
        foreach (int n in new[] { 8, 60, 170, 171, 200, 1000 })
        {
            int pairs = n * (n - 1) / 2;
            int inversions = n == 8 ? 8 : (int)(pairs * .47);
            var spread = Permutation(n, inversions);
            double[] mean = Enumerable.Range(0, n).Select(i => 10000d + 4 * i).ToArray();
            Test("untied-" + n, mean.Select((v, i) => v + spread[i]).ToArray(), mean.Select((v, i) => v - spread[i]).ToArray(), .9);
        }
        foreach (int n in new[] { 20, 171, 200 })
        {
            int inversions = n == 20 ? 0 : n * (n - 1) / 20;
            int[] spread = Permutation(n, inversions);
            double[] mean = Enumerable.Range(0, n).Select(i => 10000d + 4 * i).ToArray();
            Test("tiny-tail-" + n, mean.Select((v, i) => v + spread[i]).ToArray(), mean.Select((v, i) => v - spread[i]).ToArray(), .95, ExactLogP(n, inversions));
        }
        Check(ExactLogP(1000, 0) < Math.Log(double.Epsilon), "perfect n1000 exact Kendall probability is below double range");
        Test("perfect-1000", Enumerable.Range(1, 1000).Select(i => 10000d + 5 * i).ToArray(), Enumerable.Range(1, 1000).Select(i => 10000d + 3 * i).ToArray(), .95);
        foreach (double confidence in new[] { .8, .9, .95, .99 })
            Test("selected-alpha-" + confidence, new double[] { 494, 395, 516, 434, 476, 557, 413, 442 }, new double[] { 490, 397, 512, 430, 470, 611, 415, 431 }, confidence);
        Test("decimal-near-ties", new double[] { 1, 1.13, 2.26, 4.27, 3.05, 1.72, 2.90, 3.61, 1.48, 3.95 }, new double[] { .96, 1.09, 2.22, 4.23, 2.98, 1.62, 2.78, 3.31, 1.33, 3.74 }, .95);
        Test("exact-tie", new double[] { 101, 105, 112, 114, 123, 125, 133, 135 }, new double[] { 99, 103, 108, 110, 117, 119, 127, 129 }, .95);
        foreach(bool meanChain in new[]{false,true})
        for(int permutation=0;permutation<8;permutation++)
        {
            double[] mean=meanChain?new[]{100d,100+7.5e-11,100+15e-11,400,500,600,700,800}:Enumerable.Range(1,8).Select(i=>100d*i).ToArray();
            double[] sd=meanChain?Enumerable.Range(1,8).Select(i=>(double)i).ToArray():new[]{1d,1+2.5e-12,1+5e-12,2,3,4,5,6};
            Random random=new(991+permutation);int[] order=Enumerable.Range(0,8).OrderBy(_=>random.Next()).ToArray();
            Test("anchor-tie-chain-"+meanChain+"-"+permutation,
                order.Select(i=>mean[i]+sd[i]/Math.Sqrt(2)).ToArray(),order.Select(i=>mean[i]-sd[i]/Math.Sqrt(2)).ToArray(),.95);
        }

        void Test(string label, double[] first, double[] second, double confidence, double? logP = null)
        {
            var (run, output) = Run("Agreement", new() { ["data"] = DeferredFrame538(("First rater", first), ("Second rater", second)), ["ci"] = confidence });
            string assertion = DeferredEqual538("kt$estimate", DeferredNative538(output.ParameterBag, "tau"));
            assertion += DeferredEqual538("sqrt(2)*qnorm((1+confidence)/2)*sw", DeferredNative538(output.ParameterBag, "rep"));
            assertion += DeferredEqual538("kt$p.value", DeferredNative538(output.ParameterBag, "p2"));
            if (DeferredNumber538(output.ParameterBag, "p2") == 0) assertion += "stopifnot(kt$p.value==0)\n";
            if (logP != null)
            {
                Check(Math.Abs(Math.Log(DeferredNumber538(output.ParameterBag, "p2")) - logP.Value) < 1e-9, label + " native exact tail agrees with independent BigInteger counts");
                assertion += "stopifnot(is.finite(log(kt$p.value)),abs(log(kt$p.value)-" + logP.Value.ToString("R", inv) + ")<1e-8)\n";
            }
            string text = DeferredVerify538(run, assertion, Path.Combine(folder, label));
            Match alpha = Regex.Match(text, @"Repeatability \(for alpha = ([0-9.eE+-]+)\)");
            Check(alpha.Success && Math.Abs(double.Parse(alpha.Groups[1].Value, inv) - (1 - confidence)) < 1e-12, label + " repeatability labels the selected alpha");
            string addendum = Convert.ToString(output.ParameterBag["tau_b_addendum"].AsObject, inv);
            Check(text.Contains("Kendall's tau" + (addendum.Contains('b') ? " b =" : " =")), label + " labels tied or untied Kendall consistently with native");
        }
    }

    private static void CheckDeferredSignedRanks538(string folder)
    {
        foreach (int n in new[] { 199, 200, 201, 250, 400, 1000, 1042, 1050, 1100 })
        foreach (double confidence in n >= 250 && n != 1100 ? new[] { .9, .95, .99 } : new[] { .95 })
        foreach (bool paired in new[] { false, true })
        foreach (bool mostlyZero in n == 1100 ? new[] { false, true } : new[] { false })
        {
            double[] difference = Enumerable.Range(0, n).Select(i => (double)((i * 37 % 101) - 40)).ToArray();
            if (n <= 201) difference = Enumerable.Range(1, n).Select(i => (i % 3 == 0 ? -1d : 1d) * i).ToArray();
            if (mostlyZero) difference = Enumerable.Range(0, n).Select(i => i < 180 ? (i % 3 == 0 ? -1d : 1d) * (i % 23 + 1) : 0d).ToArray();
            double[] before = difference.Select((v, i) => 10000d + i + v).ToArray(), after = Enumerable.Range(0, n).Select(i => 10000d + i).ToArray();
            DataFrame frame = paired ? DeferredFrame538(("Before", before), ("After", after)) : DeferredFrame538(("Difference", difference));
            var (run, output) = Run("Wilcoxon", new() { ["data"] = frame, ["gamma"] = confidence });
            ParameterBag bag = output.ParameterBag;
            string label = n + "-" + confidence + "-" + paired + (mostlyZero ? "-mostly-zero" : "");
            int nonzero = difference.Count(d => d != 0);
            string assertion = DeferredEqual538("c(length(nz),median(a),a[K],rev(a)[K])", DeferredNative538(bag, "non_0", "med_diff", "from", "to"));
            assertion += DeferredEqual538("c(lower,upper,min(1,2*min(lower,upper)))", DeferredNative538(bag, "p_l", "p_u", "p_2"));
            string expectedK = Convert.ToString(DeferredBags538(bag).First(b => b.ContainsKey("k"))["k"].AsObject, inv);
            int k = int.Parse(Regex.Match(expectedK, @"K = ([0-9]+)").Groups[1].Value, inv);
            assertion += "stopifnot(K==" + k + ",n==" + n + ")\n";
            string text = DeferredVerify538(run, assertion, Path.Combine(folder, label));
            Check(text.Contains(expectedK), label + " labels exact or approximate K as native");
            string expectedLevel = Convert.ToString(DeferredBags538(bag).First(b => b.ContainsKey("pc"))["pc"].AsObject, inv);
            Match level = Regex.Match(expectedLevel, @"([0-9.]+)% confidence interval");
            Match actual = Regex.Match(text, @"(Approximate )?([0-9.]+)% confidence interval for difference");
            Check(actual.Success && level.Success && Math.Abs(double.Parse(actual.Groups[2].Value, inv) - double.Parse(level.Groups[1].Value, inv)) < 1e-12 && actual.Groups[1].Success == (n > 1000), label + " prints native achieved or approximate coverage");
            Check(text.Contains(nonzero > 200 ? "Normalised statistic" : "Exact probability"), label + " selects exact or normal P from non-zero differences");
        }
    }

    private static void CheckDeferredSpearman538(string folder)
    {
        foreach (int n in new[] { 8, 9, 10, 11, 12, 13, 40, 200, 1290, 1300, 2000 })
        foreach (bool ties in new[] { false, true })
        {
            double[] x = Enumerable.Range(0, n).Select(i => ties ? (double)(i / 2) : i + .25).ToArray();
            double[] y = Enumerable.Range(0, n).Select(i => (double)((i * 7 + i / 3) % (n + 1))).ToArray();
            if (!ties)
            {
                Random random = new(37 + n); y = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
                for (int i = n - 1; i > 0; i--) { int j = random.Next(i + 1); (y[i], y[j]) = (y[j], y[i]); }
            }
            var (run, output) = Run("Spearman", new() { ["data"] = DeferredFrame538(("Predictor", x), ("Response", y)), ["gamma"] = .9 });
            string assertion = "stopifnot(n==" + n + ")\n";
            assertion += DeferredEqual538("rho", DeferredNative538(output.ParameterBag, "rho"));
            assertion += DeferredEqual538("score", DeferredNative538(output.ParameterBag, "ix"));
            assertion += DeferredEqual538("ci", DeferredNative538(output.ParameterBag, "from", "to"));
            // The large untied branch retains native AS89 instead of R's
            // historical 1290-pair cutoff. Tied data continue to use base R's t.
            assertion += DeferredEqual538("c(upper,lower,min(1,2*min(upper,lower)))", DeferredNative538(output.ParameterBag, "p_u", "p_l", "p_2"), 2e-7);
            DeferredVerify538(run, assertion, Path.Combine(folder, n + "-" + ties));
        }
    }

    private static void CheckDeferredPca538(string folder)
    {
        double[][] values = {
            new double[] { 1.2,.4,-.3,2.1,.8,-1,.2,1.5,-.6,.9,.1,-.2 },
            new double[] { 1,.7,-.1,1.8,.6,-1.3,.5,1.2,-.4,1.1,-.2,0 },
            new double[] { -1.5,-.2,.6,-2.4,-.6,.8,0,-1.9,.9,-.6,-.3,.5 },
            new double[] { .9,.5,.1,1.7,1,-1.1,.4,1.4,-.3,1.2,0,.1 } };
        string[] titles = { "First item", "Second/item", "Reverse question (3)", "Question-4" };
        double[][] scaled = {
            new double[] { 1,4,2,4,4,3,4,2,5,1,3,3 }, new double[] { 2,3,1,5,5,2,4,3,4,2,2,3 },
            new double[] { 1,3,3,5,3,2,5,3,5,0,2,4 }, new double[] { 34,36,-8,-13,-22,-13,-18,-36,41,11,2,-14 } };
        foreach (bool distinctScales in new[] { false, true })
        foreach (bool covariance in new[] { false, true })
        foreach (bool reverse in new[] { false, true })
        foreach (double confidence in new[] { .8, .9, .95, .99 })
        {
            var (parent, fitted) = Run(covariance ? "PCRCovariance" : "PCRCorrelation", new()
            {
                ["data"] = DeferredFrame538((distinctScales ? scaled : values).Select((v, i) => (titles[i], v)).ToArray()), ["correctForReversal"] = reverse, ["ci"] = confidence
            });
            var (run, output) = Run("Cronbach", new() { ["ci"] = confidence }, fitted.ParameterBag);
            string label = distinctScales + "-" + covariance + "-" + reverse + "-" + confidence;
            string text = DeferredVerify538(run, "stopifnot(identical(names(q)," + RScriptWriter.Vector(titles.Cast<object>().ToArray()) + "))\n", Path.Combine(folder, label));
            foreach (var row in new[] { (kind: "raw", alpha: "raw_t", lower: "raw_cl"), (kind: "standardized", alpha: "standard_t", lower: "standard_cl") })
            {
                Match line = Regex.Match(text, "With " + row.kind + @" variables: Scale reliability alpha = ([-0-9.]+) \(([0-9.]+)% lower confidence limit = ([-0-9.]+)");
                Check(line.Success && Math.Abs(double.Parse(line.Groups[1].Value, inv) - DeferredNumber538(output.ParameterBag, row.alpha)) < 5.1e-7 && Math.Abs(double.Parse(line.Groups[2].Value, inv) - confidence * 100) < 1e-12 && Math.Abs(double.Parse(line.Groups[3].Value, inv) - DeferredNumber538(output.ParameterBag, row.lower)) < 5.1e-7, label + " " + row.kind + " reliability and selected Feldt limit match native");
            }
            if (reverse)
            {
                string expectedLabel = Convert.ToString(DeferredBags538(fitted.ParameterBag).First(b => b.ContainsKey("warn"))["warn"].AsObject, inv);
                Check(text.Contains(expectedLabel), label + " reports the original item labels after native PCA reversal");
            }
            else Check(DeferredNumber538(output.ParameterBag, "raw_cl") == 0, label + " exercises the native zero-clamped lower limit");
        }
        // Correlation PCA itself requests no confidence level. Its own R recipe
        // retains the historical 95% reliability default when the setting is NULL.
        var (plain, _) = Run("PCRCorrelation", new() { ["data"] = DeferredFrame538(values.Select((v, i) => (titles[i], v)).ToArray()), ["correctForReversal"] = false });
        string defaultText = DeferredVerify538(plain, "stopifnot(confidence==.95)\n", Path.Combine(folder, "no-recorded-ci"));
        Check(defaultText.Contains("95% lower confidence limit"), "PCA without recorded confidence retains the default label");
        foreach(bool covariance in new[]{false,true})
        {
            // Two items are valid PCA input. Dropping either leaves only one,
            // so that reliability is unavailable rather than a dimension error.
            var(_,fitted)=Run(covariance?"PCRCovariance":"PCRCorrelation",new()
            {
                ["data"]=DeferredFrame538(("First item",Enumerable.Range(1,12).Select(i=>(double)i).ToArray()),
                    ("Second item",new[]{1d,3,2,5,4,7,6,9,8,11,10,12})),["correctForReversal"]=false,["ci"]=.9
            });
            var(run,output)=Run("Cronbach",new(){["ci"]=.9},fitted.ParameterBag);
            string assertion=DeferredEqual538("c(alpha(q),alpha(as.data.frame(scale(q))))",DeferredNative538(output.ParameterBag,"raw_t","standard_t"))+
                "stopifnot(ncol(q)==2L,is.na(alpha(q[,-1,drop=FALSE])),is.na(alpha(q[,-2,drop=FALSE])))\n";
            string text=DeferredVerify538(run,assertion,Path.Combine(folder,"two-items-"+covariance));
            Check(Regex.Matches(text,@"Question [12] dropped: \*  change \*").Count==4,"two-item PCA preserves full alpha and marks dropped-item reliability unavailable "+covariance);
            foreach(var row in new[]{(kind:"raw",lower:"raw_cl"),(kind:"standardized",lower:"standard_cl")})
            {
                Match line=Regex.Match(text,"With "+row.kind+@" variables: Scale reliability alpha = [-0-9.]+ \(90% lower confidence limit = ([-0-9.]+)");
                Check(line.Success&&Math.Abs(double.Parse(line.Groups[1].Value,inv)-DeferredNumber538(output.ParameterBag,row.lower))<5.1e-7,"two-item PCA selected Feldt limit matches native "+covariance+"-"+row.kind);
            }
        }
    }
}
