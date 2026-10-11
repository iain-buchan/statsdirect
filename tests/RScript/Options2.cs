using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // The second batch after the reviewer's findings on 5.1.0 (11 Oct 2026): the options the audit of every recipe found ignored, and
    // the sizes and data of the help's examples still in some.  As in CheckOptions, a run goes through the engine with the option set, the
    // writer writes its script, R runs it, and the figure R prints is compared with the engine's.  Where the engine cannot be driven
    // here (a parameter the host cannot answer, or an operation that itself runs R) the record is built and the script's run is checked.
    private static void CheckOptions2()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "options2");
        Directory.CreateDirectory(folder);
        int column = 1;
        DataFrame Sheet(params (string title, double[] values)[] columns) { DataFrame f = SheetFrame(Workbook, "Options2", column, 1, columns); column += columns.Length; return f; }
        DataFrame Classifier(params (string title, double[] codes)[] columns)
        {
            DataFrame frame = new();
            foreach ((string title, double[] codes) in columns)
            {
                double[] levels = codes.Distinct().OrderBy(v => v).ToArray();
                ClassifierVariable classifier = new() { Title = title };
                classifier.EnsureLength(codes.Length);
                for (int i = 0; i < codes.Length; i++) classifier.SetData(i, Array.IndexOf(levels, codes[i]));
                foreach (double lv in levels) classifier.Groups.Add(new StatsDirect.Data.Group(lv.ToString(inv), Array.IndexOf(levels, lv)) { NBin = codes.Count(v => v == lv) });
                frame.Variables.Add(classifier);
            }
            return frame;
        }
        string Script(OperationRun run) { RScript s = RScriptWriter.Write(Record(run), null, null); if (!s.HasRecipe) throw new Exception("FAIL the run's script has no recipe"); return s.Text; }
        string Built(string example) => RScriptWriter.Write(JsonDocument.Parse(RecordOf(JsonDocument.Parse(example).RootElement)).RootElement, null, null).Text;
        string Rr(string s, string f) => RunR(s, f).Replace("\r", "");   // R on Windows ends its lines with a carriage return too
        double Num(object o) => o is double d ? d : o is int i ? i : double.Parse(Regex.Match(o.ToString(), "^-?[0-9.]+(?:[eE][-+]?[0-9]+)?").Value, inv);   // a report value, or its leading figure when it is text ("9.37_benefit")
        // every value of a key anywhere in the report's bag, depth first, so that a block's structure need not be known
        IEnumerable<ParameterBag> Bags(ParameterBag bag)
        {
            yield return bag;
            foreach (KeyValuePair<string, FilledParameter> pair in bag)
                if (pair.Value?.AsObject is IList<ParameterBag> list)
                    foreach (ParameterBag inner in list)
                        foreach (ParameterBag b in Bags(inner)) yield return b;
        }
        List<double> Found(ParameterBag bag, string key) => Bags(bag).Where(b => b.ContainsKey(key) && b[key]?.AsObject != null).Select(b => Num(b[key].AsObject)).ToList();
        double[] Numbers(string text, string pattern) => Regex.Matches(text, pattern, RegexOptions.Multiline).Select(m => double.Parse(m.Groups[1].Value, inv)).ToArray();
        bool Near(double a, double b, double relative = 1e-5) => Math.Abs(a - b) <= relative * Math.Max(Math.Abs(a), Math.Abs(b)) + 6e-7;
        string Shown(IEnumerable<double> v) => string.Join(" ", v.Select(x => x.ToString("G7", inv)));

        // kappa with quadratic weights, then with a table of weights given (not symmetric, so that the table's orientation tells)
        double[] rater1 = { 1, 1, 2, 2, 3, 3, 1, 2, 3, 1, 2, 3, 2, 1, 3, 3 }, rater2 = { 1, 2, 2, 3, 3, 3, 1, 1, 2, 1, 2, 3, 3, 1, 2, 3 };
        var (run, output) = Run("Kappa", new() { ["ci"] = 0.95, ["method"] = "2", ["responses"] = Classifier(("first", rater1), ("second", rater2)) });
        string script = Script(run);
        string r = Rr(script, Path.Combine(folder, "kappa-quadratic"));
        double[] rKappa = Numbers(r, "^Kappa = (-?[0-9.]+)");
        List<double> engineKw = Found(output.ParameterBag, "kappaw");
        Console.WriteLine($"      quadratic kappa: engine {Shown(engineKw)}, R unweighted and weighted {Shown(rKappa)}");
        Check(script.Contains("method <- \"2\"") && rKappa.Length == 2 && engineKw.Count == 1 && Near(rKappa[1], engineKw[0], 1e-4), "R's quadratically weighted kappa gives the engine's");
        double[] w1 = { 1, 0.6, 0 }, w2 = { 0.3, 1, 0.6 }, w3 = { 0, 0.3, 1 };   // the rows of the table: the first rater's grades
        (run, output) = Run("Kappa", new() { ["ci"] = 0.95, ["method"] = "3", ["responses"] = Classifier(("first", rater1), ("second", rater2)), ["weights"] = Sheet(("w1", w1), ("w2", w2), ("w3", w3)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "kappa-table"));
        rKappa = Numbers(r, "^Kappa = (-?[0-9.]+)"); engineKw = Found(output.ParameterBag, "kappaw");
        Console.WriteLine($"      kappa with a table of weights: engine {Shown(engineKw)}, R {Shown(rKappa)}");
        Check(script.Contains("weights <- list(") && rKappa.Length == 2 && Near(rKappa[1], engineKw[0], 1e-4), "R's kappa with the run's table of weights gives the engine's, with the table as the run gave it");

        // the Gini coefficient with frequency weights
        (run, output) = Run("Gini", new() { ["gamma"] = 0.95, ["boots"] = 200, ["seed"] = 7, ["data"] = Sheet(("income", new double[] { 1, 2, 3, 4, 10 })), ["weights"] = Sheet(("freq", new double[] { 3, 1, 2, 1, 1 })) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "gini"));
        double rGini = Numbers(r, "^Gini coefficient = (-?[0-9.]+)")[0], engineGini = Found(output.ParameterBag, "gini")[0];
        Console.WriteLine($"      Gini with frequency weights: engine {engineGini:G7}, R {rGini:G7}");
        Check(script.Contains("rep(imr, floor(weights))") && !script.Contains("births <-") && Near(rGini, engineGini), "R's Gini coefficient of the values repeated by their frequency weights gives the engine's, without the help's own births and deaths");

        // the effect size meta-analysis as a weighted mean difference, then from effect sizes given
        double[] en = { 20, 25, 30 }, em = { 5.1, 4.8, 5.5 }, es = { 1.2, 1.5, 1.1 }, cn = { 22, 24, 28 }, cm = { 4.2, 4.1, 4.9 }, cs = { 1.3, 1.4, 1.0 };
        (run, output) = Run("Effect", new() { ["gamma"] = 0.95, ["type"] = "m", ["strata"] = null, ["en"] = Sheet(("en", en)), ["em"] = Sheet(("em", em)), ["es"] = Sheet(("es", es)), ["cn"] = Sheet(("cn", cn)), ["cm"] = Sheet(("cm", cm)), ["cs"] = Sheet(("cs", cs)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "effect-m"));
        double rPooled = Numbers(r, "^Pooled effect size d\\+ = (-?[0-9.]+)")[0], enginePooled = Found(output.ParameterBag, "dplus")[0];
        Console.WriteLine($"      weighted mean difference: engine d+ {enginePooled:G7}, R {rPooled:G7}");
        Check(script.Contains("type <- \"m\"") && Near(rPooled, enginePooled, 1e-4), "R's pooled weighted mean difference gives the engine's");
        double[] gGiven = { 0.5, 0.3, 0.8 };
        (run, output) = Run("Effect", new() { ["gamma"] = 0.95, ["type"] = "g", ["strata"] = null, ["en"] = Sheet(("en", en)), ["cn"] = Sheet(("cn", cn)), ["g"] = Sheet(("g", gGiven)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "effect-g"));
        rPooled = Numbers(r, "^Pooled effect size d\\+ = (-?[0-9.]+)")[0]; enginePooled = Found(output.ParameterBag, "dplus")[0];
        Console.WriteLine($"      effect sizes given: engine d+ {enginePooled:G7}, R {rPooled:G7}");
        Check(script.Contains("gin <- c(0.5, 0.3, 0.8)") && script.Contains("em <- NULL") && Near(rPooled, enginePooled, 1e-4), "R's pooled effect size from the sizes given gives the engine's");

        // the single sample z test with the population standard deviation known
        double[] speed = { 9.1, 10.4, 11.2, 8.7, 10.9, 9.8, 12.1, 10.2 };
        (run, output) = Run("ZSingle", new() { ["gamma"] = 0.95, ["popmean"] = 10.0, ["popsd"] = 2.5, ["data"] = Sheet(("speed", speed)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "zsingle"));
        double rZ = Numbers(r, "^Standard normal deviate \\(z\\) = (-?[0-9.]+)")[0], engineZ = Found(output.ParameterBag, "z")[0];
        Console.WriteLine($"      z with the population sd: engine {engineZ:G7}, R {rZ:G7}");
        Check(script.Contains("popsd <- 2.5") && !script.Contains("speed[1:50]") && Near(rZ, engineZ, 1e-4), "R's z with the population standard deviation gives the engine's, without the help's own first and last 50");

        // principal components of five items without the reversal correction, then of the covariance matrix
        double[][] items = { new double[] { 3, 4, 5, 2, 4, 3, 5, 1, 4, 2 }, new double[] { 2, 4, 4, 1, 5, 3, 4, 2, 3, 2 }, new double[] { 4, 3, 2, 5, 2, 3, 1, 5, 2, 4 }, new double[] { 1, 2, 3, 2, 4, 3, 5, 2, 4, 1 }, new double[] { 5, 3, 4, 2, 4, 4, 5, 1, 3, 3 } };
        (run, output) = Run("PCRCorrelation", new() { ["correctForReversal"] = false, ["data"] = Sheet(("i1", items[0]), ("i2", items[1]), ("i3", items[2]), ("i4", items[3]), ("i5", items[4])) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "pca"));
        double[] rEigen = Numbers(r, "^[1-5] (-?[0-9.]+) ");
        List<double> engineEigen = Found(output.ParameterBag, "eigen");
        Console.WriteLine($"      principal components: engine eigenvalues {Shown(engineEigen)}, R {Shown(rEigen)}");
        Check(script.Contains("correct <- FALSE") && !r.Contains("Sign was reversed") && rEigen.Length == 5 && engineEigen.Count == 5 && rEigen.Zip(engineEigen).All(p => Near(p.First, p.Second, 1e-4)), "R's principal components of five items without the reversal correction give the engine's eigenvalues");
        (run, output) = Run("PCRCovariance", new() { ["ci"] = 0.95, ["correctForReversal"] = true, ["data"] = Sheet(("i1", items[0]), ("i2", items[1]), ("i3", items[2]), ("i4", items[3]), ("i5", items[4])) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "pca-covariance"));
        rEigen = Numbers(r, "^[1-5] (-?[0-9.]+) "); engineEigen = Found(output.ParameterBag, "eigen");
        Console.WriteLine($"      principal components of the covariance matrix: engine eigenvalues {Shown(engineEigen)}, R {Shown(rEigen)}");
        Check(script.Contains("scale. = FALSE") && rEigen.Length == 5 && engineEigen.Count == 5 && rEigen.Zip(engineEigen).All(p => Near(p.First, p.Second, 1e-4)), "R's principal components of the covariance matrix give the engine's eigenvalues");

        // the logit model with the doses as logarithms and another centile
        (run, output) = Run("Logit", new() { ["conf"] = 0.95, ["calc-log10-doses"] = true, ["nc"] = 0, ["nrc"] = 0, ["qld"] = 80.0, ["dose"] = Sheet(("dose", new double[] { 1, 2, 4, 8, 16 })), ["subjects"] = Sheet(("subjects", new double[] { 20, 20, 20, 20, 20 })), ["responders"] = Sheet(("responders", new double[] { 2, 5, 10, 15, 19 })) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "logit"));
        double rMedian = Numbers(r, "^Median \\* Dose = (-?[0-9.]+)")[0], rCentile = Numbers(r, "^\\* Dose for centile 80 = (-?[0-9.]+)")[0];
        double engineMedian = Found(output.ParameterBag, "mxd")[0], engineCentile = Found(output.ParameterBag, "doseq")[0];   // the report gives the doses back as doses, not their logarithms
        Console.WriteLine($"      logit with log doses: engine median {engineMedian:G7} and centile 80 {engineCentile:G7}, R {rMedian:G7} and {rCentile:G7}");
        Check(script.Contains("log_doses <- TRUE") && script.Contains("centile <- 80") && Near(engineMedian, rMedian, 1e-4) && Near(engineCentile, rCentile, 1e-4), "R's logit on the log doses gives the engine's median dose and its dose for the centile chosen, as doses");

        // the meta-analysis of relative risks given with their standard errors
        (run, output) = Run("MetaSummary", new() { ["gamma"] = 0.95, ["type"] = "relativeRisk", ["use_ci"] = "false", ["studies"] = null, ["y"] = Sheet(("rr", new double[] { 1.2, 0.8, 1.5 })), ["se_y"] = Sheet(("se", new double[] { 0.1, 0.2, 0.15 })) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "meta-summary"));
        double rMeta = Numbers(r, "^Pooled odds ratio = (-?[0-9.]+)")[0], engineMeta = Found(output.ParameterBag, "rmh")[0];
        Console.WriteLine($"      relative risks with standard errors: engine pooled {engineMeta:G7}, R {rMeta:G7}");
        Check(script.Contains("se_in <- c(0.1, 0.2, 0.15)") && script.Contains("lower <- NULL") && Near(rMeta, engineMeta, 1e-4), "R's pooled relative risk from the standard errors given gives the engine's");

        // the proportion meta-analysis with Miller's inverse and 1 added to each n
        (run, output) = Run("ProportionMeta", new() { ["gamma"] = 0.95, ["method"] = "doubleArcsine", ["strata"] = null, ["sn"] = Sheet(("n", new double[] { 50, 60, 40 })), ["sr"] = Sheet(("r", new double[] { 20, 30, 10 })) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "proportion-meta"));
        double rProp = Numbers(r, "^Pooled proportion = +(-?[0-9.]+)")[0], engineProp = Found(output.ParameterBag, "rmh")[0];
        Console.WriteLine($"      proportions by Miller's inverse: engine pooled {engineProp:G7}, R {rProp:G7}");
        // (the dialog's "Weights = n +" choice does not reach the engine, which adds 0.5 whatever is chosen)
        Check(script.Contains("method <- \"doubleArcsine\"") && Near(rProp, engineProp, 1e-4), "R's pooled proportion by Miller's inverse gives the engine's");

        // the 2 by 2 chi-square of a cohort study without the exact test
        (run, output) = Run("Chi2by2", new() { ["cco"] = 0.95, ["study_type"] = "cohort", ["doFisher"] = false, ["scrap"] = new double[] { 30, 10, 20, 40 } });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "chi2by2-cohort"));
        double rRr = Numbers(r, "^Relative risk = (-?[0-9.]+)")[0], engineRr = Found(output.ParameterBag, "ratio")[0];
        double rKoop = Numbers(r, "^Approximate \\(Koopman\\) 95% confidence interval = (-?[0-9.]+)")[0], engineKoop = Found(output.ParameterBag, "koopman_from")[0];
        Console.WriteLine($"      2 by 2 cohort: engine relative risk {engineRr:G7} from {engineKoop:G7}, R {rRr:G7} from {rKoop:G7}");
        Check(script.Contains("study <- \"cohort\"") && !r.Contains("Odds Ratio =") && !r.Contains("Fisher's Exact Test") && Near(rRr, engineRr, 1e-4) && Near(rKoop, engineKoop, 1e-4), "R's 2 by 2 of a cohort study gives the engine's relative risk and Koopman limit, with no odds ratio and no exact test");

        // the crossover trial with baselines
        double[] d1 = { 12, 15, 11, 14, 13, 16 }, p1 = { 10, 12, 9, 11, 12, 13 }, b1 = { 9, 11, 8, 10, 11, 12 }, d2 = { 14, 13, 15, 12, 16, 14 }, p2 = { 11, 10, 12, 10, 13, 11 }, b2 = { 10, 9, 11, 9, 12, 10 };
        (run, output) = Run("Crossover", new() { ["gamma"] = 0.95, ["group1drug"] = Sheet(("d1", d1)), ["group1placebo"] = Sheet(("p1", p1)), ["group1baseline"] = Sheet(("b1", b1)), ["group2drug"] = Sheet(("d2", d2)), ["group2placebo"] = Sheet(("p2", p2)), ["group2baseline"] = Sheet(("b2", b2)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "crossover"));
        double rCross = Numbers(r, "^diff 1 - diff 2 = (-?[0-9.]+)")[0], engineCross = Found(output.ParameterBag, "treatment_diff")[0];
        Console.WriteLine($"      crossover with baselines: engine treatment difference {engineCross:G7}, R {rCross:G7}");
        Check(script.Contains("baseline1 <- c(9, 11, 8") && Near(rCross, engineCross, 1e-4), "R's crossover analysis with the baselines taken from both periods gives the engine's treatment difference");

        // the time series summary with an observation of 0 at time 0 for each subject
        double[] times = { 1, 2, 4, 1, 2, 4, 1, 2, 4, 1, 2, 4, 1, 2, 4, 1, 2, 4 }, obs = { 5, 9, 4, 6, 10, 5, 4, 8, 3, 7, 12, 6, 5, 11, 2, 8, 9, 7 }, ids = { 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6, 6 };
        (run, output) = Run("TimeSeriesSummary", new() { ["ci"] = 0.95, ["addZeroObservationAtZeroTime"] = true, ["doExactP"] = false, ["iterations"] = 1000, ["seed"] = 1, ["groups"] = null, ["times"] = Sheet(("time", times)), ["observations"] = Sheet(("value", obs)), ["subjectIds"] = Classifier(("subject", ids)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "time-series"));
        double rAuc = Numbers(r.Substring(r.IndexOf("Subject ID", StringComparison.Ordinal)), "^1 (?:\\S+ ){5}(-?[0-9.]+) ?$")[0];   // subject 1's line after the header: id, baseline, min, max, time to max, slope, AUC (cat ends it with a space)
        ParameterBag subject1 = Bags(output.ParameterBag).First(b => b.ContainsKey("subjectId") && b["subjectId"].AsObject.ToString() == "1");
        double engineAuc = Num(subject1["auc"].AsObject);
        Console.WriteLine($"      time series with a zero at time 0: engine AUC of subject 1 {engineAuc:G7}, R {rAuc:G7}");
        Check(script.Contains("zero_at_start <- TRUE") && Near(rAuc, engineAuc, 1e-4), "R's area under the curve with the zero at time 0 gives the engine's");

        // the number needed to treat with an expected risk, and the risk analysis with a population proportion exposed and without one
        (run, output) = Run("MiscNumberNeededToTreat", new() { ["cco"] = 0.95, ["nc"] = 100.0, ["xc"] = 30.0, ["nt"] = 100.0, ["xt"] = 15.0, ["brr"] = 0.2 });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "nnt"));
        double rNnt = Numbers(r, "^NNT \\[odds ratio\\] = (-?[0-9.]+)")[0], engineNnt = Found(output.ParameterBag, "or_treat")[0];
        Console.WriteLine($"      number needed to treat at an expected risk of 20%: engine from the odds ratio {engineNnt:G7}, R {rNnt:G7}");
        Check(script.Contains("brr <- 0.2") && r.Contains("Expected risk of event without treatment = 20%") && Near(rNnt, engineNnt, 1e-4), "R's number needed to treat from the odds ratio at the expected risk given gives the engine's");
        (run, output) = Run("MiscRelRisk", new() { ["scrap"] = new double[] { 72, 20, 684, 553 }, ["pe"] = 0.4, ["cco"] = 0.95 });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "relrisk"));
        double rPar = Numbers(r, "^Population attributable risk % = (-?[0-9.]+)")[0], enginePar = Found(output.ParameterBag, "par")[0];
        Console.WriteLine($"      attributable risk with 40% exposed: engine {enginePar:G7}, R {rPar:G7}%");
        Check(script.Contains("pe <- 0.4") && (Near(rPar, enginePar, 1e-4) || Near(rPar, 100 * enginePar, 1e-4)), "R's population attributable risk with the proportion exposed given gives the engine's");
        (run, _) = Run("MiscRelRisk", new() { ["scrap"] = new double[] { 72, 20, 684, 553 }, ["pe"] = null, ["cco"] = 0.95 });
        Check(Script(run).Contains("pe <- NULL"), "a setting the run skipped is NULL in the script");

        // the reference range of more than 200 values with the conservative limits
        double[] big = Enumerable.Range(0, 250).Select(i => 50 + 8 * Math.Sin(i * 0.7) + 3 * Math.Cos(i * 1.9) + (i % 7)).ToArray();
        (run, output) = Run("ReferenceRange", new() { ["gamma"] = 0.95, ["reference-interval"] = 0.95, ["do_conservative"] = true, ["data"] = Sheet(("x", big)) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "reference-range"));
        double[] rCons = Numbers(r, "^Approximate 95% confidence interval \\(conservative\\) = (-?[0-9.]+) to");
        List<double> engineFromAny = Found(output.ParameterBag, "from_any");
        Console.WriteLine($"      reference range, conservative lower limits: engine {Shown(engineFromAny)}, R {Shown(rCons)}");
        Check(script.Contains("conservative <- TRUE") && rCons.Length == 2 && engineFromAny.Count >= 1 && Near(rCons[0], engineFromAny[0], 1e-6), "R's conservative confidence limit of the lower quantile of a reference range gives the engine's");

        // the abridged life table with utility weights: the adjusted expectation of life
        (run, output) = Run("AbridgedLifetable", new() { ["gamma"] = 0.95, ["save"] = false, ["iterations"] = "10000", ["fractions"] = null, ["intervals"] = Sheet(("n", new double[] { 1, 4, 5, 10 })), ["population"] = Sheet(("P", new double[] { 1000, 4000, 5000, 9000, 8000 })), ["deaths"] = Sheet(("D", new double[] { 50, 40, 100, 1500, 4000 })), /* a cohort that halves within the table, so that it has a median */ ["weights"] = Sheet(("u", new double[] { 1, 1, 0.9, 0.8, 0.7 })) });
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "lifetable"));
        double rAdjusted = Numbers(r, "^0 to 1 \\d+ (-?[0-9.]+) ?$")[^1], engineAdjusted = Found(output.ParameterBag, "eh")[0];
        Console.WriteLine($"      life table with utility weights: engine adjusted expectation at birth {engineAdjusted:G7}, R {rAdjusted:G7}");
        Check(script.Contains("u <- c(1, 1, 0.9, 0.8, 0.7)") && Near(rAdjusted, engineAdjusted, 1e-4), "R's adjusted expectation of life with the utility weights gives the engine's");

        // the universal agreement coefficient with observer 2 as the reference standard
        double[] measure = { 1.2, 1.4, 1.1, 1.3, 1.5, 1.3, 1.5, 1.0, 1.4, 1.6, 1.1, 1.3, 1.2, 1.2, 1.4 }, raters = { 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3 }, objects = { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 };
        (run, output) = Run("UniversalAgreement", new() { ["categories"] = null, ["reference"] = new bool[] { false, true, false }, ["data"] = Sheet(("measure", measure)), ["raters"] = Classifier(("rater", raters)), ["objects"] = Classifier(("object", objects)) });
        JsonElement record = Record(run);
        script = Script(run);
        r = Rr(script, Path.Combine(folder, "agreement"));
        double[] rR = Numbers(r, "^Agreement coefficient R: (-?[0-9.]+)");
        double engineR = Found(output.ParameterBag, "R")[0];
        Console.WriteLine($"      agreement with observer 2 as the reference: engine R {engineR:G7}, R {Shown(rR)}");
        Check(record.GetProperty("parameters").GetProperty("reference").GetRawText() == "[false,true,false]" && script.Contains("reference <- list(FALSE, TRUE, FALSE)") && r.Contains("Reference standard: Observer 2") && rR.Length == 2 && Near(rR[1], engineR, 1e-4),
              "the record keeps the observer marked as the reference, and R's agreement coefficient with that observer as the reference gives the engine's");

        // records built by hand, for the operations that run R themselves or whose prompts this host cannot answer
        string loess = "{\"operation\":\"LOESS\",\"file\":\"loess.R\",\"inputs\":[{\"name\":\"outcome\",\"columns\":[{\"title\":\"y\",\"values\":[3.1,4.2,5.9,7.1,8.8,9.9,12.2,13.1,15.3,16.8]}]}," +
            "{\"name\":\"predictors\",\"columns\":[{\"title\":\"x1\",\"values\":[1,2,3,4,5,6,7,8,9,10]},{\"title\":\"Age (years)\",\"values\":[30,35,41,28,52,47,60,33,45,58]}]}]," +
            "\"settings\":{\"span\":0.9,\"degree\":\"1\",\"model\":\"y~x1+age..years.\",\"title\":\"A fit\",\"xlab\":\"x1\",\"ylab\":\"y\"}}";
        script = Built(loess);
        r = Rr(script, Path.Combine(folder, "loess"));
        Check(script.Contains("model <- \"y~x1+age..years.\"") && r.Contains("Equivalent Number of Parameters") && Regex.IsMatch(r, "y ~ x1 \\+ age\\.\\.years\\."), "the LOESS script fits the run's formula over its two predictors, named as the program names them for R");
        string mcr = "{\"operation\":\"MethodComparisonRegression\",\"file\":\"method-comparison.R\",\"inputs\":[{\"name\":\"testData\",\"columns\":[{\"title\":\"test\",\"values\":[1.1,2.3,2.9,4.2,5.1,5.8,7.2,8.1,8.8,10.3,11.1,12.4]}]}," +
            "{\"name\":\"referenceData\",\"columns\":[{\"title\":\"reference\",\"values\":[1,2,3,4,5,6,7,8,9,10,11,12]}]}],\"settings\":{\"error-ratio\":1,\"ci\":0.95,\"methods\":[\"do.deming.analytical\"],\"seed\":3}}";
        script = Built(mcr);
        r = Rr(script, Path.Combine(folder, "mcr"));
        Check(script.Contains("methods <- list(\"do.deming.analytical\")") && Regex.IsMatch(r, "^Slope: ", RegexOptions.Multiline) && !r.Contains("jackknife"), "the method comparison script runs the regression the run chose, the Deming line with Wald limits, and not the help's jackknife");
        string linearized = "{\"operation\":\"LinearizedEstimates\",\"file\":\"linearized-estimates.R\",\"data\":[{\"title\":\"kwh\",\"values\":[2,4,9,16,26,38]},{\"title\":\"size\",\"values\":[1,2,3,4,5,6]}],\"settings\":{\"model\":\"1\"}}";
        r = Rr(Built(linearized), Path.Combine(folder, "linearized"));
        Check(r.Contains("The model fitted in StatsDirect: Geometric / power"), "the linearized estimates script names the model the run chose");
        string normal = "{\"operation\":\"NormalPlot\",\"file\":\"normal-plot.R\",\"data\":[{\"title\":\"pimax\",\"values\":[40,55,62,70,76,81,88,95,103,120]}],\"settings\":{\"ScoreMethod\":\"2\"}}";
        script = Built(normal);
        r = Rr(script, Path.Combine(folder, "normal-plot"));
        Check(script.Contains("method <- \"2\"") && script.Contains("\"2\" = blom") && !r.Contains("The four values of 75"), "the normal plot script draws the scores of the method chosen and keeps the help's remark on its own example to that example");
        string summary = "{\"operation\":\"UnivariateSummary\",\"file\":\"univariate-summary.R\",\"data\":[{\"title\":\"speed\",\"values\":[12,15,11,19,22,14,30,25,17,21,16,18]}],\"settings\":{\"gamma\":0.95,\"report-udca\":0.1,\"report-udcb\":0.9}}";
        r = Rr(Built(summary), Path.Combine(folder, "summary"));
        Check(r.Contains("Centile 90 ") && r.Contains("Centile 10 ") && !r.Contains("Centile 95"), "the summary's script gives the two centiles the run chose");
    }
}
