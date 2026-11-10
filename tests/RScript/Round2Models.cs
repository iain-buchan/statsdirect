using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

internal static partial class Program
{
    private static void CheckRound2Models(string folder, string only = null)
    {
        RRecipes.Folder = Metadata("Recipes");
        if (only is null or "s12") CheckRound2LogisticPatterns(folder);
        if (only is null or "s13") CheckRound2GlmAliases(folder);
        if (only is null or "s15") CheckRound2PoissonSaturation(folder);
        if (only is null or "s16") CheckRound2LinearSaturation(folder);
        if (only is null or "s17") CheckRound2LinearTotals(folder);
        if (only is null or "s19") CheckRound2CoxCoding(folder);
        if (only is null or "x1") CheckRound2PoissonIncidence(folder);
        if (only is null or "p1") CheckRound2Precision(folder);
        if (only is null or "irr") CheckRound2IrrSelection(folder);
        if (only is null or "auc") CheckRound2ConstantPolynomial(folder);
    }

    private static (OperationRun Run, ParameterBag Main, ParameterBag Model) Round2Glm(bool logistic,
        double[] y, (string Name, double[] Values)[] predictors, string precision = "0.0000001",
        double[] weights = null, double[] exposure = null)
    {
        var columns = new List<(string, double[])> { ("Y", y) };
        columns.AddRange(predictors);
        if (weights != null) columns.Add(("Prior", weights));
        if (exposure != null) columns.Add(("Exposure", exposure));
        var sheet = Models539Sheet(columns.ToArray());
        var answers = new Dictionary<string, object>
        {
            ["response"] = Models539Select(sheet, 0),
            ["predictors"] = Models539Select(sheet, Enumerable.Range(1, predictors.Length).ToArray()),
            ["gamma"] = .95, ["accuracy"] = precision, ["grouping"] = "individual",
            ["intercept"] = true, ["weights"] = weights != null, ["has-exposure"] = exposure == null ? "false" : "true"
        };
        if (weights != null) answers["weight"] = Models539Select(sheet, predictors.Length + 1);
        if (exposure != null) answers["exposure"] = Models539Select(sheet, columns.Count - 1);
        string operation = logistic ? "LogisticRegression" : "PoissonRegression";
        var (run, main) = Run(operation, answers);
        return (run, main.ParameterBag, Run(operation + "Model", new(), main.ParameterBag).output.ParameterBag);
    }

    private static double[] Round2Coefficients(bool logistic, ParameterBag model) =>
        Models539Block(model, logistic ? "parameters" : "unscaled").Select(row => Models539Number(row, "coef")).ToArray();

    private static string Round2CompareGlm(bool logistic, ParameterBag model, string independentFit, double tolerance = 1e-7) =>
        independentFit + "\nb_oracle <- coef(oracle); b_oracle[is.na(b_oracle)] <- 0\n" +
        Models539Equal("b_oracle", Round2Coefficients(logistic, model), tolerance) +
        Models539Equal("oracle$deviance, oracle$rank, oracle$df.residual, oracle$deviance + 2*oracle$rank, oracle$deviance + oracle$rank*log(nobs(oracle))",
            new[] { "dev", "rank", "idf", "aka", "sch" }.Select(key => Models539Number(model, key)), tolerance) +
        "stopifnot(identical(is.na(coef(fit)), is.na(coef(oracle))))\n" +
        Models539Equal("coef(fit)[!is.na(coef(fit))]", Round2Coefficients(logistic, model)
            .Zip(Models539Block(model, logistic ? "parameters" : "unscaled")).Where(pair => Models539Number(pair.Second, "err") > 0)
            .Select(pair => pair.First), tolerance);

