using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckChartRecipesB()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-chart-b-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        bool passed = false;
        try
        {
            double[] x1 = { 3, 1, 2, 2, 4, 5 }, y1 = { 6, 8, 7, 9, 3, Constant.MISSING };
            double[] x2 = { 1, 3, 2 }, y2 = { 2, 5, 4 };
            DataFrame Frame(string title, double[] data) => SheetFrame(Workbook, "Chosen chart columns", 0, 0, (title, (double[])data.Clone()));
            foreach (string operation in new[] { "ScatterPlot", "LinePlot" })
            {
                ScatterXYOptions chosen = null;
                var (run, _) = Run(operation, new()
                {
                    ["n"] = 2,
                    ["Ynew"] = new Queue<object>(new object[] { Frame("Outcome one", y1), Frame("Outcome two", y2) }),
                    ["Xnew"] = new Queue<object>(new object[] { Frame("Time one", x1), Frame("Time two", x2) })
                }, amend: fillable =>
                {
                    chosen = (ScatterXYOptions)((ChartDefinition)fillable).ChartOptions;
                    ChooseChartB(chosen, operation + " selected title", "Measured time", "Observed outcome", new[] { "Chosen first", "Chosen second" });
                });
                JsonElement record = Record(run);
                Check(chosen.JoinMarkersWithLines == (operation == "LinePlot") && record.GetProperty("parameters").GetProperty("chartSettings").GetProperty("title").GetString() == chosen.Title,
                    operation + " captures the selected final title and its native default connecting-line option");
                RScript script = RScriptWriter.Write(record, null, null);
                Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("PImax") && !script.Text.Contains("Subject's Age"),
                    operation + " binds all selected series without the help illustration's data or axis titles");
                string expected = $$"""
                    stopifnot(length(xy_series) == 2L, identical(xs[[1L]], c(3,1,2,2,4,5)),
                              identical(xy_series[[1L]]$x, c(1,2,2,3,4)),
                              identical(xy_series[[1L]]$y, c(8,9,7,6,3)),
                              identical(xy_series[[2L]]$x, c(1,2,3)),
                              identical(xy_series[[2L]]$y, c(2,4,5)))
                    stopifnot(length(.b_points) == 2L, length(.b_lines) == {{(operation == "LinePlot" ? 2 : 0)}}L,
                              length(.b_legends) == 1L)
                    stopifnot(.b_legends[[1L]]$result$rect$left+.b_legends[[1L]]$result$rect$w < .b_legends[[1L]]$usr[1L])
                    stopifnot(identical(as.double(.b_points[[1L]]$x), c(1,2,2,3,4)),
                              identical(as.double(.b_points[[1L]]$y), c(8,9,7,6,3)),
                              identical(as.integer(col2rgb(.b_points[[1L]]$details$col)), c(255L,0L,0L)))
                    if (length(.b_lines)) stopifnot(identical(as.double(.b_lines[[1L]]$x), c(1,2,2,3,4)),
                              identical(as.double(.b_lines[[1L]]$y), c(8,9,7,6,3)),
                              identical(as.integer(col2rgb(.b_lines[[1L]]$details$col)), c(0L,0L,255L)),
                              as.integer(.b_lines[[1L]]$details$lty) == 2L)
                    """;
                CheckChartBR(script.Text, Path.Combine(folder, operation), chosen, expected, operation + " selected series, styles and sorted native line path");
                CheckChartBR(script.Text, Path.Combine(folder, operation + "-pdf"), chosen, expected, operation + " opens the default Mac PDF device", pdf: true, lazyDevice: true);
                Check(RScriptWriter.Write(JsonDocument.Parse(record.GetRawText()).RootElement, null, null).Text == script.Text,
                    operation + " preserves its selected chart when its saved run is reopened");

                if (operation == "ScatterPlot") foreach (int shape in new[] { 6, 7, 8 })
                {
                    JsonNode compound = JsonNode.Parse(record.GetRawText());
                    JsonNode settings = compound["parameters"]["chartSettings"];
                    settings["showLegend"] = false;
                    settings["joinMarkersWithLines"] = true;
                    settings["markers"][0]["shape"] = shape;
                    settings["markers"][1]["shape"] = shape;
                    settings["markers"][0]["dashStyle"] = 4;
                    string compoundScript = RScriptWriter.Write(JsonDocument.Parse(compound.ToJsonString()).RootElement, null, null).Text;
                    string compoundChecks = $$"""
                        stopifnot(length(.b_points)==2L,length(.b_lines)==2L,length(.b_legends)==0L,
                                  identical(.b_lines[[1L]]$details$lty,"311111"))
                        overlay <- do.call(rbind,lapply(.b_segments,function(z)data.frame(x0=z$x0,y0=z$y0,x1=z$x1,y1=z$y1)))
                        stopifnot(nrow(overlay)=={{(shape == 8 ? 16 : 8)}}L)
                        {{(shape == 6 ? "stopifnot(all(overlay$x0==overlay$x1),all(overlay$y0!=overlay$y1))" : shape == 7 ? "stopifnot(all(overlay$x0<overlay$x1),all(overlay$y0>overlay$y1))" : "stopifnot(all(overlay$x0<overlay$x1),sum(overlay$y0>overlay$y1)==8L,sum(overlay$y0<overlay$y1)==8L)")}}
                        stopifnot(any(vapply(.b_segments,function(z)all(col2rgb(z$col)==255L),logical(1))))
                        """;
                    CheckChartBR(compoundScript, Path.Combine(folder, "compound-marker-" + shape), chosen, compoundChecks,
                        "compound marker " + shape + " preserves the native internal strokes and dash-dot-dot joining line");
                    settings["showLegend"] = true;
                    settings["useColour"] = false;
                    settings["legendFontSize"] = 15;
                    settings["markers"][1]["filled"] = false;
                    settings["x"] = JsonNode.Parse("{\"scale\":2,\"minimum\":0.5,\"maximum\":8}");
                    settings["y"] = JsonNode.Parse("{\"scale\":2,\"minimum\":1,\"maximum\":16}");
                    string legendScript = TraceChartBLegendState(RScriptWriter.Write(JsonDocument.Parse(compound.ToJsonString()).RootElement, null, null).Text);
                    string legendChecks = $$"""
                        stopifnot(length(.b_legend_points)==2L,length(.b_legend_segments)=={{(shape == 8 ? 4 : 2)}}L,
                                  .b_legend_before$xlog,.b_legend_before$ylog,
                                  identical(.b_legend_before,.b_legend_after),!isTRUE(sd_chart_state$drawing_legend))
                        stopifnot(all(vapply(.b_legend_points,function(z)all(z$npcX<0 & z$npcY>0 & z$npcY<1),logical(1))),
                                  all(vapply(.b_legend_segments,function(z)all(z$npcX0<0 & z$npcX1<0 & z$npcY0>0 & z$npcY1<1),logical(1))))
                        stopifnot(.b_legend_points[[1L]]$details$pch=={{(shape == 6 ? 16 : 15)}}L,
                                  .b_legend_points[[2L]]$details$pch=={{(shape == 6 ? 1 : 0)}}L,
                                  all(col2rgb(.b_legend_segments[[1L]]$col)==255L),
                                  all(col2rgb(.b_legend_segments[[length(.b_legend_segments)]]$col)==0L))
                        key <- .b_legend_segments[[1L]]
                        {{(shape == 6 ? "stopifnot(key$x0==key$x1,key$y0!=key$y1)" : "stopifnot(key$x0<key$x1,key$y0>key$y1)")}}
                        """;
                    foreach (bool pdf in new[] { false, true })
                        CheckChartBR(legendScript, Path.Combine(folder, "compound-legend-" + shape + (pdf ? "-pdf" : "")), chosen, legendChecks,
                            "compound marker " + shape + " has distinct filled and hollow legend keys outside logarithmic axes without leaking graphics state", pdf);
                }

                // A saved line chart can be changed to markers only, with empty titles and no legend.
                JsonNode edited = JsonNode.Parse(record.GetRawText());
                edited["parameters"]["chartSettings"]["joinMarkersWithLines"] = false;
                edited["parameters"]["chartSettings"]["showLegend"] = false;
                edited["parameters"]["chartSettings"]["title"] = "";
                edited["parameters"]["chartSettings"]["xTitle"] = "";
                edited["parameters"]["chartSettings"]["yTitle"] = "";
                chosen.Title = chosen.XAxisTitle = chosen.YAxisTitle = "";
                string changed = RScriptWriter.Write(JsonDocument.Parse(edited.ToJsonString()).RootElement, null, null).Text;
                CheckChartBR(changed, Path.Combine(folder, operation + "-empty"), chosen,
                    "stopifnot(!xy_join, length(.b_lines) == 0L, length(.b_legends) == 0L, length(.b_points) == 2L)\n",
                    operation + " respects saved empty titles and changed line/legend settings");
                GenericOptions legacy = (GenericOptions)chosen.Clone();
                legacy.Title = operation == "LinePlot" ? "Line plot" : "Scatter plot";
                legacy.XAxisTitle = "Time one";
                legacy.YAxisTitle = "Outcome one";
                CheckChartBR(ChartBLegacy(run), Path.Combine(folder, operation + "-legacy"), legacy,
                    "stopifnot(length(xy_series)==2L,length(.b_points)==2L,length(.b_legends)==0L,sum(grepl('no chart settings',.b_messages))==1L)\n",
                    operation + " reopens an older run with selected data and one explicit default-options notice");
            }

            LadderOptions ladder = null;
            double[] before = { 12, 7, Constant.MISSING, 20 }, after = { 10, 8, 9, 16 };
            var (ladderRun, _) = Run("LadderPlot", new() { ["data"] = SheetFrame(Workbook, "Chosen pairs", 3, 0, ("First visit", before), ("Follow-up", after)) }, amend: fillable =>
            {
                ladder = (LadderOptions)((ChartDefinition)fillable).ChartOptions;
                ChooseChartB(ladder, "Chosen paired changes", "", "Chosen measurement", new[] { "Baseline label", "Follow-up label" });
            });
            string ladderScript = RScriptWriter.Write(Record(ladderRun), null, null).Text;
            const string ladderChecks = """
                stopifnot(identical(ladder_before,c(12,7,20)), identical(ladder_after,c(10,8,16)),
                          identical(ladder_difference,c(2,-1,4)), length(.b_points) == 2L,
                          length(.b_segments) == 1L, length(.b_legends) == 0L)
                stopifnot(identical(as.double(.b_segments[[1L]]$y0), c(12,7,20)),
                          identical(as.double(.b_segments[[1L]]$y1), c(10,8,16)),
                          identical(.b_segments[[1L]]$col, "black"),
                          any(vapply(.b_axes,function(z) z$side == 1 && identical(as.character(z$labels), c("Baseline label","Follow-up label")),logical(1))))
                """;
            CheckChartBR(ladderScript, Path.Combine(folder, "ladder"), ladder, ladderChecks, "ladder selected pairs, edited category labels and native black rung lines");
            CheckChartBR(ladderScript, Path.Combine(folder, "ladder-pdf"), ladder, ladderChecks, "ladder chart opens the default Mac PDF device", pdf: true, lazyDevice: true);
            GenericOptions legacyLadder = (GenericOptions)ladder.Clone();
            legacyLadder.Title = "Ladder plot";
            legacyLadder.XAxisTitle = legacyLadder.YAxisTitle = "";
            CheckChartBR(ChartBLegacy(ladderRun), Path.Combine(folder, "ladder-legacy"), legacyLadder,
                "stopifnot(identical(ladder_labels,c('First visit','Follow-up')),sum(grepl('no chart settings',.b_messages))==1L)\n",
                "an older ladder run uses its original selected column labels");

            ErrorBarOptions error = null;
            var (errorRun, _) = Run("ErrorPlot", new()
            {
                ["group-count"] = 2,
                ["ydatNew"] = new Queue<object>(new object[] { Frame("First centre", new[] { 6.0, 5, 9 }), Frame("Second centre", new[] { 7.0, 10, Constant.MISSING }) }),
                ["xdatNew"] = new Queue<object>(new object[] { Frame("First X", new[] { 3.0, 1, 2 }), Frame("Second X", new[] { 3.0, 4, 5 }) }),
                ["ydatuNew"] = new Queue<object>(new object[] { Frame("First upper", new[] { 8.0, 6, 11 }), Frame("Second upper", new[] { 9.0, 12, 13 }) }),
                ["ydatlNew"] = new Queue<object>(new object[] { Frame("First lower", new[] { 4.0, 4, 7 }), Frame("Second lower", new[] { 5.0, 8, 9 }) })
            }, amend: fillable =>
            {
                error = (ErrorBarOptions)((ChartDefinition)fillable).ChartOptions;
                ChooseChartB(error, "Selected error limits", "Actual time", "Actual estimates", new[] { "First interval", "Second interval" });
                error.PlotMarkers = true;
                error.JoinMarkersWithLines = true;
                error.ShouldCheckForOffsets = true;
            });
            Check(error.Series.Count == 2 && error.Series[0].Data.Select(p => p.X).SequenceEqual(new[] { 3.0, 1, 2 })
                  && error.Series[1].Data.Length == 2, "the native error chart uses both selected series and skips incomplete rows");
            string errorScript = RScriptWriter.Write(Record(errorRun), null, null).Text;
            const string errorChecks = """
                stopifnot(length(error_series)==2L, identical(error_series[[1L]]$x,c(3,1,2)),
                          identical(error_series[[1L]]$y,c(6,5,9)),
                          identical(error_series[[1L]]$lower,c(4,4,7)), identical(error_series[[1L]]$upper,c(8,6,11)),
                          identical(error_series[[2L]]$x,c(3,4)), identical(error_series[[2L]]$y,c(7,10)),
                          error_series[[2L]]$plot_x[1L] > 3,
                          length(.b_points)==2L, length(.b_lines)==2L, length(.b_segments)==6L, length(.b_legends)==1L)
                stopifnot(.b_legends[[1L]]$result$rect$top < .b_legends[[1L]]$usr[3L])
                stopifnot(identical(as.double(.b_points[[2L]]$x),error_series[[2L]]$plot_x),
                          identical(as.double(.b_lines[[1L]]$y),c(6,6,5,9)),
                          identical(as.double(.b_segments[[1L]]$y0),c(4,4,7)),
                          identical(as.double(.b_segments[[1L]]$y1),c(8,6,11)))
                """;
            CheckChartBR(errorScript, Path.Combine(folder, "error"), error, errorChecks, "error chart selected bounds, line row order, safe offsets and legend");
            CheckChartBR(errorScript, Path.Combine(folder, "error-pdf"), error, errorChecks, "error chart opens the default Mac PDF device", pdf: true, lazyDevice: true);
            JsonNode noOffsets = JsonNode.Parse(errorRun.Record);
            noOffsets["parameters"]["chartSettings"]["checkForOffsets"] = false;
            noOffsets["parameters"]["chartSettings"]["plotMarkers"] = false;
            noOffsets["parameters"]["chartSettings"]["joinMarkersWithLines"] = false;
            string limitsOnly = RScriptWriter.Write(JsonDocument.Parse(noOffsets.ToJsonString()).RootElement, null, null).Text;
            CheckChartBR(limitsOnly, Path.Combine(folder, "error-limits-only"), error,
                "stopifnot(identical(error_series[[2L]]$plot_x,c(3,4)),length(.b_points)==0L,length(.b_lines)==0L,length(.b_segments)==6L)\n",
                "error chart respects disabled offset, marker and joining-line options");
            GenericOptions legacyError = (GenericOptions)error.Clone();
            legacyError.Title = "Error bar plot";
            legacyError.XAxisTitle = "First X";
            legacyError.YAxisTitle = "First centre";
            CheckChartBR(ChartBLegacy(errorRun), Path.Combine(folder, "error-legacy"), legacyError,
                "stopifnot(length(error_series)==2L,length(.b_lines)==0L,length(.b_points)==2L,length(.b_legends)==0L,sum(grepl('no chart settings',.b_messages))==1L)\n",
                "an older error-bar run retains all its selected series with standard options");
            ErrorBarOptions logError = null;
            var (logErrorRun, _) = Run("ErrorPlot", new()
            {
                ["group-count"] = 2,
                ["ydatNew"] = new Queue<object>(new object[] { Frame("First log centre", new[] { 6.0, 9 }), Frame("Second log centre", new[] { 7.0, 10 }) }),
                ["xdatNew"] = new Queue<object>(new object[] { Frame("First log X", new[] { 10.0, 1e6 }), Frame("Second log X", new[] { 10.0, 1e6 }) }),
                ["ydatuNew"] = new Queue<object>(new object[] { Frame("First log upper", new[] { 8.0, 11 }), Frame("Second log upper", new[] { 9.0, 12 }) }),
                ["ydatlNew"] = new Queue<object>(new object[] { Frame("First log lower", new[] { 4.0, 7 }), Frame("Second log lower", new[] { 5.0, 8 }) })
            }, amend: fillable =>
            {
                logError = (ErrorBarOptions)((ChartDefinition)fillable).ChartOptions;
                ChooseChartB(logError, "Custom logarithmic error chart", "Widely separated X", "Selected bounds", new[] { "First", "Second" });
                logError.PlotMarkers = true;
                logError.ShouldCheckForOffsets = true;
                logError.ShowLegend = false;
            });
            foreach (int scale in new[] { 1, 2 })
            {
                // The native error dialog offers a linear X axis. Users can also
                // customize the exported R settings to a logarithmic axis.
                JsonNode logRecord = JsonNode.Parse(logErrorRun.Record);
                logRecord["parameters"]["chartSettings"]["x"] = JsonNode.Parse("{\"scale\":" + scale + ",\"minimum\":5,\"maximum\":2000000}");
                string logScript = RScriptWriter.Write(JsonDocument.Parse(logRecord.ToJsonString()).RootElement, null, null).Text;
                const string logChecks = """
                    stopifnot(length(.b_segments)==6L,all(is.finite(error_series[[2L]]$plot_x)))
                    shifted <- .b_segments[[4L]]$deviceX0-.b_segments[[1L]]$deviceX0
                    stopifnot(length(shifted)==2L,max(abs(shifted-2))<1e-6)
                    for (i in c(1L,4L)) for (j in c(i+1L,i+2L)) {
                      cap <- .b_segments[[j]]; centre <- .b_segments[[i]]$deviceX0
                      stopifnot(all(is.finite(c(cap$x0,cap$x1))),
                                max(abs(centre-cap$deviceX0-10))<1e-6,
                                max(abs(cap$deviceX1-centre-10))<1e-6)
                    }
                    """;
                foreach (bool pdf in new[] { false, true })
                    CheckChartBR(logScript, Path.Combine(folder, "error-custom-log-" + scale + (pdf ? "-pdf" : "")), logError, logChecks,
                        "customized logarithmic error chart keeps constant pixel offsets and cap widths at X10 and X1000000", pdf);
            }
            passed = true;
        }
        finally
        {
            if (passed && Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) Directory.Delete(folder, true);
            else if (!passed) Console.WriteLine("      chart group B failure artifacts: " + folder);
        }
    }

    private static string ChartBLegacy(OperationRun run)
    {
        JsonNode record = JsonNode.Parse(run.Record);
        record["parameters"].AsObject().Remove("chartSettings");
        string script = RScriptWriter.Write(JsonDocument.Parse(record.ToJsonString()).RootElement, null, null).Text;
        return ".b_messages <- character()\nwithCallingHandlers({\n" + script + "\n},message=function(m){.b_messages<<-c(.b_messages,conditionMessage(m));invokeRestart('muffleMessage')})\n";
    }

    private static string TraceChartBLegendState(string script) => System.Text.RegularExpressions.Regex.Replace(script,
        @"(?m)^(\s*)sd_chart_legend\(([^\r\n]*)\)",
        "$1{ .b_legend_before <- par(c('family','xpd','xlog','ylog','mai','cex','font')); .b_legend_result <- sd_chart_legend($2); .b_legend_after <- par(c('family','xpd','xlog','ylog','mai','cex','font')); if(length(.b_legends)) .b_legends[[length(.b_legends)]]$result <- .b_legend_result }");

    private static void ChooseChartB(GenericOptions options, string title, string xTitle, string yTitle, string[] labels)
    {
        options.Title = title;
        options.XAxisTitle = xTitle;
        options.YAxisTitle = yTitle;
        options.SeriesTitles = labels;
        options.UseColour = true;
        options.ShowLegend = true;
        options.TitleFontDescriptor = new FontDescriptor("Arial", 1, 14);
        options.AxisTitleFontDescriptor = new FontDescriptor("Arial", 0, 11);
        options.AxisLabelFontDescriptor = new FontDescriptor("Arial", 0, 10);
        options.MarkerTypes[0].MarkerShape = MarkerShape.Square;
        options.MarkerTypes[0].MarkerColor = ColorDescriptor.Red;
        options.MarkerTypes[0].LineColor = ColorDescriptor.Blue;
        options.MarkerTypes[0].Width = 2;
        options.MarkerTypes[0].MarkerSize = 9;
        options.MarkerTypes[0].LineDashStyle = DashStyleDescriptor.Dash;
        options.MarkerTypes[0].IsMarkerFilled = true;
        options.MarkerTypes[1].MarkerShape = MarkerShape.Triangle;
        options.MarkerTypes[1].MarkerColor = ColorDescriptor.Green;
        options.MarkerTypes[1].LineColor = ColorDescriptor.Magenta;
    }

    private static void CheckChartBR(string script, string folder, GenericOptions chosen, string assertions, string description, bool pdf = false, bool lazyDevice = false)
    {
        script = TraceChartBLegendState(script);
        string preamble = """
            .b_pages <- 0L
            .b_titles <- list()
            .b_axes <- list()
            .b_points <- list()
            .b_lines <- list()
            .b_segments <- list()
            .b_rectangles <- list()
            .b_ablines <- list()
            .b_legends <- list()
            .b_legend_points <- list()
            .b_legend_segments <- list()
            .b_legend_rectangles <- list()
            .b_helper_legend <- function() exists('sd_chart_state',envir=.GlobalEnv,inherits=FALSE) && isTRUE(sd_chart_state$drawing_legend)
            .b_any_legend <- .b_helper_legend
            trace('plot.new',where=asNamespace('graphics'),print=FALSE,tracer=quote(.GlobalEnv$.b_pages<-.GlobalEnv$.b_pages+1L))
            title_trace <- quote(.GlobalEnv$.b_titles[[length(.GlobalEnv$.b_titles)+1L]]<-list(main=main,xlab=xlab,ylab=ylab))
            trace('title',where=asNamespace('graphics'),print=FALSE,tracer=title_trace)
            trace('title',where=as.environment('package:graphics'),print=FALSE,tracer=title_trace)
            trace('axis',where=as.environment('package:graphics'),print=FALSE,tracer=quote(.GlobalEnv$.b_axes[[length(.GlobalEnv$.b_axes)+1L]]<-list(side=side,labels=labels,at=at)))
            trace('points.default',where=asNamespace('graphics'),print=FALSE,tracer=quote({
              if (!.b_any_legend()) .GlobalEnv$.b_points[[length(.GlobalEnv$.b_points)+1L]]<-list(x=x,y=y,details=list(...))
              else if(length(x) && length(y)) .GlobalEnv$.b_legend_points[[length(.GlobalEnv$.b_legend_points)+1L]]<-list(x=x,y=y,npcX=grconvertX(x,'user','npc'),npcY=grconvertY(y,'user','npc'),details=list(...))
            }))
            trace('lines.default',where=asNamespace('graphics'),print=FALSE,tracer=quote(if (!.b_any_legend()) .GlobalEnv$.b_lines[[length(.GlobalEnv$.b_lines)+1L]]<-list(x=x,y=y,details=list(...))))
            trace('segments',where=as.environment('package:graphics'),print=FALSE,tracer=quote({
              if (!.b_any_legend()) .GlobalEnv$.b_segments[[length(.GlobalEnv$.b_segments)+1L]]<-list(x0=x0,y0=y0,x1=x1,y1=y1,col=col,lwd=lwd,lty=lty,deviceX0=grconvertX(x0,'user','device'),deviceX1=grconvertX(x1,'user','device'),details=list(...))
              else .GlobalEnv$.b_legend_segments[[length(.GlobalEnv$.b_legend_segments)+1L]]<-list(x0=x0,y0=y0,x1=x1,y1=y1,col=col,lwd=lwd,lty=lty,npcX0=grconvertX(x0,'user','npc'),npcX1=grconvertX(x1,'user','npc'),npcY0=grconvertY(y0,'user','npc'),npcY1=grconvertY(y1,'user','npc'))
            }))
            trace('rect',where=as.environment('package:graphics'),print=FALSE,tracer=quote({
              if (!.b_any_legend()) .GlobalEnv$.b_rectangles[[length(.GlobalEnv$.b_rectangles)+1L]]<-list(left=xleft,bottom=ybottom,right=xright,top=ytop,col=col,border=border)
              else if (!(all(is.na(col)) && all(is.na(border)))) .GlobalEnv$.b_legend_rectangles[[length(.GlobalEnv$.b_legend_rectangles)+1L]]<-list(left=xleft,bottom=ybottom,right=xright,top=ytop,col=col,border=border,density=density,angle=angle,npcLeft=grconvertX(xleft,'user','npc'),npcRight=grconvertX(xright,'user','npc'),npcBottom=grconvertY(ybottom,'user','npc'),npcTop=grconvertY(ytop,'user','npc'))
            }))
            trace('abline',where=as.environment('package:graphics'),print=FALSE,tracer=quote(.GlobalEnv$.b_ablines[[length(.GlobalEnv$.b_ablines)+1L]]<-list(h=h,v=v,details=list(...))))
            trace('legend',where=as.environment('package:graphics'),print=FALSE,
                  tracer=quote(if(isTRUE(plot)) .GlobalEnv$.b_legends[[length(.GlobalEnv$.b_legends)+1L]]<-list(labels=legend,usr=par('usr'))))
            """;
        preamble += pdf ? lazyDevice ? "\noptions(device=function(...)pdf('chart.pdf',width=7,height=7))\n" : "\npdf('chart.pdf',width=7,height=7)\n" : "\nsvg('chart-%03d.svg')\n";
        string check = $$"""

            same_text <- function(x,y) identical(trimws(gsub("[[:space:]]+"," ",paste(x,collapse=" "))),trimws(gsub("[[:space:]]+"," ",paste(y,collapse=" "))))
            stopifnot(.b_pages==1L)
            stopifnot(all(vapply(.b_legends,function(z)length(z$result$rect$left)==1L && length(z$result$rect$top)==1L &&
                      all(is.finite(unlist(z$result$rect))),logical(1))))
            stopifnot(any(vapply(.b_titles,function(z) same_text(z$main,{{RScriptWriter.Quote(chosen.Title)}}),logical(1))),
                      any(vapply(.b_titles,function(z) same_text(z$xlab,{{RScriptWriter.Quote(chosen.XAxisTitle)}}),logical(1))),
                      any(vapply(.b_titles,function(z) same_text(z$ylab,{{RScriptWriter.Quote(chosen.YAxisTitle)}}),logical(1))))
            {{(string.IsNullOrEmpty(chosen.Title) ? "stopifnot(all(vapply(.b_titles,function(z) same_text(z$main,''),logical(1))))" : "")}}
            {{(string.IsNullOrEmpty(chosen.XAxisTitle) ? "stopifnot(all(vapply(.b_titles,function(z) same_text(z$xlab,''),logical(1))))" : "")}}
            {{(string.IsNullOrEmpty(chosen.YAxisTitle) ? "stopifnot(all(vapply(.b_titles,function(z) same_text(z$ylab,''),logical(1))))" : "")}}
            {{assertions}}
            dev.off()
            cat("CHART B VERIFIED\n")
            """;
        string output = RunBoxR(preamble + "\n" + script + "\n" + check, folder);
        bool single = pdf
            ? File.Exists(Path.Combine(folder, "chart.pdf")) && System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(folder, "chart.pdf"))), @"/Type\s*/Page\b").Count == 1
            : Directory.GetFiles(folder, "chart-*.svg").Length == 1;
        string artifacts = Environment.GetEnvironmentVariable("STATSDIRECT_CHART_ARTIFACTS");
        if (pdf && !string.IsNullOrEmpty(artifacts))
        {
            Directory.CreateDirectory(artifacts);
            File.Copy(Path.Combine(folder, "chart.pdf"), Path.Combine(artifacts, new DirectoryInfo(folder).Name + ".pdf"), true);
        }
        Check(output.Contains("CHART B VERIFIED") && single, description + ": one page with the selected data, titles and options");
    }
}
