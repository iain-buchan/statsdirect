using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    // Fit through the real operation, then execute its recorded recipe in R. The
    // null fit, row count, criteria and diagnostics must describe the SAME rows.
    private static void CheckModelMissingReplay(string folder)
    {
        double m = Constant.MISSING;
        DataFrame Frame(params (string title, double[] values)[] columns)
        {
            DataFrame frame = new();
            foreach (var (title, values) in columns) frame.Variables.Add(Column(title, values));
            return frame;
        }
        IList<ParameterBag> Block(ParameterBag bag, string key) => (IList<ParameterBag>)bag["*" + key].AsObject;
        double Number(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        string Vec(IEnumerable<double> values) => RScriptWriter.Vector(values.Cast<object>().ToArray());
        string Compare(string expression, IEnumerable<double> expected, double tolerance = 1e-6) =>
            "stopifnot(isTRUE(all.equal(unname(c(" + expression + "))," + Vec(expected) + ",tolerance=" + tolerance.ToString("R", inv) + ",check.attributes=FALSE)))\n";
        // R's glm.fit retains the preceding IRLS QR for summary.glm's covariance.
        // The recipes request tighter convergence, so standard errors must now
        // agree to 1e-7 rather than hiding default glm's few-parts-per-million gap.
        string Script(StatsDirect.Templates.OperationRun run)
        {
            RScript script = RScriptWriter.Write(Record(run), null, null);
            Check(script.HasRecipe && !script.DataUnavailable, "model replay exports the acquired data and selected settings");
            return script.Text;
        }

        double[] x = { 72, 112, 111, 107, 119, 92, 126, 80, m, 84, 115, 118, 128, 128, 123, 116 };
        double[] y = { 68, 63, 66, m, 52, 75, 76, 118, 120, 114, 29, 42, 48, 50, 69, 59 };
        foreach (bool missing in new[] { false, true })
        foreach (double confidence in new[] { .8, .95, .99 })
        {
            int[] rows = Enumerable.Range(0, x.Length).Where(i => x[i] != m && y[i] != m).ToArray();
            double[] sx = missing ? x : rows.Select(i => x[i]).ToArray(), sy = missing ? y : rows.Select(i => y[i]).ToArray();
            var (run, output) = Run("SimpleLinearRegression", new() { ["x"] = Frame(("Selected predictor", sx)), ["y"] = Frame(("Selected response", sy)), ["gamma"] = confidence });
            ParameterBag bag = output.ParameterBag, se = Block(bag, "se")[0], ci = Block(se, "ci")[0];
            double intercept = Number(bag, "abs_yintercept") * (bag["lnk"].AsObject.ToString() == "-" ? -1 : 1);
            double power = double.Parse(Regex.Match(ci["pwr"].AsObject.ToString(), @"([0-9.]+)%$").Groups[1].Value, inv);
            string assertions = "\nstopifnot(n==14L,length(increase)==14L,length(weight)==14L,all(complete.cases(increase,weight)))\n" +
                Compare("coef(fit),se,confint(fit,level=confidence)[2,],ct$estimate,ct$estimate^2,ct$parameter,ct$statistic,ct$p.value", new[] { intercept, Number(bag, "slope"), Number(se, "slope_err"), Number(se, "se_from"), Number(se, "se_to"), Number(se, "se_r"), Number(se, "se_r2"), Number(ci, "df"), Number(ci, "tdf"), Number(ci, "p") }, 1e-10) +
                "stopifnot(abs(100*power-" + power.ToString("R", inv) + ")<=0.00500001)\ncat('MODEL_MISSING_OK\\n')\n";
            string result = RunR(Script(run) + assertions, Path.Combine(folder, "linear-" + missing + "-" + confidence.ToString("R", inv)));
            Check(result.Contains("MODEL_MISSING_OK") && result.Contains("Power (for " + (100 * (1 - confidence)).ToString("G6", inv) + "% significance)") && result.Contains("Equation: Selected response =") && result.Contains("Selected predictor +") && !result.Contains("Equation: % Increase"), "simple regression uses selected labels and jointly observed rows for coefficients, correlation, degrees of freedom and selected-level power");
        }
        // A user can copy the recipe itself and provide plain vectors, without
        // the optional titles that the application normally adds to its script.
        int[] completeRows = Enumerable.Range(0, x.Length).Where(i => x[i] != m && y[i] != m).ToArray();
        string minimal = "increase <- " + Vec(completeRows.Select(i => y[i])) + "\nweight <- " + Vec(completeRows.Select(i => x[i])) + "\nconfidence <- 0.95\n" +
            File.ReadAllText(Path.Combine(Metadata("Recipes"), "recipes", "simple-linear-regression.R")) +
            "\nstopifnot(increase_title=='increase',weight_title=='weight',n==14L,all(is.finite(coef(fit))))\ncat('UNTITLED_LINEAR_OK\\n')\n";
        string untitled = RunR(minimal, Path.Combine(folder, "linear-without-title-variables"));
        Check(untitled.Contains("UNTITLED_LINEAR_OK") && untitled.Contains("Equation: increase =") && untitled.Contains("weight +"), "direct simple-regression recipe accepts plain vectors without optional column-title variables");

        // One missing value in each model input, including exposure <= 0. The
        // complete counterpart is a separate native fit, not an imputed fixture.
        foreach (bool weighted in new[] { false, true })
        foreach (bool exposed in new[] { false, true })
        foreach (bool missing in new[] { false, true })
        {
            double[] response = { 2, 5, 9, 14, 20, 11, 6, 4, 8, 7, 12, 16 };
            double[] age = { 1, 2, 3, 4, 5, 4, 2, 1, 3, 2, 4, 5 }, veteran = { 0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 1 };
            double[] exposure = { 100, 120, 140, 160, 180, 150, 110, 90, 130, 120, 150, 170 }, weights = { 1, 2, 1, 2, 1, 1, 2, 1, 2, 1, 1, 2 };
            if (missing) { response[0] = m; age[3] = m; veteran[8] = m; if (exposed) { exposure[1] = 0; exposure[9] = -1; exposure[10] = m; } if (weighted) weights[6] = m; }
            int expectedN = Enumerable.Range(0, response.Length).Count(i => response[i] != m && age[i] != m && veteran[i] != m && (!exposed || exposure[i] > 0 && exposure[i] != m) && (!weighted || weights[i] != m));
            Dictionary<string, object> answers = new() { ["gamma"] = .95, ["grouping"] = "individual", ["accuracy"] = "0.000000001", ["intercept"] = true, ["weights"] = weighted, ["has-exposure"] = exposed ? "true" : "false", ["response"] = Frame(("Counts", response)), ["predictors"] = Frame(("Age", age), ("Veteran", veteran)) };
            if (exposed) answers["exposure"] = Frame(("Person time", exposure));
            if (weighted) answers["weight"] = Frame(("Prior weight", weights));
            var (run, output) = Run("PoissonRegression", answers);
            string script = Script(run);
            var (_, model) = Run("PoissonRegressionModel", new(), output.ParameterBag);
            ParameterBag bag = model.ParameterBag;
            double[] expected = new[] { "ll", "dev", "idf", "rank", "aka", "sch", "devx", "r2", "r2i", "x2_pearson", "sp" }.Select(key => Number(bag, key)).ToArray();
            IList<ParameterBag> coefficients = Block(bag, "unscaled");
            string assertions = "\nstopifnot(n==" + expectedN + "L,nobs(fit)==" + expectedN + "L,all(complete.cases(response,predictors,exposure,weight)))\n" +
                Compare("ll,dev,fit$df.residual,k,dev+2*k,dev+k*log(n),fit$null.deviance,1-dev/fit$null.deviance,1-ll/ll0,pearson,scale", expected) +
                Compare("coef(fit)", coefficients.Select(b => Number(b, "coef"))) + Compare("summary(fit)$coefficients[,2]", coefficients.Select(b => Number(b, "err")), 1e-7) +
                "cat('MODEL_MISSING_OK\\n')\n";
            Check(RunR(script + assertions, Path.Combine(folder, "poisson-" + weighted + "-" + exposed + "-" + missing)).Contains("MODEL_MISSING_OK"), "Poisson replay uses identical complete positive-exposure rows for the fitted and null models, criteria, Pearson scale and coefficients");
        }

        foreach (bool weighted in new[] { false, true })
        foreach (bool missing in new[] { false, true })
        {
            double[] total = { 60, 17, 15, 8, 187, 85, 51, 23 }, response = { 5, 2, 1, 0, 35, 13, 15, 8 };
            double[] smoking = { 0, 1, 0, 1, 0, 1, 0, 1 }, obesity = { 0, 0, 1, 1, 0, 0, 1, 1 }, weights = { 1, 2, 1, 2, 1, 2, 1, 2 };
            if (missing) total[2] = m;
            Dictionary<string, object> answers = new() { ["gamma"] = .95, ["grouping"] = "grouped", ["accuracy"] = "0.000000001", ["intercept"] = true, ["weights"] = weighted, ["total"] = Frame(("Total", total)), ["response"] = Frame(("Responders", response)), ["predictors"] = Frame(("Smoking", smoking), ("Obesity", obesity)) };
            if (weighted) answers["weight"] = Frame(("Prior weight", weights));
            VerifyLogistic("grouped-" + weighted + "-" + missing, answers, 4);
        }
        foreach (bool weighted in new[] { false, true })
        foreach (bool missing in new[] { false, true })
        {
            double[] total = Enumerable.Repeat(20.0, 12).ToArray(), response = { 2, 5, 7, 9, 15, 18, 1, 4, 6, 10, 14, 17 };
            double[] dose = { -2, -1, 0, 1, 2, 3, -2, -1, 0, 1, 2, 3 }, marker = { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 }, weights = { 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2 };
            if (missing) { total[0] = m; response[3] = m; dose[8] = m; if (weighted) weights[10] = m; }
            Dictionary<string, object> answers = new() { ["gamma"] = .95, ["grouping"] = "grouped", ["accuracy"] = "0.000000001", ["intercept"] = true, ["weights"] = weighted, ["total"] = Frame(("Total", total)), ["response"] = Frame(("Responders", response)), ["predictors"] = Frame(("Dose", dose), ("Marker", marker)) };
            if (weighted) answers["weight"] = Frame(("Prior weight", weights));
            VerifyLogistic("all-inputs-" + weighted + "-" + missing, answers, 6);
        }
        // Individual rows are merged as well, and missing response, predictor or
        // prior weight must be removed together before any merge or diagnostic.
        foreach (bool weighted in new[] { false, true })
        foreach (bool missing in new[] { false, true })
        {
            double[] predictor = Enumerable.Range(1, 5).SelectMany(v => Enumerable.Repeat((double)v, 6)).ToArray();
            double[] response = Enumerable.Range(1, 5).SelectMany(v => Enumerable.Range(0, 6).Select(i => i < v ? 1.0 : 0)).ToArray();
            double[] weights = Enumerable.Repeat(1.0, 30).ToArray();
            if (missing) { response[1] = m; predictor[8] = m; if (weighted) weights[13] = m; }
            Dictionary<string, object> answers = new() { ["gamma"] = .95, ["grouping"] = "individual", ["accuracy"] = "0.000000001", ["intercept"] = true, ["weights"] = weighted, ["response"] = Frame(("Outcome", response)), ["predictors"] = Frame(("Dose", predictor)) };
            if (weighted) answers["weight"] = Frame(("Prior weight", weights));
            VerifyLogistic("individual-" + weighted + "-" + missing, answers, 5);
        }

        // The former aggregation helper names are valid worksheet headings.
        // Identical covariates with equal weights merge; a different prior
        // weight keeps a separate pattern. Both properties affect model df/ICs.
        foreach (string[] titles in new[] { new[] { "First", "Second", "Third" }, new[] { "sd_r", "sd_n", "sd_w" }, new[] { "prior_weight", "sd_n", "sd_w" } })
        {
            double[] total = Enumerable.Repeat(20.0, 24).ToArray();
            double[] counts = { 2, 4, 6, 8, 9, 11, 14, 16 };
            double[] response = Enumerable.Range(0, 24).Select(i => counts[i % 8] + i / 8).ToArray();
            double[] x1 = Enumerable.Range(0, 24).Select(i => (double)(i % 2)).ToArray(), x2 = Enumerable.Range(0, 24).Select(i => (double)(i / 2 % 2)).ToArray(), x3 = Enumerable.Range(0, 24).Select(i => (double)(i / 4 % 2)).ToArray();
            double[] weights = Enumerable.Range(0, 24).Select(i => i / 8 == 1 ? 2.0 : 1).ToArray();
            VerifyLogistic("colliding-labels-" + titles[0], new() { ["gamma"] = .95, ["grouping"] = "grouped", ["accuracy"] = "0.000000001", ["intercept"] = true, ["weights"] = true, ["total"] = Frame(("Total", total)), ["response"] = Frame(("Responders", response)), ["weight"] = Frame(("Prior weight", weights)), ["predictors"] = Frame((titles[0], x1), (titles[1], x2), (titles[2], x3)) }, 16);
        }

        // Native HL excludes zero prior frequencies and bases its df on occupied
        // groups, without the former empty first group below frequency 10.
        double[] hlDose = Enumerable.Range(0, 10).Select(i => 1.5 + i).ToArray();
        double[] hlTotal = Enumerable.Repeat(20.0, 10).ToArray(), hlResponse = { 2, 3, 5, 6, 8, 10, 12, 13, 15, 17 };
        foreach (int[] zeros in new[] { Array.Empty<int>(), new[] { 9 }, new[] { 0 }, new[] { 5 }, new[] { 8, 9 } })
        {
            double[] weights = Enumerable.Range(0, 10).Select(i => zeros.Contains(i) ? 0.0 : 1).ToArray();
            VerifyLogisticHl("grouped-zero-" + (zeros.Length == 0 ? "none" : string.Join("-", zeros)), hlDose, hlResponse, hlTotal, weights);
        }
        VerifyLogisticHl("grouped-unweighted", hlDose, hlResponse, hlTotal, null);
        VerifyLogisticHl("grouped-decreasing-zero-first", hlDose, hlResponse.Reverse().ToArray(), hlTotal, new[] { 0d }.Concat(Enumerable.Repeat(1.0, 9)).ToArray());
        VerifyLogisticHl("grouped-unequal-zero-last", hlDose, hlResponse, hlTotal, new double[] { 1, 2, 1, 2, 1, 2, 1, 2, 1, 0 });

        double[] hlAge = Enumerable.Range(1, 40).Select(i => (double)i).ToArray();
        double[] hlBinary = { 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1 };
        foreach (double weight in new[] { .2, .5, 1.0 })
            VerifyLogisticHl("individual-weight-" + weight.ToString("R", inv), hlAge, hlBinary, null, Enumerable.Repeat(weight, 40).ToArray());
        VerifyLogisticHl("individual-zero-last", hlAge, hlBinary, null, Enumerable.Repeat(1.0, 39).Append(0).ToArray());
        VerifyLogisticHl("individual-nine-unweighted", Enumerable.Range(1, 9).Select(i => (double)i).ToArray(), new double[] { 0, 0, 1, 0, 1, 0, 1, 1, 1 }, null, null);

        // Include the fractional-frequency seeds that formerly overflowed the
        // native fixed arrays. No failure is silently caught or skipped.
        foreach (int seed in Enumerable.Range(1, 40))
        {
            Random rng = new(seed);
            int count = 25 + rng.Next(30);
            double[] predictor = Enumerable.Range(0, count).Select(i => Math.Round(i * 10.0 / count + rng.NextDouble() * .05, 4)).ToArray();
            double[] response = predictor.Select(v => rng.NextDouble() < 1 / (1 + Math.Exp(-(v - 5) * .6)) ? 1.0 : 0).ToArray();
            double[] choices = seed % 4 == 0 ? new[] { 0d, 1, 1, 2 } : seed % 4 == 1 ? new[] { .1, .2, .3 } : seed % 4 == 2 ? new[] { .5, 1.5, 2.5 } : new[] { 0d, .25, .5 };
            double[] weights = predictor.Select(_ => choices[rng.Next(choices.Length)]).ToArray();
            VerifyLogisticHl("seed-" + seed, predictor, response, null, weights);
        }

        void VerifyLogisticHl(string label, double[] predictor, double[] response, double[] totals, double[] weights)
        {
            object[] Cells(string title, double[] values) => new object[] { title }.Concat(values.Cast<object>()).ToArray();
            List<object[]> cells = new() { Cells("Dose", predictor), Cells("Outcome", response) };
            int totalColumn = -1, weightColumn = -1;
            if (totals != null) { totalColumn = cells.Count; cells.Add(Cells("Total", totals)); }
            if (weights != null) { weightColumn = cells.Count; cells.Add(Cells("Prior weight", weights)); }
            RecordCells grid = new(cells.ToArray());
            int rows = response.Length + 1;
            Dictionary<string, object> answers = new()
            {
                ["gamma"] = .95, ["grouping"] = totals == null ? "individual" : "grouped", ["accuracy"] = "0.000000001", ["intercept"] = true, ["weights"] = weights != null,
                ["response"] = grid.Acquire(1, 0, rows, DataAcquisitionMode.NumericPreserveRows),
                ["predictors"] = grid.Acquire(0, 0, rows, DataAcquisitionMode.NumericCodingTextToDummiesPreserveRows)
            };
            if (totals != null) answers["total"] = grid.Acquire(totalColumn, 0, rows, DataAcquisitionMode.NumericPreserveRows);
            if (weights != null) answers["weight"] = grid.Acquire(weightColumn, 0, rows, DataAcquisitionMode.NumericPreserveRows);
            var (run, output) = Run("LogisticRegression", answers);
            string script = Script(run);
            var (_, model) = Run("LogisticRegressionModel", new(), output.ParameterBag);
            ParameterBag bag = model.ParameterBag;
            double statistic = Number(bag, "hl"), df = Number(bag, "df_hl"), probability = Number(bag, "p_hl");
            string assertions = "\nstopifnot(hldf==" + df.ToString("R", inv) + ",is.finite(hl))\n" +
                Compare("hl,pchisq(hl,hldf,lower.tail=FALSE)", new[] { statistic, probability }) +
                "cat('NATIVE_HL_OK\\n')\n";
            string result = RunR(script + assertions, Path.Combine(folder, "logistic-hl-" + label));
            Check(result.Contains("NATIVE_HL_OK") && result.Contains("Coefficients and standard errors:"), "logistic HL " + label + " reproduces native statistic, df and P and completes the coefficient table");
        }

        void VerifyLogistic(string label, Dictionary<string, object> answers, int expectedPatterns)
        {
            var (run, output) = Run("LogisticRegression", answers);
            string script = Script(run);
            var (_, model) = Run("LogisticRegressionModel", new(), output.ParameterBag);
            ParameterBag bag = model.ParameterBag;
            double[] expected = new[] { "ll", "dev", "idf", "rank", "aka", "sch", "devx", "r2", "r2i", "x2", "hl", "df_hl", "p_hl" }.Select(key => Number(bag, key)).ToArray();
            IList<ParameterBag> coefficients = Block(bag, "parameters");
            string assertions = "\nstopifnot(n==" + expectedPatterns + "L,nobs(fit)==" + expectedPatterns + "L,all(complete.cases(responders,total,predictors,weight)))\n" +
                Compare("ll,dev,fit$df.residual,k,dev+2*k,dev+k*log(n),fit$null.deviance,1-dev/fit$null.deviance,1-ll/ll0,pearson,hl,hldf,pchisq(hl,hldf,lower.tail=FALSE)", expected) +
                Compare("coef(fit)", coefficients.Select(b => Number(b, "coef"))) + Compare("summary(fit)$coefficients[,2]", coefficients.Select(b => Number(b, "err")), 1e-7) +
                "cat('MODEL_MISSING_OK\\n')\n";
            Check(RunR(script + assertions, Path.Combine(folder, "logistic-" + label)).Contains("MODEL_MISSING_OK"), "logistic " + label + " reproduces native merged-pattern coefficients, criteria, Pearson and weighted Hosmer-Lemeshow diagnostics");
        }
    }
}