    private static void CheckRound2LogisticPatterns(string folder)
    {
        double[] response = { 0, 0, 1, 0, 1, 1 }, x = { 0, 0, 0, 1, 1, 1 };
        foreach (var (label, y, predictors, oracle) in new[]
        {
            ("single-pattern", new[] { 0d, 1, 1 }, new[] { ("K", new[] { 2d, 2, 2 }) },
                "oracle <- glm(cbind(2,1) ~ K, data=data.frame(K=2), family=binomial)"),
            ("two-pattern-constant", response, new[] { ("X", x), ("K", Enumerable.Repeat(2d,6).ToArray()) },
                "oracle <- glm(cbind(c(1,2),c(2,1)) ~ X+K, data=data.frame(X=0:1,K=2), family=binomial)"),
            ("two-pattern-affine", response, new[] { ("X", x), ("X2", x.Select(v => 2*v+1).ToArray()) },
                "oracle <- glm(cbind(c(1,2),c(2,1)) ~ X+X2, data=data.frame(X=0:1,X2=c(1,3)), family=binomial)")
        })
        {
            var (run, main, model) = Round2Glm(true, y, predictors, "0.0000000001");
            Check(Models539Number(model, "idf") == 0 && Models539Number(model, "rank") == predictors.Length,
                label + ": compacted covariate patterns give the saturated estimable fit");
            Check(Models539Warnings(main).Any(w => w.Contains(predictors[^1].Item1 + " dropped from the model")),
                label + ": native warning names the dropped predictor");
            Models539Verify(folder, label, run, Round2CompareGlm(true, model, oracle));
        }
        string refusal = Models5310Refused(() => Round2Glm(true,
            new[] { 0d,1,Constant.MISSING,Constant.MISSING,Constant.MISSING,Constant.MISSING },
            new[] { ("A", new[] { 0d,1,0,1,0,1 }), ("B", new[] { 0d,1,2,3,4,5 }) }));
        Check(refusal?.Contains("fewer usable observations than the rank") == true,
            "logistic refuses when omitted rows leave fewer observations than the original design rank");
    }

    private static void CheckRound2GlmAliases(string folder)
    {
        double[] x = { 1, 2, 3, 4, 5, 6 }, affine = x.Select(v => 2*v+1).ToArray();
        foreach (bool logistic in new[] { true, false })
        {
            double[] y = logistic ? new[] { 0d, 0, 1, 0, 1, 1 } : new[] { 2d, 5, 3, 9, 6, 14 };
            var (_, _, control) = Round2Glm(logistic, y, new[] { ("A", x) }, "1e-10");
            foreach (var (label, alias, expression) in new[]
            {
                ("Affine", affine, "2*A+1"), ("Constant", Enumerable.Repeat(2d,6).ToArray(), "rep(2,6)")
            })
            {
                var (run, main, model) = Round2Glm(logistic, y, new[] { ("A", x), (label, alias) }, "1e-15");
                string id = (logistic ? "binomial-" : "poisson-") + label;
                Check(Models539Warnings(main).Any(w => w.Contains(label + " dropped from the model")) &&
                    Round2Coefficients(logistic, model)[2] == 0 && Models539Number(model, "rank") == 2,
                    id + ": preserve the earlier predictor and name the alias");
                Check(new[] { "aka", "sch" }.All(key => Math.Abs(Models539Number(model, key) - Models539Number(control, key)) < 1e-8),
                    id + ": an alias cannot increase AIC or BIC");
                string oracle = "A <- " + Models539Vector(x) + "\nY <- " + Models539Vector(y) + "\nB <- " + expression +
                    "\noracle <- glm(Y ~ A+B, family=" + (logistic ? "binomial" : "poisson") + ")\n";
                // The independent formula has a different alias label.
                string comparison = Round2CompareGlm(logistic, model, oracle)
                    .Replace("identical(is.na(coef(fit)), is.na(coef(oracle)))", "identical(unname(is.na(coef(fit))), unname(is.na(coef(oracle))))");
                string output = Models539Verify(folder, id, run, comparison);
                Check(Regex.IsMatch(output, "^" + label + @" \* error: drop this variable \*", RegexOptions.Multiline),
                    id + ": replay names the unavailable coefficient");
            }
        }
        double[] veteran = { 0,1,0,1,1,0,0,1,1,0,1,0 };
        var (poisson, _, poissonModel) = Round2Glm(false, Models539Counts,
            new[] { ("Veteran", veteran), ("Dose", Models539Dose), ("NonVeteran", veteran.Select(v => 1-v).ToArray()) },
            exposure: Models539Exposures);
        string summedAlias = "Y <- " + Models539Vector(Models539Counts) + "\nE <- " + Models539Vector(Models539Exposures) +
            "\nVeteran <- " + Models539Vector(veteran) + "\nDose <- " + Models539Vector(Models539Dose) +
            "\nNonVeteran <- 1-Veteran\noracle <- glm(Y ~ Veteran+Dose+NonVeteran, offset=log(E), family=poisson)\n";
        Check(Round2Coefficients(false, poissonModel)[3] == 0, "Poisson drops the later predictor when two dummy columns sum to the intercept");
        Models539Verify(folder, "poisson-sum-alias", poisson, Round2CompareGlm(false, poissonModel, summedAlias));
    }

