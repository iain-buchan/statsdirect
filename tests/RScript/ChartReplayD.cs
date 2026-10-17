using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckChartReplayD()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-chart-d-" + Guid.NewGuid().ToString("N"));
        Console.WriteLine("      chart D artifacts: " + folder);
        DataFrame Frame(string title, params double[] values) => SheetFrame(Workbook, "Replay D selections", 0, 0, (title, values));
        void Common(GenericOptions options, string title, string x, string y, string[] labels)
        {
            ChooseChartB(options, title, x, y, labels);
            options.TitleFontDescriptor = new FontDescriptor("Arial", 1, 22);
            options.LegendFontDescriptor = new FontDescriptor("Arial", 0, 15);
        }
        void Replay(OperationRun run, GenericOptions chosen, string assertions, string name, bool legacy = false)
        {
            JsonElement record = Record(run);
            string script = legacy ? ChartBLegacy(run) : RScriptWriter.Write(record, null, null).Text;
            if (!legacy) Check(RScriptWriter.Write(JsonDocument.Parse(record.GetRawText()).RootElement, null, null).Text == script,
                name + " reopens with the same selected data and chart choices");
            foreach (bool pdf in new[] { false, true })
                CheckChartBR(script, Path.Combine(folder, name + (pdf ? "-pdf" : "-svg")), chosen, assertions,
                    name + " actual native run replay", pdf: pdf, lazyDevice: pdf);
        }
        foreach (bool limits in new[] { false, true })
        {
            SurvivalOptions selected = null;
            var (run, _) = Run("SurvivalPlot", new()
            {
                ["group-count"] = 3,
                ["xdatNew"] = new Queue<object>(Enumerable.Range(1,3).Select(i => (object)Frame("Time " + i, 1, 2, 4, 7))),
                ["cdatNew"] = new Queue<object>(Enumerable.Range(1,3).Select(i => (object)Frame("Event " + i, 1, 0, 1, 1))),
                ["ydatNew"] = new Queue<object>(Enumerable.Range(1,3).Select(i => (object)Frame("Chosen S " + i, .95, .95, .6 + .02*i, .2 + .03*i))),
                ["ydatlNew"] = new Queue<object>(Enumerable.Range(1,3).Select(i => limits ? (object)Frame("Chosen L " + i, .8, .8, .4, .1) : null)),
                ["ydatuNew"] = new Queue<object>(Enumerable.Range(1,3).Select(i => limits ? (object)Frame("Chosen U " + i, 1, 1, .8, .5) : null))
            }, amend: value =>
            {
                var d = (ChartDefinition)value; selected = (SurvivalOptions)d.ChartOptions;
                Common(selected, "Selected three survival curves", "Times", "Selected survival proportion", new[] { "Scotland", "Wales", "Northern Ireland" });
                selected.ShowCensorshipTics = true; selected.ShowEventMarkers = limits;
                selected.UseSeriesColourForConfidenceIntervals = true;
                d.ScaleParameters.X.AxisScale = new LinearAxisScale(1,7,0,8,4);
                d.ScaleParameters.Y.AxisScale = new LinearAxisScale(0,1,0,1,5);
            });
            string check = $$"""
                stopifnot(length(survival_curves)==3L,survival_ci=={{(limits ? "TRUE" : "FALSE")}},survival_censor_tics,
                          survival_event_markers=={{(limits ? "TRUE" : "FALSE")}},length(.b_points)=={{(limits ? 3 : 0)}}L,
                          length(.b_segments)=={{(limits ? 12 : 9)}}L,
                          max(abs(survival_curves[[3L]]$y-c(.95,.95,.66,.29)))<1e-14,
                          identical(survival_labels,c('Scotland','Wales','Northern Ireland')),
                          identical(.b_segments[[1L]]$x0,c(0,1,2,4)),max(abs(.b_segments[[1L]]$y0-c(1,.95,.95,.62)))<1e-14)
                {{(limits ? "stopifnot(identical(.b_segments[[4L]]$y0,c(.8,.4,.1)),identical(.b_segments[[4L]]$y1,c(1,.8,.5)))" : "")}}
                """;
            Replay(run, selected, check, "survival-" + limits);
            JsonNode withoutSnapshot = JsonNode.Parse(run.Record);
            withoutSnapshot["parameters"]["chartSettings"].AsObject().Remove("curves");
            string fallback = RScriptWriter.Write(JsonDocument.Parse(withoutSnapshot.ToJsonString()).RootElement, null, null).Text;
            CheckChartBR(fallback, Path.Combine(folder, "survival-inputs-" + limits), selected, check,
                "survival binds selected proportion/CI columns even without the embedded curve snapshot");
            if (!limits)
            {
                string script = ChartBLegacy(run);
                string output = RunR("options(device=function(...)svg('legacy.svg',...))\n" + script +
                    "\nstopifnot(length(survival_curves)==3L,max(abs(survival_curves[[3]]$y-c(.95,.95,.66,.29)))<1e-14,any(grepl('no chart settings',.b_messages)))\ncat('LEGACY_D_OK\\n')", Path.Combine(folder,"survival-legacy"));
                Check(output.Contains("LEGACY_D_OK"), "legacy survival records use their selected S columns and announce missing chart choices");
            }
        }
        {
            SurvivalOptions selected=null;ChartDefinition nativeChart=null;
            var (run, _)=Run("SurvivalPlot",new()
            {
                ["group-count"]=3,
                ["xdatNew"]=new Queue<object>(Enumerable.Range(0,3).Select(i=>(object)Frame("Time",1,2))),
                ["cdatNew"]=new Queue<object>(Enumerable.Range(0,3).Select(i=>(object)Frame("Event",1,1))),
                ["ydatNew"]=new Queue<object>(Enumerable.Range(0,3).Select(i=>(object)Frame("S",.9,.5))),
                ["ydatlNew"]=new Queue<object>(new object[]{Frame("Lower",.7,.2),null,Frame("Lower third",.6,.1)}),
                ["ydatuNew"]=new Queue<object>(new object[]{Frame("Upper",1,.8),Frame("Upper third",1,.9)})
            },amend:value=>
            {
                nativeChart=(ChartDefinition)value;selected=(SurvivalOptions)nativeChart.ChartOptions;
                Common(selected,"Selected partially supplied survival intervals","Times","Selected survival proportion",new[]{"First CI","No CI","Third CI"});
                selected.MarkerTypes[^1].LineColor=ColorDescriptor.FromArgb(12,34,56);
                nativeChart.ScaleParameters.X.AxisScale=new LinearAxisScale(1,2,0,3,3);
                nativeChart.ScaleParameters.Y.AxisScale=new LinearAxisScale(0,1,0,1,5);
            });
            Check(selected.Series[1].YDatL==null && selected.Series[1].YDatU==null && selected.Series[2].YDatL.SequenceEqual(new[]{.6,.1}),
                "native survival aligns optional confidence limits to their actual acquisition group");
            string check="""
                stopifnot(identical(survival_ci_groups,c(TRUE,FALSE,TRUE)),length(.b_segments)==8L,
                          identical(.b_segments[[3]]$y0,c(.7,.2)),identical(.b_segments[[8]]$y0,c(.6,.1)),
                          length(survival_curves[[2]]$lower)==0L,identical(survival_curves[[3]]$lower,c(.6,.1)))
                """;
            Replay(run,selected,check,"survival-mixed-CI");
            JsonNode noSnapshot=JsonNode.Parse(run.Record);noSnapshot["parameters"]["chartSettings"].AsObject().Remove("curves");
            CheckChartBR(RScriptWriter.Write(JsonDocument.Parse(noSnapshot.ToJsonString()).RootElement,null,null).Text,
                Path.Combine(folder,"survival-mixed-fallback"),selected,check,"mixed survival bounds use curveShapes when a large record omits curve snapshots");
            var (svg,_)=RenderNativeChartD(nativeChart.Clone());
            Check(svg.Descendants().Count(e=>e.Name.LocalName=="line" && e.Attribute("style")?.Value.Contains("#0C2238")==true)==4,
                "native survival SVG draws exactly the four supplied event confidence intervals for groups one and three");
        }
        foreach (bool log in new[] { false, true }) foreach (bool optional in new[] { false, true })
        {
            double[] effects = log ? new[] { .5, 1.2, 2.0 } : new[] { -2.0, -.5, .4 };
            double[] low = log ? new[] { .2, .8, 1.0 } : new[] { -3.0, -1, -.4 };
            double[] high = log ? new[] { .9, 2.0, 4.0 } : new[] { -1.0, .3, 1.2 };
            DataFrame text = new(); text.Variables.Add(new StringVariable(new[] { "Chosen North", "Chosen South", "Chosen pool" }, "Labels"));
            ForestOptions selected = null;
            var (run, _) = Run("CochranePlot", new()
            {
                ["odds"] = Frame(log ? "Selected ratio" : "Selected mean difference",effects), ["lci"] = Frame("Lower", low), ["uci"] = Frame("Upper", high),
                ["gn"] = optional ? Frame("Weights",10,40,100) : null, ["pg"] = optional ? Frame("Pools",0,0,-1) : null,
                ["title"] = text
            }, amend: value =>
            {
                var d=(ChartDefinition)value; selected=(ForestOptions)d.ChartOptions;
                Common(selected,"Selected forest effects", "Selected effect and confidence interval", "", null);
                selected.EffectSizeAndIntervalDecimalPlaces=3; selected.MarkCentres=false; selected.cco=.99;
                selected.Titles[0]="Edited North";
                d.ScaleParameters.X.ScaleType=log?ScaleType.Log10:ScaleType.Linear;
                d.ScaleParameters.X.AxisScale= log ? new Log10AxisScale(.2,4,-1,1,2,1,new[]{2,5}) : new LinearAxisScale(-3,1.2,-4,2,6);
            });
            string check=$$"""
                stopifnot(nrow(forest_rows)==3L,forest_log=={{(log ? "TRUE" : "FALSE")}},forest_decimals==3L,
                          identical(forest_rows$label,c('Edited North','Chosen South','Chosen pool')),
                          identical(forest_rows$effect,{{RScriptWriter.Vector(effects.Cast<object>().ToArray())}}),
                          identical(forest_rows$weight,{{(optional ? "c(10,40,100)" : "c(10,10,10)")}}),
                          identical(forest_rows$pooling,{{(optional ? "c(0,0,-1)" : "c(0,0,0)")}}),
                          length(.b_points)==3L,!sd_chart_get('markCentres'),chart$confidence==.99,
                          par('xlog')=={{(log ? "TRUE" : "FALSE")}},identical(forest_format(c(-.000012,.00002)),c('-1.2E-5','2.0E-5')))
                """;
            Replay(run, selected, check, "forest-"+log+"-"+optional);
            if(!log && !optional)
            {
                string output=RunR("options(device=function(...)svg('legacy.svg',...))\n"+ChartBLegacy(run)+
                    "\nstopifnot(!forest_log,identical(forest_rows$effect,c(-2,-.5,.4)),identical(forest_rows$label,c('Chosen North','Chosen South','Chosen pool')),any(grepl('no chart settings',.b_messages)))\ncat('LEGACY_D_OK\\n')",Path.Combine(folder,"forest-legacy"));
                Check(output.Contains("LEGACY_D_OK"),"legacy signed forest records retain their selected values/labels and announce default chart choices");
            }
        }
        foreach (Comparison comparison in new[] { Comparison.GreaterEqual, Comparison.GreaterThan, Comparison.LessEqual, Comparison.LessThan })
            foreach (double confidence in new[] { .95, .99 })
        {
            ROCOptions selected=null;
            ChartDefinition nativeChart=null;
            var (run, output)=Run("ROCPlot",new()
            {
                ["series-count"]=1,["P1"]=Frame("Present",1,2,2,4,5),["A1"]=Frame("Absent",2,3,3,6,7)
            },amend:value=>
            {
                if(value is ChartDefinition d)
                {
                    nativeChart=d;
                    selected=(ROCOptions)d.ChartOptions;
                    Common(selected,"Selected ROC curve", "1-Specificity", "Sensitivity",new[]{"Chosen population"});
                    selected.Comparison=comparison;selected.GAMMA=confidence;selected.Weight=2;
                    selected.ShowCutOffCalculator=false;selected.ShowOptimumCutOff=true;
                    selected.MarkerTypes[1].MarkerShape=MarkerShape.Circle;
                }
            });
            var native=((IList<ParameterBag>)output.ParameterBag["*datasets"].AsObject)[0];
            double expected=comparison is Comparison.LessEqual or Comparison.LessThan ? .72 : .28;
            // 18 of the 25 cross-group pairs put the present value below the absent value (ties count half).
            Check(Math.Abs(native["auc"].AsDouble-expected)<1e-12 && Math.Abs(native["theta"].AsDouble-expected)<1e-12,
                "native ROC area and rank estimate follow "+comparison+" with ties at "+confidence);
            if(confidence==.95) CheckNativeRocGeometry(nativeChart,comparison,native,Path.Combine(folder,"native-roc-"+(int)comparison+".svg"));
            string Native(string key)=>native[key].AsDouble.ToString("R",inv);
            string check=$$"""
                s<-roc_series[[1L]]
                stopifnot(length(roc_series)==1L,s$comparison=={{(int)comparison}}L,roc_confidence=={{confidence.ToString("R",inv)}},s$weight==2,
                          abs(s$auc-{{Native("auc")}})<1e-12,abs(s$theta-{{Native("theta")}})<1e-12,
                          abs(s$se-{{Native("se")}})<1e-12,max(abs(s$confidence-c({{Native("ll")}},{{Native("ul")}})))<1e-10,
                          identical(c(s$a,s$b,s$c,s$d),as.integer(c({{Native("a")}},{{Native("b")}},{{Native("c")}},{{Native("d")}}))),
                          s$cutoff=={{Native("cut")}},length(.b_points)==2L,length(.b_lines)==1L,
                          identical(as.numeric(.b_points[[2L]]$x),s$b/length(s$absent)),identical(as.numeric(.b_points[[2L]]$y),s$a/length(s$present)))
                """;
            Replay(run,selected,check,"roc-"+(int)comparison+"-"+confidence);
        }
        foreach (bool optimum in new[] { false,true })
        {
            ROCOptions selected=null;ChartDefinition nativeChart=null;int prompts=0;
            var (run,output)=Run("ROCPlot",new()
            {
                ["series-count"]=3,
                ["P1"]=new Queue<object>(Enumerable.Range(0,3).Select(i=>(object)Frame("Present "+i,1,2,3,4))),
                ["A1"]=new Queue<object>(Enumerable.Range(0,3).Select(i=>(object)Frame("Absent "+i,10,11,12,13)))
            },amend:value=>
            {
                if(value is ChartDefinition d)
                {
                    nativeChart=d;
                    selected=(ROCOptions)d.ChartOptions;
                    Common(selected,"Three selected ROC populations", "1-Specificity", "Sensitivity",new[]{"North","South","West"});
                    selected.Comparison=Comparison.LessEqual;selected.GAMMA=.9;selected.Weight=2;
                    selected.ShowCutOffCalculator=true;selected.ShowOptimumCutOff=optimum;
                }
                else if(value is ROCCutoff cutoff){prompts++;cutoff.SeriesRecord.cutoff=2.5;cutoff.SeriesRecord.ReCut();}
            });
            if(optimum)
            {
                var bag=((IList<ParameterBag>)output.ParameterBag["*datasets"].AsObject)[0];
                Check(bag["auc"].AsDouble==1 && bag["theta"].AsDouble==1 && bag["cut"].AsDouble==2.5 && bag["a"].AsInt32==2,
                    "native lower-positive perfect classifier retains its manually chosen cutoff");
                Check(prompts==3,"the results pass asks the ROC cutoff calculator once per selected population");
                string originalScript=RScriptWriter.Write(Record(run),null,null).Text;
                ChartDefinition redraw=nativeChart.Clone();redraw.ChartOptions.Title="Formatting-only redraw";
                ((ROCOptions)redraw.ChartOptions).GAMMA=.99;
                var (svg,redrawn)=RenderNativeChartD(redraw,new Host(new(),_=>throw new Exception("a redraw unexpectedly reopened the cutoff calculator")));
                var datasets=(IList<ParameterBag>)redrawn["*datasets"].AsObject;
                Check(datasets.Count==3 && datasets.All(b=>b["cut"].AsDouble==2.5 && b["a"].AsInt32==2 && Math.Abs(b["pc"].AsDouble-99)<1e-10)
                    && svg.Name.LocalName=="svg" && RScriptWriter.Write(Record(run),null,null).Text==originalScript,
                    "ROC results-to-SVG redraw and formatting/confidence changes retain the recorded manual cutoff without another prompt");
                foreach(string change in new[]{"data","comparison","weight","optimum","calculator"})
                {
                    ChartDefinition changed=nativeChart.Clone();var options=(ROCOptions)changed.ChartOptions;int requests=0;
                    if(change=="data")changed.AddXSeriesAt(new DoubleSeries(new[]{1.0,2,3,5},"Changed source"),0);
                    if(change=="comparison")options.Comparison=Comparison.GreaterEqual;
                    if(change=="weight")options.Weight=3;
                    if(change=="optimum")options.ShowOptimumCutOff=false;
                    if(change=="calculator")options.ShowCutOffCalculator=false;
                    Host host=new(new(),value=>{if(value is ROCCutoff cutoff){requests++;cutoff.SeriesRecord.cutoff=2.5;cutoff.SeriesRecord.ReCut();}});
                    var (_,updated)=RenderNativeChartD(changed,host);
                    if(change is "optimum" or "calculator")
                    {
                        options.ShowOptimumCutOff=true;options.ShowCutOffCalculator=true;
                        _=RenderNativeChartD(changed,host);
                    }
                    Check(requests==3,"ROC finalized-cutoff cache invalidates when "+change+" changes");
                }
            }
            string check=$$"""
                stopifnot(length(roc_series)==3L,all(vapply(roc_series,function(s)s$auc==1 && s$theta==1,logical(1))),
                          length(.b_points)=={{(optimum?6:3)}}L,length(.b_lines)==3L)
                {{(optimum?"stopifnot(all(vapply(roc_series,function(s)s$cutoff==2.5 && s$a==2 && s$b==0,logical(1))))":"")}}
                """;
            Replay(run,selected,check,"roc-three-"+optimum);
            if(optimum)
            {
                JsonNode legacy=JsonNode.Parse(run.Record);legacy["parameters"].AsObject().Remove("chartSettings");legacy["parameters"].AsObject().Remove("rocCutoffs");
                string script=RScriptWriter.Write(JsonDocument.Parse(legacy.ToJsonString()).RootElement,null,null).Text;
                string replay=".d_messages<-character()\nwithCallingHandlers({\n"+script+"\n},message=function(m){.d_messages<<-c(.d_messages,conditionMessage(m));invokeRestart('muffleMessage')})\n";
                string result=RunR("options(device=function(...)pdf('legacy.pdf',...))\n"+replay+
                    "stopifnot(length(roc_series)==3L,roc_comparison==3L,roc_weight==1,roc_confidence==.95,all(vapply(roc_series,function(s)s$auc==1 && s$cutoff==4,logical(1))),any(grepl('no chart settings',.d_messages)))\ncat('LEGACY_D_OK\\n')",Path.Combine(folder,"roc-legacy"));
                Check(result.Contains("LEGACY_D_OK"),"legacy ROC records infer their native default comparison from the selected groups and announce missing choices");
            }
        }
        foreach(string operation in new[]{"SurvivalPlot","CochranePlot","ROCPlot"})
        {
            JsonElement example=JsonDocument.Parse(File.ReadAllText(Path.Combine(Metadata("Examples"),operation+".json"))).RootElement;
            string script=RScriptWriter.Write(JsonDocument.Parse(RecordOf(example)).RootElement,null,null).Text;
            string selected=RunR("options(device=function(...)svg('selected-%03d.svg',...))\ninvisible(capture.output({\n"+script+
                "\n}))\nemit<-function(x)cat(formatC(as.double(x),digits=8,format='f'),'\\n')\n"+SelectedChartExampleProbe(operation),Path.Combine(folder,operation+"-example"));
            string reference=RunR(File.ReadAllText(Path.Combine(Metadata("Examples"),operation+".R")),Path.Combine(folder,operation+"-reference"));
            Check(Figures(selected)==Figures(reference),operation+" every selected worked-example value agrees with its independent R reference");
        }
    }

    private static void CheckNativeRocGeometry(ChartDefinition definition, Comparison comparison, ParameterBag native, string file)
    {
        var (svg,_)=RenderNativeChartD(definition.Clone());
        Directory.CreateDirectory(Path.GetDirectoryName(file));svg.Save(file);
        double Number(XElement e,string name)=>double.Parse(e.Attribute(name).Value,inv);
        // The full-span null-effect diagonal independently identifies the plot rectangle.
        XElement diagonal=svg.Descendants().Where(e=>e.Name.LocalName=="line")
            .MaxBy(e=>Math.Abs((Number(e,"x2")-Number(e,"x1"))*(Number(e,"y2")-Number(e,"y1"))));
        double left=Math.Min(Number(diagonal,"x1"),Number(diagonal,"x2")),bottom=Math.Max(Number(diagonal,"y1"),Number(diagonal,"y2"));
        double width=Math.Abs(Number(diagonal,"x2")-Number(diagonal,"x1")),height=Math.Abs(Number(diagonal,"y2")-Number(diagonal,"y1"));
        var points=svg.Descendants().Where(e=>e.Name.LocalName=="rect" && e.Attribute("style")?.Value.Contains("#FF0000")==true)
            .Select(e=>(x:(Number(e,"x")+Number(e,"width")/2-left)/width,y:(bottom-Number(e,"y")-Number(e,"height")/2)/height))
            .Where(p=>p.x>=-1e-8&&p.x<=1+1e-8&&p.y>=-1e-8&&p.y<=1+1e-8).ToArray();
        double[] present={1,2,2,4,5},absent={2,3,3,6,7};double[] cuts=present.Concat(absent).Order().ToArray();
        bool Positive(double v,double c)=>comparison switch{Comparison.GreaterEqual=>v>=c,Comparison.GreaterThan=>v>c,Comparison.LessEqual=>v<=c,_=>v<c};
        Check(points.Length==cuts.Length && points.Zip(cuts).All(pair=>Math.Abs(pair.First.x-absent.Count(v=>Positive(v,pair.Second))/5.0)<1e-8 && Math.Abs(pair.First.y-present.Count(v=>Positive(v,pair.Second))/5.0)<1e-8),
            "native ROC SVG threshold coordinates follow "+comparison+" including ties");
        XElement cutoff=svg.Descendants().Single(e=>e.Name.LocalName=="ellipse" && e.Attribute("style")?.Value.Contains("#008000")==true);
        Check(Math.Abs((Number(cutoff,"cx")-left)/width-native["b"].AsDouble/5)<1e-8 && Math.Abs((bottom-Number(cutoff,"cy"))/height-native["a"].AsDouble/5)<1e-8,
            "native ROC SVG cutoff marker uses the chosen sensitivity and specificity for "+comparison);
    }

    private static (XElement svg, ParameterBag results) RenderNativeChartD(ChartDefinition definition, ITemplateHost host=null)
    {
        Type type=typeof(ChartDefinition).Assembly.GetType("StatsDirect.Charting.SvgCanvasFactory",true);
        ICanvasFactory factory=(ICanvasFactory)Activator.CreateInstance(type,true);
        using IChartRenderer renderer=ChartRendererFactory.ChartRendererFor(definition,factory);
        ParameterBag results=renderer.Plot(host,false);
        using Stream image=renderer.Canvas.DetachAndReturnImageStream();
        return (XElement.Load(image),results);
    }
}
