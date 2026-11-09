using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.R;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    // Worked examples compare every numerical figure with their independent R reference. Graphical recipes probe the arrays they
    // actually plotted, rather than requiring the extra demonstrations and teaching text that used to accompany the selected chart.
    private static void CheckRecipesAgainstHelp()
    {
        RRecipes.Folder = Metadata("Recipes");
        string examples = Metadata("Examples");
        string folder = Path.Combine(TemporaryRoot, "statsdirect-rscript-recipes-" + Guid.NewGuid().ToString("N"));
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
                if (IsModelWorkedExample(recipeFile: RRecipes.Find(op).File))
                {
                    CheckModelWorkedExample(op, RRecipes.Find(op).File, example, scriptText,
                        File.ReadAllText(Path.Combine(examples, op + ".R")), runFolder);
                    agreed++;
                    continue;
                }
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

    private static bool IsModelWorkedExample(string recipeFile) => recipeFile is
        "logistic-regression.R" or "poisson-regression.R" or "multiple-linear-regression.R" or
        "cox-regression.R" or "conditional-logistic.R";

    // Compare actual model quantities, not the numerals in printed calls,
    // coefficient labels or iteration counts. The independent help scripts keep
    // their own data and formulas. Their glm is converged more tightly before
    // extracting covariance/Wald limits, as the production recipe now is.
    private static void CheckModelWorkedExample(string operation, string file, JsonElement example, string script, string reference, string folder)
    {
        double level = .95;
        if (example.TryGetProperty("settings", out var options))
            foreach (string key in new[] { "gamma", "ci" })
                if (options.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number) level = value.GetDouble();
        var (nativeExpression, nativeValues) = ModelWorkedExampleNative(file, example, level);
        string kind = RScriptWriter.Quote(file);
        string reportExpression = file switch
        {
            "logistic-regression.R" => "c(ll,dev,lr,df_lr,k,n,pearson,hl,hldf,r2,r2i)",
            "poisson-regression.R" => "c(ll,dev,lr,df_lr,k,n,pearson,scale,r2,r2i)",
            "multiple-linear-regression.R" => "c(ssreg,ssres,df,sum(diff(e)^2)/sum(e^2))",
            "cox-regression.R" => "c(ll,dev,df)",
            _ => "c(ll,ll0,-2*ll,2*(ll-ll0),1-ll/ll0)"
        };
        string referenceReport = file switch
        {
            "logistic-regression.R" => "c(model_ll,fit$deviance,fit$null.deviance-fit$deviance,fit$df.null-fit$df.residual,length(coef(fit)),nobs(fit),sum(residuals(fit,type='pearson')^2),model_hl,length(unique(group))-2,1-fit$deviance/fit$null.deviance,1-model_ll/model_ll0)",
            "poisson-regression.R" => "c(as.numeric(logLik(fit)),fit$deviance,fit$null.deviance-fit$deviance,fit$df.null-fit$df.residual,length(coef(fit)),nobs(fit),model_pearson,model_pearson/fit$df.residual,1-fit$deviance/fit$null.deviance,1-as.numeric(logLik(fit))/as.numeric(logLik(model_null)))",
            "multiple-linear-regression.R" => "c(sum(anova(fit)$'Sum Sq'[-nrow(anova(fit))]),tail(anova(fit)$'Sum Sq',1),fit$df.residual,sum(diff(residuals(fit))^2)/sum(residuals(fit)^2))",
            "cox-regression.R" => "c(fit$loglik,2*diff(fit$loglik),length(coef(fit)))",
            _ => "c(fit$loglik[2],fit$loglik[1],-2*fit$loglik[2],2*diff(fit$loglik),1-fit$loglik[2]/fit$loglik[1])"
        };
        string referenceExtras = file switch
        {
            "logistic-regression.R" => "model_y<-model.response(model.frame(fit));model_p<-fitted(fit)\nmodel_ll<-sum(model_y[,1]*log(model_p)+model_y[,2]*log1p(-model_p))\nmodel_p0<-sum(model_y[,1])/sum(model_y)\nmodel_ll0<-sum(model_y[,1]*log(model_p0)+model_y[,2]*log1p(-model_p0))\nmodel_observed<-rowsum(model_y[,1],group);model_expected<-rowsum(rowSums(model_y)*model_p,group);model_total<-rowsum(rowSums(model_y),group)\nmodel_hl<-sum((model_observed-model_expected)^2/(model_expected*(1-model_expected/model_total)))\n",
            "poisson-regression.R" => "model_y<-model.response(model.frame(fit));model_offset<-model.offset(model.frame(fit))\nmodel_null<-glm(model_y~1,offset=model_offset,family=poisson,control=glm.control(epsilon=1e-14,maxit=100))\nmodel_pearson<-sum(residuals(fit,type='pearson')^2)\n",
            _ => ""
        };
        // Cox's printed baseline rows also remain covered, without making the
        // row numbers or column headings part of the statistical comparison.
        string baseline = file == "cox-regression.R" ? "baseline=as.numeric(head(cbind(base$time[base$n.event>0],base$surv[base$n.event>0],base$cumhaz[base$n.event>0]),5))," : "";
        bool prefix = example.TryGetProperty("cut", out var cut) && cut.ValueKind == JsonValueKind.True;
        string text = ModelWorkedExampleR + "\nmodel_level<-" + level.ToString("R",inv) + "\n" +
            "model_actual<-local({\nmodel_output<-capture.output({\n" + script + "\n})\nlist(" + baseline + "rendered=model_rendered_numbers(model_output),statistics=model_statistics(fit,model_level),reported=" + reportExpression + ",native=c(" + nativeExpression + "))\n})\n" +
            "model_reference<-local({\n# Refine the independent help's own glm formula, including its printed Wald values.\nglm<-model_high_precision_glm\nmodel_output<-capture.output({\n" + reference + "\n})\n" + referenceExtras +
            "list(" + baseline + "rendered=model_rendered_numbers(model_output),statistics=model_statistics(fit,model_level),reported=" + referenceReport + ")\n})\n" +
            "model_equal(model_actual$statistics,model_reference$statistics,'independent high-precision R model',1e-9)\n" +
            "model_equal(model_actual$reported,model_reference$reported,'reported diagnostics',1e-8)\n" +
            "model_equal(model_actual$rendered," + (prefix ? "head(model_reference$rendered,length(model_actual$rendered))" : "model_reference$rendered") + ",'rendered statistical figures',1e-7)\n" +
            (file == "cox-regression.R" ? "model_equal(model_actual$baseline,model_reference$baseline,'baseline survival and hazard',1e-9)\n" : "") +
            "model_equal(model_actual$native," + RScriptWriter.Vector(nativeValues.Cast<object>().ToArray()) + ",'native model',1e-7)\ncat('MODEL_WORKED_EXAMPLE_OK\\n')\n";
        Check(RunR(text, folder).Contains("MODEL_WORKED_EXAMPLE_OK"), operation + " worked example matches independent R and native coefficients, covariance, Wald limits and reported diagnostics");
    }

    private static (string Expression, double[] Values) ModelWorkedExampleNative(string file, JsonElement example, double level)
    {
        string parent = file switch { "logistic-regression.R" => "LogisticRegression", "poisson-regression.R" => "PoissonRegression", "multiple-linear-regression.R" => "MultipleLinearRegression", "cox-regression.R" => "CoxRegression", _ => "ConditionalLogisticRegression" };
        Dictionary<string,object> answers = new()
        {
            ["gamma"]=level,["ci"]=level,["intercept"]=true,["calculateIntercept"]=true,["grouping"]=file=="logistic-regression.R"?"grouped":"individual",
            ["accuracy"]=file=="cox-regression.R"?(object)1e-11:"0.00000000001",["weights"]=false,["useWeights"]=false,["has-exposure"]="true",["show-counts"]=false,
            ["centre-continuous-covariates"]=true,["useTimesAdjustment"]=false,["strata"]=null
        };
        if(example.TryGetProperty("settings",out var settings))
            foreach(var setting in settings.EnumerateObject())
                answers[setting.Name]=setting.Value.ValueKind switch
                {
                    JsonValueKind.True=>true,JsonValueKind.False=>false,JsonValueKind.Number=>setting.Value.GetDouble(),
                    JsonValueKind.String=>setting.Value.GetString(),_=>answers.GetValueOrDefault(setting.Name)
                };
        foreach (JsonElement input in example.GetProperty("inputs").EnumerateArray())
        {
            string name=input.GetProperty("name").GetString();
            DataFrame frame=new();
            foreach(JsonElement column in input.GetProperty("columns").EnumerateArray())
            {
                double[] values=column.GetProperty("values").EnumerateArray().Select(v=>v.GetDouble()).ToArray();
                if(name is "strata" or "stratum")
                {
                    double[] levels=values.Distinct().OrderBy(v=>v).ToArray();
                    ClassifierVariable classifier=new(){Title=column.GetProperty("title").GetString()};
                    classifier.EnsureLength(values.Length);
                    for(int i=0;i<values.Length;i++)classifier.SetData(i,Array.IndexOf(levels,values[i]));
                    foreach(double v in levels)classifier.Groups.Add(new StatsDirect.Data.Group(v.ToString(inv),Array.IndexOf(levels,v)){NBin=values.Count(x=>x==v)});
                    frame.Variables.Add(classifier);
                }
                else frame.Variables.Add(Column(column.GetProperty("title").GetString(),values));
            }
            answers[name]=frame.VariableCount>0?frame:null;
        }
        var(_,output)=Run(parent,answers);
        ParameterBag bag=output.ParameterBag;
        double Num(ParameterBag b,string key)=>Convert.ToDouble(b[key].AsObject,inv);
        IList<ParameterBag> Block(ParameterBag b,string key)=>(IList<ParameterBag>)b["*"+key].AsObject;
        IEnumerable<double> Numbers(ParameterBag b,params string[] keys)=>keys.Select(k=>Num(b,k));
        if(file is "logistic-regression.R" or "poisson-regression.R")
        {
            bool logistic=file=="logistic-regression.R";
            var model=Run(logistic?"LogisticRegressionModel":"PoissonRegressionModel",new(),bag).output.ParameterBag;
            var coefficients=Block(model,logistic?"parameters":"unscaled");
            string expression=logistic?
                "coef(fit),summary(fit)$coefficients[,2],ll,dev,fit$df.residual,k,dev+2*k,dev+k*log(n),null_deviance,lr,df_lr,pchisq(lr,df_lr,lower.tail=FALSE),r2,r2i,pearson,pchisq(pearson,fit$df.residual,lower.tail=FALSE),hl,hldf,pchisq(hl,hldf,lower.tail=FALSE)":
                "coef(fit),summary(fit)$coefficients[,2],ll,dev,fit$df.residual,k,dev+2*k,dev+k*log(n),null_deviance,lr,df_lr,pchisq(lr,df_lr,lower.tail=FALSE),r2,r2i,pearson,pchisq(pearson,fit$df.residual,lower.tail=FALSE),scale,lr/scale,pchisq(lr/scale,df_lr,lower.tail=FALSE),pearson/scale,pchisq(pearson/scale,fit$df.residual,lower.tail=FALSE),dev/scale,pchisq(dev/scale,fit$df.residual,lower.tail=FALSE)";
            string[] keys=logistic?new[]{"ll","dev","idf","rank","aka","sch","devx","x2dev","x2df","x2p","r2","r2i","x2","p","hl","df_hl","p_hl"}:
                new[]{"ll","dev","idf","rank","aka","sch","devx","x2dev","x2df","x2p","r2","r2i","x2_pearson","p_pearson","sp","x2dev_scaled","x2p_scaled","x2_scaled","p_p_scaled","dev_scaled","p_dev_scaled"};
            return(expression,coefficients.Select(b=>Num(b,"coef")).Concat(coefficients.Select(b=>Num(b,"err"))).Concat(Numbers(model,keys)).ToArray());
        }
        if(file=="multiple-linear-regression.R")
        {
            var anova=Run("MultipleLinearRegressionAnova",new(){["ci"]=level},bag).output.ParameterBag;
            return("coef(fit),ssreg,k,ssreg/k,ssres,df,ssres/df,ssreg+ssres,df+k,sqrt(ssres/df),summary(fit)$fstatistic[1],pf(summary(fit)$fstatistic[1],k,df,lower.tail=FALSE),100*summary(fit)$r.squared,100*summary(fit)$adj.r.squared,sum(diff(e)^2)/sum(e^2)",
                AcquisitionCoefficients(bag).Concat(Numbers(anova,"reg_sum","reg_df","reg_mean","res_sum","res_df","res_mean","tot_sum","tot_df","mse","f","p","r2","ra2","dw")).ToArray());
        }
        if(file=="cox-regression.R")
        {
            var model=Run("CoxModelAnalysis",new(),bag).output.ParameterBag;
            var hr=Run("CoxHazardRatios",new(){["gamma"]=level},bag).output.ParameterBag;
            var parameters=Block(hr,"parameter");var limits=Block(hr,"hazard");
            return("coef(fit),sqrt(diag(vcov(fit))),ll,dev,df,pchisq(dev,df,lower.tail=FALSE),fit$n,fit$nevent,exp(coef(fit)),exp(coef(fit)-qnorm((1+model_level)/2)*sqrt(diag(vcov(fit)))),exp(coef(fit)+qnorm((1+model_level)/2)*sqrt(diag(vcov(fit))))",
                parameters.Select(b=>Num(b,"coef")).Concat(parameters.Select(b=>Num(b,"se"))).Concat(Numbers(model,"ll0","ll","x2","df","p")).Concat(Numbers(bag,"n","d")).Concat(limits.Select(b=>Num(b,"ec"))).Concat(limits.Select(b=>Num(b,"ell"))).Concat(limits.Select(b=>Num(b,"eul"))).ToArray());
        }
        var estimates=Block(bag,"est");
        return("coef(fit),sqrt(diag(vcov(fit))),-2*ll,2*(ll-ll0),pchisq(2*(ll-ll0),length(coef(fit)),lower.tail=FALSE),1-ll/ll0",
            estimates.Select(b=>Num(b,"b")).Concat(estimates.Select(b=>Num(b,"se"))).Concat(Numbers(bag,"dv","x2","p_dev","r2")).ToArray());
    }

    private const string ModelWorkedExampleR = """
        model_equal <- function(actual,expected,label,tolerance) {
          actual<-as.numeric(actual);expected<-as.numeric(expected)
          stopifnot(length(actual)==length(expected),identical(is.na(actual),is.na(expected)),
                    identical(is.infinite(actual),is.infinite(expected)),
                    identical(actual[is.infinite(expected)],expected[is.infinite(expected)]))
          use<-is.finite(expected)
          bad<-which(use & abs(actual-expected)>tolerance*pmax(1,abs(expected)))
          if(length(bad))stop(label,': numerical mismatch at ',bad[1],': ',actual[bad[1]],' versus ',expected[bad[1]])
        }
        model_high_precision_glm <- function(...) {
          model_call<-match.call(expand.dots=TRUE)
          model_call[[1L]]<-quote(stats::glm)
          model_call$control<-quote(stats::glm.control(epsilon=1e-14,maxit=100))
          eval(model_call,parent.frame())
        }
        model_statistics <- function(fit,level) {
          result<-c(coef(fit),as.vector(vcov(fit)),as.vector(summary(fit)$coefficients),as.vector(confint.default(fit,level=level)))
          if(inherits(fit,'glm')) {
            result<-c(result,fit$deviance,fit$null.deviance,fit$df.residual,fit$df.null,fit$rank,
                      sum(residuals(fit,type='pearson')^2),fitted(fit),residuals(fit,type='deviance'))
          } else if(inherits(fit,'coxph')) {
            result<-c(result,fit$loglik,fit$n,fit$nevent,exp(coef(fit)),as.vector(summary(fit,conf.int=level)$conf.int))
          } else {
            result<-c(result,as.vector(as.matrix(anova(fit))),fit$df.residual,summary(fit)$sigma,
                      summary(fit)$r.squared,summary(fit)$adj.r.squared,fitted(fit),residuals(fit))
          }
          unname(result)
        }
        model_rendered_numbers <- function(lines) {
          # A formula/control call and a convergence iteration count are metadata,
          # not statistical results. Keep all coefficient tables, confidence
          # limits, likelihood/GOF lines, IRR combinations and baseline rows.
          drop<-logical(length(lines));in_call<-FALSE
          for(i in seq_along(lines)) {
            if(grepl('^Call:',lines[i])) {in_call<-TRUE;drop[i]<-TRUE}
            else if(in_call) {drop[i]<-TRUE;if(!nzchar(trimws(lines[i])))in_call<-FALSE}
          }
          lines<-lines[!drop & !grepl('Fisher Scoring iterations:|^[[:space:]]*Iterations:',lines)]
          text<-gsub('\\[[0-9]*,[0-9]*\\]',' ',paste(lines,collapse='\n'))
          as.numeric(regmatches(text,gregexpr('-?[0-9]+(?:\\.[0-9]+)?(?:[eE][-+]?[0-9]+)?',text,perl=TRUE))[[1]])
        }
        """;

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
        "HistogramPlot" => "emit(length(histograms))\nfor (h in histograms) emit(c(h$n, h$breaks, h$counts, h$mids, h$heights))\nemit(sd_histogram_doane(series[[1]]))\n",
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