    private static void CheckRound2PoissonSaturation(string folder)
    {
        double[] x = { 1, 2, 3 }, counts = { 2, 5, 7 }, a = { .5, 1.75, 1.25, 2.5 }, b = { 6.5, 2.25, 5.75, 4.5 };
        foreach (var (label, y, predictors, weights, rank) in new[]
        {
            ("two-equal", new[] { 2d, 5 }, new[] { ("X", x[..2]) }, (double[])null, 2),
            ("two-zero-weight", counts, new[] { ("X", x) }, new[] { 1d, 1, 0 }, 2),
            ("two-missing", new[] { 2d, Constant.MISSING, 5 }, new[] { ("X", new[] { 1d, 3, 2 }) }, null, 2),
            ("three-equal", new[] { 2d, 5, 3 }, new[] { ("A", a[..3]), ("B", b[..3]) }, null, 3),
            ("three-zero-weight", new[] { 2d, 5, 3, 9 }, new[] { ("A", a), ("B", b) }, new[] { 1d, 1, 1, 0 }, 3)
        })
        {
            var (run, main, model) = Round2Glm(false, y, predictors, weights: weights);
            Check(Models539Number(model, "idf") == 0 && Models539Number(model, "rank") == rank &&
                Models539Warnings(main).Any(w => w.Contains("saturated model")),
                label + ": exact Poisson fit is accepted after complete positive-weight filtering");
            string oracle = rank == 2 ? "oracle <- glm(c(2,5) ~ X, data=data.frame(X=1:2), family=poisson)\n"
                : "oracle <- glm(c(2,5,3) ~ A+B, data=data.frame(A=c(.5,1.75,1.25),B=c(6.5,2.25,5.75)), family=poisson)\n";
            Models539Verify(folder, label, run, Round2CompareGlm(false, model, oracle));
        }
        foreach (bool zero in new[] { false, true })
        {
            string refusal = Models5310Refused(() => Round2Glm(false, zero ? new[] { 2d,5,3,9 } : new[] { 2d,5 },
                zero ? new[] { ("A", a), ("B", b) } : new[] { ("A", a[..2]), ("B", b[..2]) },
                weights: zero ? new[] { 1d,1,0,0 } : null));
            Check(refusal?.Contains("More parameters than complete observations") == true,
                "Poisson still refuses more selected parameters than usable rows (zero weights = " + zero + ")");
        }
    }

    private static void CheckRound2LinearSaturation(string folder)
    {
        double[] y = { 3.9, 7.2, 5.5 }, p = { 1.3, 2.9, 2.2 }, q = { 4, 1.7, 3.5 };
        foreach (int n in new[] { 2, 3 })
        {
            var columns = new List<(string, double[])> { ("Y", y[..n]), ("P", p[..n]), ("P2", p[..n].Select(v => 2*v).ToArray()) };
            if (n == 3) columns.Add(("Q", q));
            var (run, model) = Models539Linear(Models539Sheet(columns.ToArray()), 0, -1, Enumerable.Range(1, columns.Count-1).ToArray(), true);
            string[] warnings = Models539Warnings(model);
            Check(warnings.Any(w => w.StartsWith("P2 dropped from the model")) && warnings.Any(w => w.StartsWith("saturated model")),
                n + " complete rows: exact linear refit names the alias and gives the saturation warning");
            var anova = Models539Anova(model);
            Check(Models539Number(anova, "res_df") == 0 && Models539Number(anova, "f") == Constant.MISSING,
                n + " complete rows: the exact refit has no residual tests");
            string oracle = "Y <- " + Models539Vector(y[..n]) + "\nP <- " + Models539Vector(p[..n]) +
                "\nP2 <- 2*P\n" + (n == 3 ? "Q <- " + Models539Vector(q) + "\n" : "") +
                "oracle <- lm(Y ~ P+P2" + (n == 3 ? "+Q" : "") + ")\n" +
                Models539Equal("coef(oracle)[!is.na(coef(oracle))]", AcquisitionCoefficients(model)) +
                "stopifnot(oracle$df.residual==0L,fit$df.residual==0L,is.na(coef(fit)['P2']))\n";
            string rendered = Models539Verify(folder, "linear-saturated-" + n, run, oracle);
            Check(rendered.Contains("P2 dropped from the model"), "linear replay names P2 in the same warning");
        }
    }

