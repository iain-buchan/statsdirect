using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StatsDirect.Builtins;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    private static void CheckDeferredIndependent538(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        Directory.CreateDirectory(folder);
        double Number(ParameterBag bag, string key)
        {
            object value = bag[key].AsObject;
            if (value is string text) return double.Parse(Regex.Match(text, @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[Ee][-+]?\d+)?").Value, inv);
            return Convert.ToDouble(value, inv);
        }
        string Vector(IEnumerable<double> values) => RScriptWriter.Vector(values.Select(v => v == Constant.MISSING || double.IsNaN(v) ? null : (object)v).ToArray());
        string Equal(string expression, IEnumerable<double> values, double tolerance = 1e-8) =>
            "stopifnot(isTRUE(all.equal(unname(c(" + expression + ")) ," + Vector(values) + ",tolerance=" + tolerance.ToString("R", inv) + ",check.attributes=FALSE)))\n";
        DataFrame Frame(params (string title, double[] values)[] columns)
        {
            DataFrame frame = new();
            foreach (var column in columns) frame.Variables.Add(Column(column.title, column.values.ToArray()));
            return frame;
        }
        string Replay(string label, OperationRun run, string assertion, string setup = "", Func<FramePointer, FrameValues> read = null)
        {
            var script = RScriptWriter.Write(Record(run), null, read);
            Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), label + " records and replays unchanged selected data");
            string output = RunR(setup + "\n" + script.Text + "\n" + assertion + "\ncat('DEFERRED_INDEPENDENT_538_OK\\n')\n", Path.Combine(folder, label));
            Check(output.Contains("DEFERRED_INDEPENDENT_538_OK"), label + " reproduces native results in R");
            return output;
        }

        // N1: the report deliberately clamps f1/f2 to one and rounds the unseen
        // component before adding observed richness. Test the half-integer case.
        foreach (double[] counts in new[] { new double[] { 3, 5, 4, 6, 1, 3 }, new double[] { 3, 5, 4, 6, 3 }, new double[] { 3, 5, 2, 4, 6, 3 }, new double[] { 3, 5, 2, 4, 6, 1, 3 }, new double[] { 1, 1, 1, 1, 2, 3, 4 } })
        {
            var (run, output) = Run("Diversity", new() { ["data"] = Frame(("Observed classes", counts)), ["gamma"] = .95, ["boots"] = 120, ["seed"] = 2001 });
            var b = ((IList<ParameterBag>)output.ParameterBag["*var"].AsObject)[0];
            string shown = Replay("chao-" + string.Join("-", counts), run,
                Equal("S+round(extra),se,round(S+extra/K),round(S+extra*K)", new[] { Number(b,"stotal"),Number(b,"se-largeSample"),Number(b,"from-largeSample"),Number(b,"to-largeSample") }) +
                "stopifnot(all(is.finite(c(extra,se,K))))\n");
            Match estimate = Regex.Match(shown,@"Estimated total number of classes = ([0-9]+)"), interval = Regex.Match(shown,@"Log-normal \(Chao\) 95% CI = ([0-9]+) to ([0-9]+)");
            Check(estimate.Success && interval.Success && double.Parse(estimate.Groups[1].Value,inv)==Number(b,"stotal") && double.Parse(interval.Groups[1].Value,inv)==Number(b,"from-largeSample") && double.Parse(interval.Groups[2].Value,inv)==Number(b,"to-largeSample"),"Chao printed integer estimate and interval equal the native report");
        }

        // M3: actual per-column acquisition, including repeated titles. The last
        // column must not accidentally repeat the first via a named-list lookup.
        foreach (int columns in new[] { 1, 3, 200 })
        foreach (string operation in new[] { "PoissonConfidenceInterval", "Diversity", "Normality" })
        {
            object[][] cells = Enumerable.Range(0, columns).Select(c =>
            {
                IEnumerable<double> values = operation == "Normality"
                    ? Enumerable.Range(1, 12 + c % 14).Select(i => 8d + .13 * i * i + Math.Sin(i + c))
                    : new[] { 1d, 2, 3 + c % 4, 5, 4 + c % 3, 2, 1, 3 };
                return new object[] { c % 2 == 0 ? "Shared title" : "Different title" }.Concat(values.Take(3).Cast<object>()).Append("*").Concat(values.Skip(3).Cast<object>()).ToArray();
            }).ToArray();
            Multiple(operation, cells, columns + "-columns");
        }
        // Each native normality row has its own supported tests. A constant,
        // short or >5000-value sample must not prevent later valid samples.
        var normalityCases = new List<object[]>();
        foreach (int count in new[] { 1, 2, 3, 5, 7, 8, 11, 12, 30, 5001, 18 })
            normalityCases.Add(new object[] { "Sample " + count }.Concat(Enumerable.Range(1, count).Select(i => (object)(5 + Math.Sin(i * .7) + i * .013))).ToArray());
        normalityCases.Insert(1, new object[] { "Constant", 4d, 4d, 4d, 4d });
        normalityCases.Insert(normalityCases.Count - 1, new object[] { "Two point values" }.Concat(Enumerable.Range(0,40).Select(i=>(object)(double)(i%2))).ToArray());
        foreach(double scale in new[]{1e-150,1e150}) normalityCases.Insert(normalityCases.Count - 1,new object[]{"Moment scale " + scale}.Concat(Enumerable.Range(1,12).Select(i=>(object)(scale*(2+i*.15+Math.Sin(i))))).ToArray());
        Multiple("Normality", normalityCases.ToArray(), "supported-domains");
        Multiple("PoissonConfidenceInterval", new[] { new object[] { "Invalid negative", 3d, -1d, 2d }, new object[] { "Fractional counts", 1.5, 2d, 3d, "*" }, new object[] { "No observed counts", "*" }, new object[] { "Later valid sample", 1d, 2d, 4d } }, "invalid-then-valid");
        Multiple("Diversity",new[]{new object[]{"Equal classes"}.Concat(Enumerable.Repeat((object)3d,6)).ToArray(),new object[]{"Equal classes"}.Concat(Enumerable.Repeat((object)3d,7)).ToArray(),new object[]{"Unequal classes",1d,2d,3d,4d,5d}},"equal-class-variances");
        var (blankTitles, blankTitlesOutput) = Run("PoissonConfidenceInterval", new() { ["data"] = Frame(("Same",new[]{1d,2,3}),("Same",new[]{2d,3,4}),("",new[]{3d,4,5})), ["gamma"] = .95 });
        var blankRows=(IList<ParameterBag>)blankTitlesOutput.ParameterBag["*sample"].AsObject;
        Replay("multiple-blank-titles", blankTitles,
            "stopifnot(identical(names(samples),c('Same','Same','')))\n"+Equal("printed_means",blankRows.Select(b=>Number(b,"mean"))),
            "printed_means<-numeric()\ncat<-function(...) {parts<-list(...);if(length(parts)>=4L&&identical(parts[[1]],'Size ='))printed_means<<-c(printed_means,as.numeric(parts[[4]]));base::cat(...)}\n");
        EmbeddedRepeatTitles();
        var (lowPoisson, lowPoissonOutput) = Run("PoissonConfidenceInterval",new(){["data"]=Frame(("Count sample",new[]{0d,1,2,4})),["gamma"]=.4});
        var lowPoissonBag=((IList<ParameterBag>)lowPoissonOutput.ParameterBag["*sample"].AsObject)[0];
        Replay("poisson-one-sided-unavailable",lowPoisson,Equal("r$conf.int,ci",new[]{Number(lowPoissonBag,"lower2"),Number(lowPoissonBag,"upper2"),Number(lowPoissonBag,"lower1"),Number(lowPoissonBag,"upper1")}));
        foreach (double confidence in new[] { 0d,1d,.9 })
        {
            double used=confidence>0&&confidence<1?confidence:.95;
            var (pr,po)=Run("PoissonConfidenceInterval",new(){["data"]=Frame(("Count sample",new[]{0d,1,2,4})),["gamma"]=confidence});
            var pb=((IList<ParameterBag>)po.ParameterBag["*sample"].AsObject)[0];
            Replay("poisson-confidence-"+confidence,pr,Equal("confidence,r$conf.int,ci",new[]{used,Number(pb,"lower2"),Number(pb,"upper2"),Number(pb,"lower1"),Number(pb,"upper1")}));
            foreach(int classes in new[]{6,7})
            {
                var (dr,doutput)=Run("Diversity",new(){["data"]=Frame(("Equal classes",Enumerable.Repeat(3d,classes).ToArray())),["gamma"]=confidence,["boots"]=120,["seed"]=2001});
                var db=((IList<ParameterBag>)doutput.ParameterBag["*var"].AsObject)[0];
                Replay("diversity-equal-"+classes+"-confidence-"+confidence,dr,
                    Equal("confidence,seL,seS,hL,hS,Ds-z*seL,Ds+z*seL,H-z*hL,H+z*hL",new[]{used,Number(db,"se-simpson-largeSample"),Number(db,"ses-simpson"),Number(db,"se-shannon-largeSample"),Number(db,"ses-shannon"),Number(db,"from-simpson-largeSample"),Number(db,"to-simpson-largeSample"),Number(db,"from-shannon-largeSample"),Number(db,"to-shannon-largeSample")},2e-7)+"stopifnot(all(is.finite(c(seL,seS,hL,hS))))\n");
            }
        }

        void Multiple(string operation, object[][] cells, string label)
        {
            var grid = new RecordCells(cells);
            DataFrame data = new();
            var mode = operation == "Normality" ? DataAcquisitionMode.NumericSkipMissing : DataAcquisitionMode.NumericReplaceMissing;
            for (int i = 0; i < cells.Length; i++)
                foreach (var variable in grid.Acquire(i, 0, cells[i].Length, mode).Variables) data.Variables.Add(variable);
            var (run, native) = RunMultiple(operation, data);
            CheckMultiple(operation, label, RScriptWriter.Write(Record(run), null, grid.Read), native, cells.Length);
        }

        (OperationRun run, ParameterBag[] native) RunMultiple(string operation, DataFrame data)
        {
            var answers = new Dictionary<string, object> { ["data"] = data };
            if (operation != "Normality") answers["gamma"] = .95;
            if (operation == "Diversity") { answers["boots"] = 120; answers["seed"] = 2001; }
            var (run, output) = Run(operation, answers);
            string block = operation == "Normality" ? "*variable" : operation == "Diversity" ? "*var" : "*sample";
            return (run, ((IList<ParameterBag>)output.ParameterBag[block].AsObject).ToArray());
        }

        void EmbeddedRepeatTitles()
        {
            string[] titles = { "Same", "Same", "Same_2", "", "" };
            string[] heldKeys = { "Same", "Same_2", "Same_2_2", "", "_2" };
            foreach (string operation in new[] { "PoissonConfidenceInterval", "Diversity", "Normality" })
            {
                var data = SheetFrame(Workbook, "Original column titles", 0, 0, titles.Select((title, index) =>
                    (title, Enumerable.Range(1, 12 + index).Select(value => (double)(value * (index + 1) + value % 3)).ToArray())).ToArray());
                var (run, native) = RunMultiple(operation, data);
                JsonElement record = Record(run);
                JsonElement input = AcquisitionInput(record, "data");
                Check(input.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("title").GetString()).SequenceEqual(titles)
                    && record.GetProperty("data").GetProperty("data").EnumerateObject().Select(c => c.Name).SequenceEqual(heldKeys),
                    operation + " records original worksheet titles separately from unique held keys");
                CheckMultiple(operation, "held-titles", RScriptWriter.Write(record, null, null), native, titles.Length, titles);

                JsonObject reread = JsonNode.Parse(record.GetRawText()).AsObject();
                reread.Remove("data");
                bool readCalled = false;
                FrameValues Read(FramePointer pointer)
                {
                    readCalled = true;
                    var values = new FrameValues();
                    for (int i = 0; i < pointer.Columns.Count; i++)
                        values.Columns.Add(new ColumnValues { Title = pointer.Columns[i].Title, Values = RunRecord.Values(data.Variables[i]) });
                    return values;
                }
                CheckMultiple(operation, "reread-titles", RScriptWriter.Write(JsonSerializer.SerializeToElement(reread), null, Read), native, titles.Length, titles);
                Check(readCalled, operation + " replays the same original titles and values from worksheet pointers");

                if (operation != "PoissonConfidenceInterval") continue;
                JsonObject legacy = JsonNode.Parse(record.GetRawText()).AsObject();
                JsonArray columns = legacy["inputs"].AsArray().Single(i => i["name"].GetValue<string>() == "data")["columns"].AsArray();
                columns[1].AsObject().Remove("title");
                string[] legacyTitles = (string[])titles.Clone();
                legacyTitles[1] = heldKeys[1];
                CheckMultiple(operation, "missing-recorded-title", RScriptWriter.Write(JsonSerializer.SerializeToElement(legacy), null, null), native, titles.Length, legacyTitles);
                columns.RemoveAt(columns.Count - 1);
                CheckMultiple(operation, "unmatched-recorded-titles", RScriptWriter.Write(JsonSerializer.SerializeToElement(legacy), null, null), native, titles.Length, heldKeys);
            }
        }

        void CheckMultiple(string operation, string label, RScript script, ParameterBag[] native, int columns, string[] titles = null)
        {
            Check(native.Length == columns, operation + " " + label + " has one native output per selected column");
            Check(!script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), operation + " " + label + " retains every column and hash");
            bool multiple = columns > 1;
            Check(script.Text.Contains("samples <- list(") == multiple && script.Text.Contains("for (sample_number in seq_along(samples))") == multiple,
                operation + " " + label + " adds a loop only for multiple columns");
            string capture = operation == "PoissonConfidenceInterval" ? "captured[[sample_number]]<-if(is.null(r)) c(n,NA,NA,NA,NA,NA) else c(n,mean(counts),r$conf.int,ci)\n" :
                operation == "Diversity" ? "captured[[sample_number]]<-c(N,S,S+round(extra),se,Ds,H,seL,seS,hL,hS)\n" :
                "valid<-n>=2&&is.finite(sd(yield))&&sd(yield)>0\ncaptured[[sample_number]]<-c(n,mean(yield),sd(yield),if(valid)skew else NA,if(valid)kurt else NA,if(valid&&!is.null(sw))unname(sw$statistic) else NA,if(valid&&!is.null(sw))sw$p.value else NA,if(valid&&n>=5)Wf else NA,if(valid&&n>=5)psf else NA,if(valid&&n>=8)pk else NA)\n";
            string code = script.Text;
            if (multiple) { int end = code.LastIndexOf("\n}", StringComparison.Ordinal); Check(end >= 0, "multi-column script has its expected analysis loop"); code = code.Insert(end, "\n" + capture); }
            else code += "\nsample_number<-1L\n" + capture;
            var assertions = new StringBuilder("stopifnot(length(captured)==" + native.Length + "L)\n");
            if (titles != null)
            {
                string expected = RScriptWriter.Vector(titles.Cast<object>().ToArray());
                assertions.Append("stopifnot(identical(names(samples), ").Append(expected)
                    .Append("), identical(headings, ").Append(expected).Append("))\n");
            }
            for (int i = 0; i < native.Length; i++)
            {
                var b = native[i];
                double[] values;
                if (operation == "PoissonConfidenceInterval") values = new[] { "n", "mean", "lower2", "upper2", "lower1", "upper1" }.Select(k => Number(b,k)).ToArray();
                else if (operation == "Diversity") values = new[] { "n", "s", "stotal", "se-largeSample", "simpson", "shannon", "se-simpson-largeSample", "ses-simpson", "se-shannon-largeSample", "ses-shannon" }.Select(k => Number(b,k)).ToArray();
                else
                {
                    int n = (int)Number(b,"n"); bool variation = Number(b,"sd") > 0;
                    double Optional(string key, bool supported) => supported && b.ContainsKey(key) ? Number(b,key) : double.NaN;
                    values = new[] { Number(b,"n"),Number(b,"mean"),Number(b,"sd"),Optional("skewness",variation),Optional("kurtosis",variation),Optional("sw_w",variation&&n>=3&&n<=5000),Optional("sw_p",variation&&n>=3&&n<=5000),Optional("sf_w",variation&&n>=5),Optional("sf_p",variation&&n>=5),Optional("k2_p",variation&&n>=8) };
                }
                assertions.Append(Equal("captured[[" + (i+1) + "]]", values, 2e-7));
            }
            assertions.Append("cat('MULTIPLE_COLUMNS_OK\\n')\n");
            string setup = "captured<-list()\nplot_count<-0L\nplot<-function(...) {plot_count<<-plot_count+1L;invisible(NULL)}\nabline<-function(...)invisible(NULL)\n";
            if (titles != null)
                setup += """
                    headings <- character()
                    cat <- function(..., sep = " ") {
                      parts <- list(...)
                      if (length(parts) == 3L && identical(sep, "") &&
                          identical(parts[[1]], "\n") && identical(parts[[3]], "\n"))
                        headings <<- c(headings, parts[[2]])
                      base::cat(..., sep = sep)
                    }

                    """;
            string result = RunR(setup + code + "\n" + assertions, Path.Combine(folder, operation + "-" + label));
            Check(result.Contains("MULTIPLE_COLUMNS_OK"), operation + " " + label + " reproduces every native column including distinct and duplicate titles");
            if (label == "supported-domains") Check(result.Contains("3 to 5000") && result.Contains("Normality cannot be tested") && result.Contains("Sample 18"), "normality unavailable tests are explicit and a later valid sample still completes");
            if (operation == "Normality" && columns == 3)
            {
                string charts = Path.Combine(folder, "normality-three-svg-charts");
                Check(RunR("svg('normality-%03d.svg',width=6,height=6)\n" + script.Text + "\ndev.off()\ncat('THREE_NORMALITY_CHARTS_OK\\n')\n", charts).Contains("THREE_NORMALITY_CHARTS_OK") && Directory.GetFiles(charts,"normality-*.svg").Length == 3,
                    "multi-column normality creates one actual SVG chart per selected valid sample");
            }
        }

        double[] x = { 8,12,15,11,19,14,9,16,21,18,13,17 }, y = { 9,10,13,12,17,16,14,18,22,19,15,20 };
        foreach (object popsd in new object[] { null, 0d, 4d })
        foreach (double confidence in new[] { 0d,1d,.9 })
        {
            var (run, output) = Run("ZSingle", new() { ["data"] = Frame(("Selected measurements",x)), ["gamma"] = confidence, ["popmean"] = 12d, ["popsd"] = popsd });
            var b = output.ParameterBag;
            Replay("z-sd-" + (popsd?.ToString() ?? "unknown") + "-confidence-" + confidence, run,
                Equal("dev,mean(speed)-mu-z*se,mean(speed)-mu+z*se,2*pnorm(-abs(dev))", new[] { Number(b,"z"),Number(b,"from"),Number(b,"to"),Number(b,"p_2") }));
        }
        bool zeroRefused=false;
        try { Run("SimpleLinearRegression",new(){["y"]=Frame(("Outcome",x)),["x"]=Frame(("Predictor",y)),["gamma"]=0d}); }
        catch(Exception ex) when(ex.Message.Contains("GAMMA must be greater than zero",StringComparison.Ordinal)||ex.Message=="the operation refused: Calculation Error") { zeroRefused=true; }
        Check(zeroRefused,"native simple regression retains its nonpositive-confidence refusal");
        var (validLinear,validLinearOutput) = Run("SimpleLinearRegression",new(){["y"]=Frame(("Outcome",x)),["x"]=Frame(("Predictor",y)),["gamma"]=.9});
        string zeroCode=RScriptWriter.Write(Record(validLinear),null,null).Text.Replace("confidence <- 0.9\n","confidence <- 0\n",StringComparison.Ordinal);
        Check(zeroCode.Contains("confidence <- 0\n",StringComparison.Ordinal)&&RunR("error_message<-tryCatch({\n"+zeroCode+"\nNULL\n},error=function(e)conditionMessage(e))\nstopifnot(identical(error_message,'Confidence must be greater than zero.'))\ncat('ZERO_CONFIDENCE_REFUSED\\n')\n",Path.Combine(folder,"linear-zero-refusal")).Contains("ZERO_CONFIDENCE_REFUSED"),"R simple regression refuses nonpositive confidence instead of inventing a different native contract");
        foreach(double selected in new[]{0d,1d,.8})
        {
            double used=selected>0&&selected<1?selected:.95;
            var (follow,followOutput)=Run("PlotSeCi",new(){["reggamma"]=selected},validLinearOutput.ParameterBag);
            var chart=(ChartDefinition)followOutput.ParameterBag["chart"].AsObject;
            var options=(LinearRegressionAndMaybeSeCiOrPredictionIntervalOptions)chart.ChartOptions;
            Replay("linear-follow-separate-confidence-"+selected,follow,
                Equal("confidence,level,qt((1+level)/2,fit$df.residual)",new[]{.9,used,options.pert}),
                "plot<-lines<-legend<-function(...)invisible(NULL)\n");
        }
        foreach (double confidence in new[] { 0d,1d,1.1,.9 })
        {
            double used = confidence > 0 && confidence < 1 ? confidence : .95;
            string label = confidence.ToString("R",inv);
            var (paired,po) = Run("TPaired",new(){["data"]=Frame(("First",x),("Second",y)),["gamma"]=confidence,["doAgreement"]=false});
            Replay("paired-confidence-"+label,paired,Equal("confidence,r$conf.int,r$p.value",new[]{used,Number(po.ParameterBag,"from"),Number(po.ParameterBag,"to"),Number(po.ParameterBag,"tail_2")}));
            var (single,so)=Run("TSingle",new(){["data"]=Frame(("Observed",x)),["gamma"]=confidence,["population-mean"]=12d});
            Replay("single-confidence-"+label,single,Equal("confidence,r$conf.int-mu,r$p.value",new[]{used,Number(so.ParameterBag,"from"),Number(so.ParameterBag,"to"),Number(so.ParameterBag,"p_2")}));
            var (unpaired,uo)=Run("TUnpaired",new(){["data"]=Frame(("First",x),("Second",y)),["gamma"]=confidence});
            Replay("unpaired-confidence-"+label,unpaired,Equal("confidence,equal$conf.int,unequal$conf.int",new[]{used,Number(uo.ParameterBag,"from"),Number(uo.ParameterBag,"to"),Number(uo.ParameterBag,"from_unequal"),Number(uo.ParameterBag,"to_unequal")}));
            if (confidence > 0) {
                var (linear,lo)=Run("SimpleLinearRegression",new(){["y"]=Frame(("Outcome",x)),["x"]=Frame(("Predictor",y)),["gamma"]=confidence});
                ParameterBag lb=((IList<ParameterBag>)lo.ParameterBag["*se"].AsObject)[0];
                Replay("linear-confidence-"+label,linear,Equal("confidence,confint(fit,level=confidence)[2,]",new[]{used,Number(lb,"se_from"),Number(lb,"se_to")}));
                var (follow,followOutput)=Run("PlotSeCi",new(){["reggamma"]=confidence},lo.ParameterBag);
                var chart=(ChartDefinition)followOutput.ParameterBag["chart"].AsObject;
                var options=(LinearRegressionAndMaybeSeCiOrPredictionIntervalOptions)chart.ChartOptions;
                Replay("linear-follow-confidence-"+label,follow,
                    Equal("level,qt((1+level)/2,fit$df.residual)",new[]{used,options.pert}),
                    "plot<-lines<-legend<-function(...)invisible(NULL)\n");
            }
        }
        foreach (bool paired in new[] { false,true })
        foreach (double difference in new[] { -2d,0d,3d })
        foreach (double confidence in new[] { .9,1d })
        {
            DataFrame data=paired?Frame(("First",Enumerable.Range(1,8).Select(i=>i+difference).ToArray()),("Second",Enumerable.Range(1,8).Select(i=>(double)i).ToArray())):Frame(("Differences",Enumerable.Repeat(difference,8).ToArray()));
            var (run,output)=Run("TPaired",new(){["data"]=data,["gamma"]=confidence,["doAgreement"]=paired});
            var b=output.ParameterBag;
            string extra=Equal("n,mean(d),sd(d),r$parameter,r$conf.int",new[]{Number(b,"n"),Number(b,"mean"),Number(b,"sd"),Number(b,"df"),Number(b,"from"),Number(b,"to")})+
                (difference==0?"stopifnot(is.nan(r$statistic),is.nan(r$p.value))\n":Equal("r$statistic,r$p.value",new[]{Number(b,"t"),Number(b,"tail_2")}))+
                "stopifnot(is.na(pw$power),plot_count==0L)\n";
            Check(b["pwr"].AsObject.ToString().EndsWith("*",StringComparison.Ordinal),"native constant-difference power is not calculated");
            Replay("constant-paired-"+paired+"-"+difference+"-"+confidence,run,extra,"plot_count<-0L\nplot<-function(...) {plot_count<<-plot_count+1L;stop('Native constant differences have no agreement chart')}\n");
        }

        // Native LOESS itself calls R. Compare its returned fit dimensions and df,
        // then intercept the actual plotted replay bands at each selected level.
        foreach (double confidence in new[] { .8,.9,.99 })
        foreach (bool band in new[] { false,true })
        {
            double[] lx=Enumerable.Range(1,28).Select(i=>(double)i).ToArray(),ly=lx.Select(v=>10*Math.Sin(v/4)+v+(v%3)-1).ToArray();
            var (run,output)=Run("LOESS",new(){["outcome"]=Frame(("Measured Y",ly)),["predictors"]=Frame(("Chosen X",lx)),["model"]="measured.y~chosen.x",["title"]="Selected LOESS",["ylab"]="Response",["xlab"]="Predictor",["span"]=.8,["degree"]="2",["plotFits"]=true,["plotFitsAndCi"]=band,["ci"]=confidence,["saveFitsAndSe"]=false,["saveRScript"]=false});
            var b=output.ParameterBag;
            string assertions=Equal("m$n,m$enp,m$s,p$df",new[]{Number(b,"observations"),Number(b,"parameters"),Number(b,"residualse"),Number(b,"df")},1e-7)+
                "stopifnot(length(plotted_lines)=="+(band?3:1)+"L)\n";
            if(band) assertions+=Equal("critical",new[]{PDF.tfromp2(1-confidence,Number(b,"df"))},1e-7)+"stopifnot(isTRUE(all.equal(plotted_lines[[2]]$y,as.numeric(p$fit[i]-critical*p$se.fit[i]),check.attributes=FALSE)),isTRUE(all.equal(plotted_lines[[3]]$y,as.numeric(p$fit[i]+critical*p$se.fit[i]),check.attributes=FALSE)))\n";
            Replay("loess-band-"+confidence+"-"+band,run,assertions,"plotted_lines<-list()\nplot<-function(...)invisible(NULL)\nlines<-function(x,y,...) {plotted_lines[[length(plotted_lines)+1L]]<<-list(x=x,y=y)}\n");
        }
        {
            double[] lx=Enumerable.Range(1,60).Select(i=>i/5d).ToArray(),lz=lx.Select(v=>Math.Sin(v*.7)).ToArray(),ly=lx.Select((v,i)=>10+v+2*lz[i]+Math.Cos(i)).ToArray();
            var (run,output)=Run("LOESS",new(){["outcome"]=Frame(("Measured Y",ly)),["predictors"]=Frame(("Chosen X",lx),("Chosen Z",lz)),["model"]="measured.y~chosen.x+chosen.z",["title"]="Two-predictor LOESS",["span"]=.8,["degree"]="2",["saveFitsAndSe"]=false,["saveRScript"]=false});
            Check(!output.ParameterBag.ContainsKey("metafile1"),"native multi-predictor LOESS suppresses the one-dimensional chart");
            var b=output.ParameterBag;
            Replay("loess-two-predictors",run,
                Equal("m$n,m$enp,m$s,p$df",new[]{Number(b,"observations"),Number(b,"parameters"),Number(b,"residualse"),Number(b,"df")},1e-7),
                "plot<-function(...)stop('Native multi-predictor LOESS has no one-dimensional chart')\n");
        }
    }
}
