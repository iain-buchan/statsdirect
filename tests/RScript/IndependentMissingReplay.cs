using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckIndependentMissingReplay(string folder)
    {
        double m=Constant.MISSING;
        double[] first={8,12,15,11,19,14,9,16,21,18,13,17}, second={9,10,13,12,17,16,14,18};
        double[] Missing(double[] values,int layout) => layout==0?values.ToArray():layout==1?
            new[]{m}.Concat(values.Take(3)).Concat(new[]{m}).Concat(values.Skip(3)).Append(m).ToArray():values.Concat(new[]{m,m}).ToArray();
        DataFrame Frame(params (string title,double[] values)[] columns)
        {
            DataFrame data=new();foreach(var column in columns)data.Variables.Add(Column(column.title,column.values.ToArray()));return data;
        }
        JsonElement ReplayRecord(string operation,DataFrame data,Dictionary<string,object> settings)
        {
            ParameterBag bag=new();bag.AddInput("data",data);
            foreach(var pair in settings)if(pair.Value!=null)bag.AddInput(pair.Key,pair.Value);
            OperationRun run=new(Load(operation));run.Note(new FrameParameter{Name="data"},"Selected input",value:bag["data"]);
            return JsonDocument.Parse(RunRecord.Write(run,bag)).RootElement.Clone();
        }
        double Num(ParameterBag bag,string key)=>Convert.ToDouble(bag[key].AsObject,inv);
        string Vector(IEnumerable<double> values)=>RScriptWriter.Vector(values.Cast<object>().ToArray());
        string Equal(string actual,IEnumerable<double> expected,double tolerance=1e-9)=>
            "stopifnot(isTRUE(all.equal("+actual+","+Vector(expected)+",tolerance="+tolerance.ToString("R",inv)+",check.attributes=FALSE)))\n";
        string Execute(string operation,string label,DataFrame data,Dictionary<string,object> settings,string assertions)
        {
            var script=RScriptWriter.Write(ReplayRecord(operation,data,settings),null,null);
            Check(script.HasRecipe&&!script.DataUnavailable,operation+" "+label+" writes all original entered data and settings");
            string output=RunR(script.Text+"\n"+assertions+"\ncat('INDEPENDENT_MISSING_OK\\n')\n",Path.Combine(folder,"independent-"+operation+"-"+label));
            Check(output.Contains("INDEPENDENT_MISSING_OK")&&!output.Contains("= NA"),operation+" "+label+" replays selected data and native results in R");
            return output;
        }
        foreach(int layout in new[]{0,1,2})
        foreach(double confidence in new[]{0.8,0.95,0.99})
        {
            double[] x=Missing(first,layout),y=Missing(second,layout);
            string label=layout+"-"+confidence.ToString("R",inv);
            var singleSettings=new Dictionary<string,object>{{"gamma",confidence},{"population-mean",12d}};
            var (_,single)=Run("TSingle",new(){{"data",Frame(("Chosen score",x))},{"gamma",confidence},{"population-mean",12d}});
            ParameterBag s=single.ParameterBag;
            double singlePower=Power.ptpower(1-confidence,Num(s,"sam_mean")-12,Num(s,"sd"),Num(s,"size"));
            Execute("TSingle",label,Frame(("Chosen score",x)),singleSettings,
                Equal("c(n,mean(bp),sd(bp),unname(r$parameter),unname(r$statistic),r$p.value,r$conf.int[1]-mu,r$conf.int[2]-mu)",new[]{Num(s,"size"),Num(s,"sam_mean"),Num(s,"sd"),Num(s,"df"),Num(s,"t"),Num(s,"p_2"),Num(s,"from"),Num(s,"to")})+
                Equal("pw$power",new[]{singlePower})+"stopifnot(identical(bp,"+Vector(first)+"),alpha==1-confidence)\n");

            var (_,unpaired)=Run("TUnpaired",new(){{"data",Frame(("Long observed group",x),("Short observed group",y))},{"gamma",confidence}});
            ParameterBag u=unpaired.ParameterBag;
            double delta=Math.Abs(Num(u,"mean_0")-Num(u,"mean_1"));
            double pooledSd=Num(u,"error")/Math.Sqrt(1/Num(u,"n0")+1/Num(u,"n1"));
            double equalPower=Power.tstpower(1-confidence,delta,pooledSd,Num(u,"n0"),Num(u,"n1")/Num(u,"n0"));
            double unequalPower=Power.uvttpower(1-confidence,delta,Num(u,"n0"),Num(u,"n1"),Math.Sqrt(first.Select(v=>(v-first.Average())*(v-first.Average())).Sum()/(first.Length-1)),Math.Sqrt(second.Select(v=>(v-second.Average())*(v-second.Average())).Sum()/(second.Length-1)));
            string unpairedOutput=Execute("TUnpaired",label,Frame(("Long observed group",x),("Short observed group",y)),new(){{"gamma",confidence}},
                Equal("c(length(high),mean(high),length(low),mean(low),equal$stderr,unname(equal$parameter),unname(equal$statistic),equal$p.value,equal$conf.int,unequal$stderr,unname(unequal$parameter),unname(unequal$statistic),unequal$p.value,unequal$conf.int)",new[]{Num(u,"n0"),Num(u,"mean_0"),Num(u,"n1"),Num(u,"mean_1"),Num(u,"error"),Num(u,"df"),Num(u,"t"),Num(u,"p_2"),Num(u,"from"),Num(u,"to"),Num(u,"error_unequal"),Num(u,"df_unequal"),Num(u,"t_unequal"),Num(u,"p_2_unequal"),Num(u,"from_unequal"),Num(u,"to_unequal")})+
                "stopifnot(identical(high,"+Vector(first)+"),identical(low,"+Vector(second)+"),alpha==1-confidence)\n");
            string powerLabel="Power (for "+(100*(1-confidence)).ToString("G6",inv)+"% significance) = ";
            Check(unpairedOutput.Contains(powerLabel+(100*equalPower).ToString("F2",inv)+"%")&&unpairedOutput.Contains(powerLabel+(100*unequalPower).ToString("F2",inv)+"%"),"unpaired "+label+" powers and significance labels match both native models");
        }
        foreach(int layout in new[]{0,1,2})
        {
            double[] x=Missing(first,layout),y=Missing(second,layout);
            var (_,reference)=Run("ReferenceRange",new(){{"data",Frame(("Selected reference sample",x))},{"gamma",0.9},{"reference-interval",0.9},{"do_conservative",true}});
            ParameterBag rr=reference.ParameterBag;
            Execute("ReferenceRange",layout.ToString(),Frame(("Selected reference sample",x)),new(){{"gamma",0.9},{"reference-interval",0.9},{"do_conservative",true}},
                Equal("c(n,mean(igm),sd(igm))",new[]{Num(rr,"size"),Num(rr,"mean"),Num(rr,"sd")})+
                "captured<-numeric()\nnormal_range(igm,function(value){captured<<-c(captured,value);value})\n"+
                Equal("captured",new[]{Num(rr,"lrr"),Num(rr,"urr"),Num(rr,"lx_l"),Num(rr,"ux_l"),Num(rr,"lx_u"),Num(rr,"ux_u")})+
                "stopifnot(identical(igm,"+Vector(first)+"))\n");

            foreach(object popsd in new object[]{null,4d})
            {
                var (_,ztest)=Run("ZSingle",new(){{"data",Frame(("Selected z sample",x))},{"gamma",0.9},{"popmean",12d},{"popsd",popsd}});
                ParameterBag z=ztest.ParameterBag;
                Execute("ZSingle",layout+"-"+(popsd==null?"estimated":"known"),Frame(("Selected z sample",x)),new(){{"gamma",0.9},{"popmean",12d},{"popsd",popsd}},
                    Equal("c(n,mean(speed),sd(speed),dev,mean(speed)-mu-z*se,mean(speed)-mu+z*se,2*pnorm(-abs(dev)))",new[]{Num(z,"size"),Num(z,"mean"),Num(z,"sd"),Num(z,"z"),Num(z,"from"),Num(z,"to"),Num(z,"p_2")})+
                    "stopifnot(identical(speed,"+Vector(first)+"))\n");
            }

            string nativeSummary=null;
            Run("QuickSummary",new(){{"data",Frame(("Selected quick summary",x))},{"gamma",0.9}},amend:options=>{if(options is SummaryStatisticsOptions summary)nativeSummary=summary.Text;});
            Summary native=new();native.FullSummaryFromX(x.ToArray(),x.Length,"Selected quick summary",0.9,5,95,1);
            int missing=x.Count(v=>v==m);
            string quickOutput=Execute("QuickSummary",layout.ToString(),Frame(("Selected quick summary",x)),new(){{"gamma",0.9}},
                Equal("c(n,missing,sum(bp),mean(bp),var(bp),sd(bp),se,exp(mean(log(bp))),m(3)/m(2)^1.5,m(4)/m(2)^2,max(bp),unname(q),min(bp))",new[]{(double)native.ValidData,native.MissingData,native.Sum,native.Mean,native.Variance,native.SD,native.SEM,native.GeometricMean,native.Skewness,native.Kurtosis,native.Maximum,native.UserCentileL,native.LowerQuartile,native.Median,native.UpperQuartile,native.UserCentileU,native.Minimum})+
                "stopifnot(identical(bp,"+Vector(first)+"))\n");
            Check(nativeSummary!=null&&Regex.IsMatch(nativeSummary,@"Valid data\s+12\b")&&Regex.IsMatch(nativeSummary,@"Missing\s+"+missing+@"\b")&&quickOutput.Contains("Valid data 12   Missing "+missing),"quick summary "+layout+" preserves the native missing count before omission");

            double[] farm=Missing(new[]{1d,3,4,6,7},layout),town=Missing(new[]{2d,3,5,8,9,11},layout);
            var (_,mann)=Run("MannWhitney",new(){{"data",Frame(("First rank sample",farm),("Second rank sample",town))},{"gamma",0.9}});
            ParameterBag mw=mann.ParameterBag;
            Execute("MannWhitney",layout.ToString(),Frame(("First rank sample",farm),("Second rank sample",town)),new(){{"gamma",0.9}},
                Equal("c(n1,n2,median(farm),median(town),ranksum,U,Uprime,theta,ci)",new[]{Num(mw,"obs_1"),Num(mw,"obs_2"),Num(mw,"median_1"),Num(mw,"median_2"),Num(mw,"ranksum"),Num(mw,"u"),Num(mw,"u_prime"),Num(mw,"theta"),Num(mw,"tll"),Num(mw,"tul")},1e-7)+
                Equal("c(lower,upper,min(1,2*min(lower,upper)))",new[]{Num(mw,"p_l"),Num(mw,"p_u"),Num(mw,"p_2")})+
                "stopifnot(identical(farm,c(1,3,4,6,7)),identical(town,c(2,3,5,8,9,11)))\n");

            // Normality acquisition normally removes missing data already; imported
            // or legacy replay inputs get the same cleaning before all calculations.
            var (_,normal)=Run("Normality",new(){{"data",Frame(("Selected normality sample",first))}});
            var nv=((IList<ParameterBag>)normal.ParameterBag["*variable"].AsObject)[0];
            double sw=double.Parse(nv["sw_w"].AsObject.ToString().Trim().TrimEnd(','),inv);
            double sf=double.Parse(nv["sf_w"].AsObject.ToString().Trim().TrimEnd(','),inv);
            Execute("Normality",layout.ToString(),Frame(("Selected normality sample",x)),new(),
                Equal("c(n,mean(yield),sd(yield),unname(sw$statistic),sw$p.value,Wf,psf)",new[]{Num(nv,"n"),Num(nv,"mean"),Num(nv,"sd"),sw,Num(nv,"sw_p"),sf,Num(nv,"sf_p")},1e-7)+
                "stopifnot(identical(yield,"+Vector(first)+"))\n");
        }
        foreach(var sample in new[]{
            ("first-larger",new[]{1d,4,9,15,22},new[]{2d,3,4,5}),
            ("second-larger",new[]{2d,3,4,5},new[]{1d,4,9,15,22}),
            ("tied",new[]{0d,2},new[]{-2d,0,0,0,2})})
        foreach(int layout in new[]{0,1,2})
        {
            double[] x=Missing(sample.Item2,layout),y=Missing(sample.Item3,layout);
            var (_,native)=Run("VarianceRatio",new(){{"data",Frame(("First actual group",x),("Second actual group",y))}});
            ParameterBag v=native.ParameterBag;
            string title0=v["title_0"].AsObject.ToString(),title1=v["title_1"].AsObject.ToString();
            string output=Execute("VarianceRatio",sample.Item1+"-"+layout,Frame(("First actual group",x),("Second actual group",y)),new(),
                Equal("c(unname(f$parameter),var(high),var(low),unname(f$statistic),p,2*min(p,1-p))",new[]{Num(v,"df_0"),Num(v,"df_1"),Num(v,"var_0"),Num(v,"var_1"),Num(v,"f"),Num(v,"p_1"),Num(v,"p_2")})+
                "stopifnot(identical(high_title,"+RScriptWriter.Quote(title0)+"),identical(low_title,"+RScriptWriter.Quote(title1)+"),identical(f$data.name,paste(high_title,'and',low_title)))\n");
            Check(output.Contains(title0)&&output.Contains(title1)&&!output.Contains("Marked symptoms")&&!output.Contains("Slight/no symptoms"),"variance ratio "+sample.Item1+"-"+layout+" preserves actual sample titles and native ordering");
        }
        // NumericReplaceMissing records retain explicitly selected missing markers,
        // including one at the end: counts are computed before each recipe omits it.
        foreach (bool trailing in new[] { false, true })
        {
            double[] values=trailing?first.Append(m).ToArray():first.Take(4).Concat(new[]{m}).Concat(first.Skip(4)).ToArray();
            DataFrame Selected() => SheetFrame("C:/tests/Explicit-missing.xlsx", "NumericReplaceMissing", 0, 0, ("Selected missing marker",values));
            string label=trailing?"explicit-trailing":"explicit-interior";
            var (_,summary)=Run("UnivariateSummary",new(){{"data",Selected()},{"gamma",0.95},{"report-centile-type","1"},{"report-udca",0.05},{"report-udcb",0.95},{"output-to-frame",false}});
            var fields=(IList<ParameterBag>)summary.ParameterBag["*fields"].AsObject;
            double Field(int i)=>Num(((IList<ParameterBag>)fields[i]["*results"].AsObject)[0],"result");
            Check(Field(0)==first.Length&&Field(1)==1,"native univariate summary "+label+" retains the explicit missing count");
            Execute("UnivariateSummary",label,Selected(),new(){{"gamma",0.95},{"report-centile-type","1"},{"report-udca",0.05},{"report-udcb",0.95}},
                Equal("unname(summary_results[[1]][c('n','missing')])",new[]{Field(0),Field(1)})+
                "stopifnot(length(samples[[1]])==13,sum(is.na(samples[[1]]))==1)\n");
            var (_,quantile)=Run("Quantile",new(){{"data",Selected()},{"gamma",0.95},{"conservative-ci",false},{"quantile",0.5}});
            var qv=((IList<ParameterBag>)quantile.ParameterBag["*variable"].AsObject)[0];
            Execute("Quantile",label,Selected(),new(){{"gamma",0.95},{"conservative-ci",false},{"quantile",0.5}},
                Equal("unlist(quantile_results[[1]][c('n','missing','value','lower','upper','coverage')])",new[]{Num(qv,"size"),values.Length-Num(qv,"size"),Num(qv,"value"),Num(qv,"from"),Num(qv,"to"),Num(qv,"exact")})+
                "stopifnot(length(samples[[1]])==13,sum(is.na(samples[[1]]))==1)\n");
        }
    }
}