    private static void CheckRound2LinearTotals(string folder)
    {
        var sheet = Models539Sheet(("Y", new[] { 3.9, 7.2, 5.5 }), ("P", new[] { 1.3, 2.9, 2.2 }));
        foreach (bool intercept in new[] { false, true })
        {
            var (_, model) = Models539Linear(sheet, 0, -1, new[] { 1 }, intercept);
            var (follow, output) = Run("MultipleLinearRegressionAnova", new() { ["ci"] = .95 }, model);
            var anova = output.ParameterBag;
            string label = intercept ? "Total (corrected)" : "Total (uncorrected)";
            Check(anova.TryGetValue("tot_label", out FilledParameter total) && total.AsString == label,
                "linear ANOVA labels its " + (intercept ? "centred" : "uncentred") + " total correctly");
            string oracle = "Y <- c(3.9,7.2,5.5); P <- c(1.3,2.9,2.2)\n" +
                "oracle <- lm(Y ~ " + (intercept ? "P" : "0+P") + ")\n" +
                Models539Equal(intercept ? "sum((Y-mean(Y))^2), 2" : "sum(Y^2), 3",
                    new[] { Models539Number(anova, "tot_sum"), Models539Number(anova, "tot_df") });
            Check(Models539Verify(folder, "linear-total-" + intercept, follow, oracle).Contains(label),
                "linear ANOVA replay uses the same total label");
        }
        // A polynomial explicitly offers the ANOVA follow-on. Its prerequisite
        // marker lets the writer inherit the actual root rather than guessing it.
        var (_, polynomial) = Models5310Polynomial(Models539Sheet(("Y", new[] { 3d,1,7,4 }), ("X", new[] { 1d,2,4,5 })), 2);
        var (run, _) = Run("MultipleLinearRegressionAnova", new() { ["ci"] = .95 }, polynomial);
        Check(Load("PolynomialRegression").SuggestedOperations.Any(s => s.Name == "MultipleLinearRegressionAnova") &&
            RRecipes.Find(Record(run))?.File == "polynomial-regression.R",
            "a polynomial offers ANOVA and its actual follow-on record chooses the polynomial replay");
    }

    private static void CheckRound2CoxCoding(string folder)
    {
        double[] t = { 6,7,10,15,19,25,2,4,8,12,14,20,23,30,31 };
        double[] e = { 1,1,1,1,0,1,1,1,1,0,1,1,1,0,1 };
        double[] x = { 0,1,0,1,0,1,0,1,0,1,0,1,0,1,2 };
        foreach (bool complete in new[] { false, true })
        {
            object[] time = new object[] { "Time" }.Concat(t.Cast<object>()).ToArray();
            if (!complete) time[^1] = "*";
            var cells = new RecordCells(time, new object[] { "Event" }.Concat(e.Cast<object>()).ToArray(),
                new object[] { "Treated" }.Concat(x.Cast<object>()).ToArray());
            object Select(int column) => new Func<ITemplateProcessor, Parameter, ParameterBag, ITemplateHost, ParameterBag>((processor, parameter, context, host) =>
            {
                IGrid grid = DispatchProxy.Create<IGrid, AlignedGrid>();
                var proxy = (AlignedGrid)(object)grid;
                proxy.Cells = cells; proxy.Host = host;
                proxy.Selection = new CellSelection { LongestRowCount = time.Length };
                proxy.Selection.ColumnSelections.Add(new CellColumnSelection(cells) { ColumnIndex = column, RowIndex = 0, RowCount = time.Length, WorkbookPath = Workbook, WorksheetName = "Cox fixture" });
                var selector = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
                return (ParameterBag)selector.GetMethod("FillFrameParameter", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Activator.CreateInstance(selector, grid), new object[] { parameter, processor, context });
            });
            int prompts = 0;
            var (run, output) = Run("CoxRegression", new() { ["times"] = Select(0), ["events"] = Select(1), ["predictors"] = Select(2),
                ["strata"] = null, ["accuracy"] = 1e-9, ["centre-continuous-covariates"] = false, ["useTimesAdjustment"] = false },
                amend: fill => { if (fill is DummyOptions options) { prompts++; options.JDrop = 0; } });
            Check(prompts == (complete ? 1 : 0), "Cox predictor coding counts only rows with a time and an event (complete = " + complete + ")");
            if (!complete)
            {
                double coef = Models539Number(Models539Block(output.ParameterBag, "pred")[0], "b");
                string oracle = "Time <- " + Models539Vector(t[..14]) + "\nEvent <- " + Models539Vector(e[..14]) +
                    "\nTreated <- " + Models539Vector(x[..14]) + "\noracle <- survival::coxph(Surv(Time,Event)~Treated,ties='breslow')\n" +
                    Models539Equal("coef(oracle)", new[] { coef }) + "stopifnot(fit$n==14L,fit$nevent==11L,df==1L)\n";
                Models539Verify(folder, "cox-complete-coding", run, oracle);
            }
        }
        // An older record can already contain the unnecessary all-zero dummy.
        var legacy = Models539Sheet(("Time", t[..14]), ("Event", e[..14]), ("Treated(1)", x[..14]), ("Treated(2)", new double[14]));
        var (oldRun, _) = Run("CoxRegression", new() { ["times"] = Models539Select(legacy,0), ["events"] = Models539Select(legacy,1),
            ["predictors"] = Models539Select(legacy,2,3), ["strata"] = null, ["accuracy"] = 1e-9, ["centre-continuous-covariates"] = false, ["useTimesAdjustment"] = false });
        string oldText = Models539Verify(folder, "cox-legacy-aliased", oldRun,
            "stopifnot(df==1L,is.na(coef(fit)[2]),abs(coef(fit)[1]+.7478852)<1e-7)\n");
        Check(Regex.IsMatch(oldText, @"Treated\(2\).* b2 = \* +z = \* +P = \*"), "Cox replay reports an aliased coefficient as unavailable and finishes");
    }

