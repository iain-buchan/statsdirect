using StatsDirect.R;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static void CheckChartRecipeSimplicity()
    {
        string helper = File.ReadAllText(Path.Combine(Metadata("Recipes"), "recipes", "chart-common.R"));
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-chart-simple-" + Guid.NewGuid().ToString("N"));
        string checks = """
            svg('simple.svg')
            old <- par(c('mar','family','cex.main','cex.lab','cex.axis','font.main','xpd'))
            tick_labels <- list()
            trace('axis',where=as.environment('package:graphics'),print=FALSE,
              tracer=quote(tick_labels[[length(tick_labels)+1L]] <<- list(side=side,labels=labels,at=at)))
            chart <- list(title='Chosen title',xTitle='Positive X',yTitle='PEFR',showLegend=TRUE,
              x=list(scale=2L,minimum=1,maximum=100),
              y=list(minimum=200,maximum=400,interval=25,firstTick=200,grid=TRUE,marker=312.5))
            limits <- sd_chart_frame(c(2,90),c(210,390))
            stopifnot(par('xlog'),!par('ylog'),identical(limits$xlim,c(1,100)),identical(limits$ylim,c(200,400)),
              par('cex.main')*par('ps')==12,par('cex.lab')*par('ps')==10,par('cex.axis')*par('ps')==9,
              identical(tick_labels[[2L]]$labels,as.character(seq(200,400,25))))
            sd_chart_legend(c('First series','Second series'))
            sd_chart_finish()
            stopifnot(identical(par(names(old)),old))
            chart <- list()
            sd_chart_frame(c(200,250),c(0,1))
            sd_chart_axis(1L,at=c(200,225.5,250))
            stopifnot(identical(tail(tick_labels,1L)[[1L]]$labels,c('200','225.5','250')))
            sd_chart_finish()
            dev.off()
            cat('SIMPLER_CHARTS_OK\n')
            """;
        string output = RunBoxR(helper + "\n" + checks, folder);
        Check(output.Contains("SIMPLER_CHARTS_OK") && File.Exists(Path.Combine(folder,"simple.svg")),
            "standard R chart options preserve selected limits, logarithms, reference lines and fractional ticks, use 12/10/9 fonts and restore graphics state");
        RRecipes.Folder = Metadata("Recipes");
        foreach (double confidence in new[] { .8, .99 }) foreach (bool stratified in new[] { false, true })
        {
            JsonObject example = JsonNode.Parse(File.ReadAllText(Path.Combine(Metadata("Examples"), "LogRank.json"))).AsObject();
            example["settings"]["gamma"] = confidence;
            if (stratified)
                example["inputs"][3]["columns"] = JsonSerializer.SerializeToNode(new[] { new { title = "Stratum", values = Enumerable.Range(0,80).Select(i => i % 2 + 1).ToArray() } });
            using JsonDocument document = JsonDocument.Parse(example.ToJsonString());
            using JsonDocument record = JsonDocument.Parse(RecordOf(document.RootElement));
            string script = RScriptWriter.Write(record.RootElement, null, null).Text;
            string assertions = """
                expected_interval <- function(r) {
                  estimate <- (r$O[1L]/r$E[1L])/(r$O[2L]/r$E[2L])
                  half <- qnorm((1+confidence)/2)*sqrt(sum(1/r$E[1:2]))
                  paste0('Group 1 vs. Group 2 = ',six(estimate),' (',six(estimate*exp(-half)),' to ',six(estimate*exp(half)),')')
                }
                stopifnot(expected_interval(lr) %in% printed)
                if (!is.null(stratum)) stopifnot(expected_interval(total) %in% printed)
                cat('HR_CONFIDENCE_OK\n')
                """;
            string result = RunR("printed <- capture.output({\n" + script + "\n})\n" + assertions,
                Path.Combine(folder, "hazard-" + confidence.ToString("R",inv) + "-" + stratified));
            Check(result.Contains("HR_CONFIDENCE_OK"), "log-rank approximate hazard-ratio intervals use selected confidence " + confidence + ", stratified=" + stratified);
        }
    }
}
