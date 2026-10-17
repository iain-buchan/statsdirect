using System.Text.Json;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckRecipeSurvivalMethodSimplicity()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-survival-method-" + Guid.NewGuid().ToString("N"));
        double[] time = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        double[] events = { 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1 };
        double[] groups = { 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1 };
        foreach (double confidence in new[] { .8, .9, .99 })
        foreach (bool grouped in new[] { false, true })
        {
            var inputs = new List<object>
            {
                new { name = "times", columns = new[] { new { title = "Time", values = time } } },
                new { name = "deaths", columns = new[] { new { title = "Event", values = events } } }
            };
            if (grouped) inputs.Add(new { name = "groups", columns = new[] { new { title = "Group", values = groups } } });
            string example = JsonSerializer.Serialize(new { operation = "KaplanMeier", file = "kaplan-meier.R", inputs, settings = new { gamma = confidence } });
            string script = RScriptWriter.Write(JsonDocument.Parse(RecordOf(JsonDocument.Parse(example).RootElement)).RootElement, null, null).Text;
            string runFolder = Path.Combine(folder, "km-" + confidence.ToString(inv) + "-" + grouped);
            string output = RunR("options(device=function(...) svg(filename='curve-%03d.svg',...))\n" + script + """

                stopifnot(isTRUE(all.equal(fit$conf.int,confidence)),length(median_limits)==2L)
                expected <- summary(survival::survfit(survival::Surv(time,event)~g,
                  conf.type='plain',conf.int=confidence),rmean='individual')$table
                if(is.null(dim(expected))) expected<-matrix(expected,nrow=1L)
                stopifnot(isTRUE(all.equal(unname(means),unname(expected))))
                stopifnot(length(strata)==length(s$time),length(H)==length(s$time))
                stopifnot(show_tics,!show_markers)
                cat('KM_SELECTED_OK\n')
                """, runFolder);
            Check(output.Contains("KM_SELECTED_OK") && Directory.GetFiles(runFolder, "curve-*.svg").Length == 5,
                $"Kaplan-Meier {(grouped ? "groups" : "single group")} at {confidence:P0} retains selected limits and all five useful native diagnostic curves");
        }

        // A censored-only group has no median; undefined diagnostic transforms must not abort the script.
        string censored = "time<-1:8;event<-rep(0,8);group<-NULL;confidence<-.9\n"
            + File.ReadAllText(Path.Combine(Metadata("Recipes"), "recipes", "kaplan-meier.R"));
        string censoredOutput = RunR("options(device=function(...) svg(filename='curve-%03d.svg',...))\n" + censored
            + "\nstopifnot(is.na(med));cat('CENSORED_OK\\n')\n", Path.Combine(folder, "censored"));
        Check(censoredOutput.Contains("CENSORED_OK"), "a censored-only survival series keeps its available curves without inventing a median");

        foreach (bool tics in new[] { false, true })
        foreach (bool markers in new[] { false, true })
        {
            var (run, output) = Run("KaplanMeier", new()
            {
                ["gamma"] = .9, ["use-tics"] = tics, ["use-markers"] = markers, ["save"] = false,
                ["times"] = SheetFrame(Workbook, "Survival marker choices", 0, 0, ("Time", time)),
                ["deaths"] = SheetFrame(Workbook, "Survival marker choices", 1, 0, ("Event", events)),
                ["groups"] = null
            });
            var native = output.ParameterBag["*chart"].AsParameterBagList
                .Select(b => (KaplanMeierOptions)((ChartDefinition)b["chart"].AsObject).ChartOptions).ToArray();
            Check(native.Length == 5 && native.All(o => o.DrawTics == tics && o.UseMarkers == markers),
                "actual Kaplan-Meier run applies both selected marker checkboxes to all five native curves");
            JsonElement record = Record(run);
            string script = RScriptWriter.Write(record, null, null).Text;
            Check(script == RScriptWriter.Write(JsonDocument.Parse(record.GetRawText()).RootElement, null, null).Text,
                "Kaplan-Meier marker choices survive reopening the actual operation record");
            string runFolder = Path.Combine(folder, "km-markers-" + tics + "-" + markers);
            string result = RunR("""
                options(device=function(...) svg(filename='curve-%03d.svg',...))
                .km_page<-0L;.km_points<-list()
                trace('plot.new',where=asNamespace('graphics'),print=FALSE,
                  tracer=quote(.km_page<<-.km_page+1L))
                trace('points.default',where=asNamespace('graphics'),print=FALSE,tracer=quote({
                  xy<-xy.coords(x,y)
                  .km_points[[length(.km_points)+1L]]<<-list(page=.km_page,x=xy$x,y=xy$y,pch=list(...)$pch)
                }))
                """ + "\n" + script + "\n" + $$"""
                stopifnot(show_tics=={{(tics ? "TRUE" : "FALSE")}},show_markers=={{(markers ? "TRUE" : "FALSE")}})
                transforms<-list(list(x=s$time,y=s$surv),list(x=s$time,y=H),
                  list(x=log(s$time),y=log(H)),list(x=log(s$time),y=qnorm(s$surv)),
                  list(x=s$time,y=H/s$time))
                for(page in seq_along(transforms)) {
                  xy<-transforms[[page]];valid<-is.finite(xy$x)&is.finite(xy$y)
                  for(kind in c('censor','event')) {
                    expected<-valid & if(kind=='censor') s$n.censor>0&s$n.event==0 else s$n.event>0
                    enabled<-if(kind=='censor')show_tics else show_markers
                    pch<-if(kind=='censor')'|' else '16'
                    marks<-Filter(function(p)p$page==page&&as.character(p$pch)==pch,.km_points)
                    expected_count<-if(enabled)sum(expected)else 0L
                    stopifnot(sum(vapply(marks,function(p)length(p$x),integer(1)))==expected_count)
                    if(expected_count) {
                      stopifnot(isTRUE(all.equal(unlist(lapply(marks,`[[`,'x')),xy$x[expected],check.attributes=FALSE)),
                                isTRUE(all.equal(unlist(lapply(marks,`[[`,'y')),xy$y[expected],check.attributes=FALSE)))
                    }
                  }
                }
                stopifnot(length(.km_points)==sum(vapply(.km_points,function(p)
                  as.character(p$pch)%in%c('|','16'),logical(1))))
                cat('KM_NATIVE_MARKERS_OK\n')
                """, runFolder);
            Check(result.Contains("KM_NATIVE_MARKERS_OK") && Directory.GetFiles(runFolder, "curve-*.svg").Length == 5,
                $"Kaplan-Meier tics={tics}, markers={markers} draws only genuine selected observations at the five transformed positions");
        }

        double[] reference = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, double.NaN, 12 };
        double[] test = { 1.1, 2.6, 3.2, 5.3, 5.9, 8.2, 7.4, 10.9, 11.1, 14.3, 12, double.NaN };
        int index = 0;
        foreach (string method in new[] { "do.deming.analytical", "do.deming.jackknife", "do.deming.bootstrap" })
        foreach (double confidence in method.EndsWith("bootstrap", StringComparison.Ordinal) ? new[] { .9 } : new[] { .8, .99 })
        {
            double ratio = confidence == .8 ? 2 : .5;
            bool difference = method.EndsWith("jackknife", StringComparison.Ordinal);
            var (run, output) = Run("MethodComparisonRegression", new()
            {
                ["testData"] = SheetFrame(Workbook, "Method", 0, 0, ("Test", test)),
                ["referenceData"] = SheetFrame(Workbook, "Method", 1, 0, ("Reference", reference)),
                ["error-ratio"] = ratio, ["ci"] = confidence, ["seed"] = 127,
                ["methods"] = new Dictionary<string, bool> { [method] = true },
                ["desired-charts"] = new Dictionary<string, bool> { ["plot.bland.altman"] = difference },
                ["saveRScript"] = false
            });
            ParameterBag native = output.ParameterBag["*methodResults"].AsParameterBagList.Single();
            double[] expected = new[] { "slope", "seslope", "slopelci", "slopeuci", "intercept", "seintercept", "interceptlci", "interceptuci" }
                .Select(key => Convert.ToDouble(native[key].AsObject, inv)).ToArray();
            string script = RScriptWriter.Write(Record(run), null, null).Text;
            string assertions = "\nexpected<-c(" + string.Join(",", expected.Select(v => v.ToString("R", inv))) + ")\n"
                + "est<-mcr::getCoefficients(fits[[1]])\n"
                + "if(is.na(est['Slope','SE'])) est['Slope','SE']<-sd(fits[[1]]@B1)\n"
                + "if(is.na(est['Intercept','SE'])) est['Intercept','SE']<-sd(fits[[1]]@B0)\n"
                + "actual<-c(est['Slope',c('EST','SE','LCI','UCI')],est['Intercept',c('EST','SE','LCI','UCI')])\n"
                + "stopifnot(length(fits)==1L,nrow(fits[[1]]@data)==10L,isTRUE(all.equal(as.numeric(actual),expected,tolerance=1e-10)))\n"
                + "cat('SELECTED_METHOD_OK\\n')\n";
            string runFolder = Path.Combine(folder, "mcr-" + index++);
            string result = RunR("options(device=function(...) svg(filename='method-%03d.svg',...))\n" + script + assertions, runFolder);
            Check(result.Contains("SELECTED_METHOD_OK") && Directory.GetFiles(runFolder, "method-*.svg").Length == (difference ? 1 : 0),
                $"selected {method} uses ratio {ratio}, confidence {confidence:P0}, complete pairs and only the chosen charts, matching native coefficients and intervals");
            Check(!script.Contains("deming <- function") && !script.Contains("qt(0.975"),
                "method comparison uses its selected package fit without a conflicting manual default fit");
        }
    }
}