    private static void CheckRound2PoissonIncidence(string folder)
    {
        double[] y = { 2,3,6,7,8,9,10,12 }, x = { 1,1.5,2.1,2.8,3.2,3.9,4.4,5.1 }, pt = { 10,12,15,11,14,13,12,16 };
        foreach (string kind in new[] { "plain", "missing", "zero", "exposure" })
        {
            double[] response = (double[])y.Clone();
            if (kind == "missing") response[2] = Constant.MISSING;
            var (run, main, _) = Round2Glm(false, response, new[] { ("X", x) },
                weights: kind == "zero" ? new[] { 1d,1,0,1,1,1,1,1 } : null, exposure: kind == "exposure" ? pt : null);
            var rows = Models539Block(Run("PoissonRegressionFitToReport", new(), main).output.ParameterBag, "events");
            Check(rows.All(row => kind == "exposure"
                ? Math.Abs(Models539Number(row,"iro") - Models539Number(row,"obs") / pt[(int)Models539Number(row,"idx")-1]) < 1e-12 &&
                    Math.Abs(Models539Number(row,"ire") - Models539Number(row,"fit") / pt[(int)Models539Number(row,"idx")-1]) < 1e-12
                : Models539Number(row,"iro") == Constant.MISSING && Models539Number(row,"ire") == Constant.MISSING),
                kind + ": incidence columns are available only with selected exposure");
            Models539Verify(folder, "poisson-incidence-" + kind, run, Models539Equal("fitted(fit)", rows.Select(row => Models539Number(row,"fit")), 1e-7));
        }
    }

    private static void CheckRound2Precision(string folder)
    {
        double[] x = Enumerable.Range(1,10).Select(i => (double)i).ToArray();
        foreach (bool logistic in new[] { false, true })
        {
            double[] y = logistic ? new[] { 0d,0,1,0,1,1,0,1,1,1 } : new[] { 2d,3,6,7,8,9,10,12,15,20 };
            string operation = logistic ? "LogisticRegression" : "PoissonRegression";
            string xml = File.ReadAllText(Path.Combine(Metadata("Operations"), operation + ".xml"));
            Check(!xml.Contains("<maximum-length>12</maximum-length>"), operation + ": the full precision entry fits the prompt");
            var (_, _, defaults) = Round2Glm(logistic, y, new[] { ("X", x) });
            var (_, _, tight) = Round2Glm(logistic, y, new[] { ("X", x) }, "1e-11");
            foreach (string text in new[] { "0", "-0.0001", "0.00000000001", ".00000000001", "1E-11", "1d-3" })
            {
                var (run, _, model) = Round2Glm(logistic, y, new[] { ("X", x) }, text);
                bool fallback = text is "0" or "-0.0001";
                double expected = fallback ? 1e-7 : text == "1d-3" ? 1e-3 : 1e-11;
                JsonElement setting = Record(run).GetProperty("parameters").GetProperty("accuracy");
                Check(setting.ValueKind == JsonValueKind.Number && setting.GetDouble() == expected,
                    operation + ": saved precision is the effective value for " + text);
                if (text != "1d-3") Check(Round2Coefficients(logistic, model).SequenceEqual(Round2Coefficients(logistic, fallback ? defaults : tight)),
                    operation + ": alternate precision entry produces the identical native fit for " + text);
                string oracle = "stopifnot(precision==" + expected.ToString("R", inv) + ",fit$control$epsilon==precision)\n";
                if (text is "0" or "0.00000000001")
                    oracle += "Y <- " + Models539Vector(y) + "\nX <- 1:10\noracle <- glm(Y~X,family=" +
                        (logistic ? "binomial" : "poisson") + ",control=glm.control(epsilon=1e-14,maxit=100))\n" +
                        Models539Equal("coef(oracle)", Round2Coefficients(logistic, model), 1e-8);
                Models539Verify(folder, operation + "-precision-" + text, run, oracle);
            }
        }
    }

    private static void CheckRound2IrrSelection(string folder)
    {
        double[] fixedValue = Enumerable.Repeat(1d,12).ToArray(), veteran = { 0,1,0,1,1,0,0,1,1,0,1,0 };
        var (_, main, _) = Round2Glm(false, Models539Counts, new[] { ("Fixed", fixedValue), ("Veteran", veteran), ("Dose", Models539Dose) },
            exposure: Models539Exposures);
        var (follow, output) = Run("PoissonRegressionIRR", new() { ["gamma"] = .95, ["cov"] = new[] { true } }, main);
        var tables = Models539Block(output.ParameterBag, "pop");
        var dose = Models539Block(tables[1], "par").First(row => row["par"].AsString == "Dose");
        string oracle = "group_coef <- sum(coef(fit)[c('Veteran','Dose')])\n" +
            "group_half <- qnorm(.975)*sqrt(sum(vcov(fit)[c('Veteran','Dose'),c('Veteran','Dose')]))\n" +
            Models539Equal("exp(group_coef+c(0,-group_half,group_half))", new[] { "irr", "lci", "uci" }.Select(key => Models539Number(dose,key)));
        string replay = Models539Verify(folder, "irr-aliased-first", follow, oracle);
        Check(replay.Contains("Veteran = 1: Dose") && replay.Contains("Veteran = 0: Dose") && !replay.Contains("Fixed = 1:"),
            "both Veteran sub-populations replay after a leading alias");
        string refusal = Models5310Refused(() => Run("PoissonRegressionIRR", new() { ["gamma"] = .95, ["cov"] = new[] { true, false } }, main));
        Check(refusal?.Contains("selected sub-population is no longer available") == true, "native IRR refuses an old selection from a longer list");
        JsonNode old = JsonNode.Parse(Record(follow).GetRawText());
        old["parameters"]["cov"] = new JsonArray(JsonValue.Create(true), JsonValue.Create(false));
        string saved = RScriptWriter.Write(JsonDocument.Parse(old.ToJsonString()).RootElement, null, null).Text;
        string replayRefusal = Models5310Refused(() => RunR(saved, Path.Combine(folder, "irr-stale-choice")));
        Check(replayRefusal?.Contains("selected sub-population is no longer available") == true, "R refuses the same stale sub-population choice");
    }

    private static void CheckRound2ConstantPolynomial(string folder)
    {
        var sheet = Models539Sheet(("Y", new[] { 3d,1,7,4 }), ("X", new[] { 2d,2,2,2 }));
        var (run, model) = Models5310Polynomial(sheet, 2);
        var result = Run("PolynomialAreaUnderCurve", new() { ["x"] = Models539Select(sheet,1), ["y"] = Models539Select(sheet,0) }, model).output.ParameterBag;
        Check(result["poly_auc"].AsString == "0" && Models539Number(result,"trap_auc") == 0,
            "the fitted and trapezoidal areas over a zero-width interval are both zero");
        Models539Verify(folder, "constant-polynomial", run, "stopifnot(curve_area==0,trapezia==0,is.null(summary(fit)$fstatistic),length(coef(fit)[!is.na(coef(fit))])==1L)\n");
    }
}
