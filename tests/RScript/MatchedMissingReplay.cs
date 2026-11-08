using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckMatchedMissingReplay(string folder)
    {
        RRecipes.Folder=Metadata("Recipes");
        double missing=Constant.MISSING;
        DataFrame Frame(params (string title,double[] values)[] columns)
        {
            DataFrame frame=new();foreach(var column in columns)frame.Variables.Add(Column(column.title,column.values));return frame;
        }
        IEnumerable<ParameterBag> Bags(ParameterBag bag)
        {
            yield return bag;
            foreach(var pair in bag)
                if(pair.Value?.AsObject is IList<ParameterBag> children)
                    foreach(var child in children)foreach(var item in Bags(child))yield return item;
        }
        double Number(ParameterBag bag,string key)=>Convert.ToDouble(Bags(bag).First(b=>b.ContainsKey(key)&&b[key]?.AsObject!=null)[key].AsObject,inv);
        string Native(ParameterBag bag,params string[] keys)=>RScriptWriter.Vector(keys.Select(k=>(object)Number(bag,k)).ToArray());
        void Verify(string label,OperationRun run,string assertions,string setup="")
        {
            RScript script=RScriptWriter.Write(Record(run),null,null);
            Check(script.HasRecipe&&!script.DataUnavailable,label+" has recorded data and the selected recipe");
            string output=RunR(setup+script.Text+"\n"+assertions+"\ncat('MATCHED_MISSING_OK\\n')\n",Path.Combine(folder,"matched-missing-"+label));
            Check(output.Contains("MATCHED_MISSING_OK"),label+" reproduces native results on the same complete observations");
        }
        string Equal(string expression,string expected,double tolerance=1e-10)=>"stopifnot(isTRUE(all.equal("+expression+","+expected+",tolerance="+tolerance.ToString("R",inv)+",check.attributes=FALSE)))\n";

        foreach(bool ragged in new[]{false,true})foreach(double confidence in new[]{0.8,0.95,0.99})
        {
            double[] x={1,2,3,4,5,6,7,8},y={2,7,1,8,3,5,4,6};
            if(ragged){x=new[]{missing,1.0,2,missing,3,4,5,6,7,8,missing};y=new[]{99.0,2,7,55,1,8,3,5,4,6,missing};}
            foreach(string method in new[]{"Spearman","Kendall"})
            {
                var(run,output)=Run(method,new(){["data"]=Frame(("Observed career",x),("Observed psychology",y)),["gamma"]=confidence});
                string assertions="stopifnot(n==8L,identical(career,1:8+0),identical(psychology,c(2,7,1,8,3,5,4,6)))\n";
                if(method=="Spearman")assertions+=Equal("c(n,rho,score,ci,upper,lower,min(1,2*min(upper,lower)))",Native(output.ParameterBag,"obs","rho","ix","from","to","p_u","p_l","p_2"));
                else assertions+=Equal("c(n,concordant,discordant,score,se,tau,limits,upper,lower,min(1,2*min(upper,lower)))",Native(output.ParameterBag,"obs","con","dis","s","ses","tau","ll","ul","p_uexact","p_lexact","p_2exact"));
                Verify(method+"-"+ragged+"-"+confidence,run,assertions);
            }
        }
        foreach(bool ragged in new[]{false,true})foreach(double confidence in new[]{0.8,0.95})
        {
            double[] a={494,395,516,434,476,557,413,442},b={490,397,512,430,470,611,415,431},c={512,401,520,428,500,600,409,440};
            if(ragged){b[3]=missing;a=a.Concat(new[]{missing}).ToArray();b=b.Concat(new[]{missing}).ToArray();c=c.Concat(new[]{missing}).ToArray();}
            var(run,output)=Run("Agreement",new(){["ci"]=confidence,["data"]=Frame(("Rater one",a),("Rater two",b),("Rater three",c))});
            Verify("agreement-"+ragged+"-"+confidence,run,Equal("c(n-1,n*(m-1),icc,sw,sqrt(2)*qnorm((1+confidence)/2)*sw)",Native(output.ParameterBag,"icc_df1","icc_df2","icc","wssd","rep"))+"stopifnot(!anyNA(flow),n=="+(ragged?"7L":"8L")+")\n");
        }
        foreach(bool ragged in new[]{false,true})
        {
            double[] a={8.4,12.8,9.6,9.8,8.4,8.6,10.4,11.2},b={9.4,15.2,10.6,9.4,9.0,11.2,12.0,13.1},c={9.8,12.9,11.2,9.9,10.4,9.8,11.7,12.0};
            if(ragged){b[2]=missing;a=a.Concat(new[]{missing}).ToArray();b=b.Concat(new[]{missing}).ToArray();c=c.Concat(new[]{missing}).ToArray();}
            var(run,output)=Run("TwoWay",new(){["data"]=Frame(("Treatment A",a),("Treatment B",b),("Treatment C",c))});
            string actual="c(anova(fit)$'Sum Sq',anova(fit)$Df,anova(fit)$'F value'[1:2],anova(fit)$'Pr(>F)'[1:2])";
            Verify("two-way-"+ragged,run,Equal(actual,Native(output.ParameterBag,"sub_sum","grp_sum","res_sum","sub_df","grp_df","res_df","sub_vr","grp_vr","sub_p","grp_p"))+"stopifnot(nrow(blocks)=="+(ragged?"7L":"8L")+",!anyNA(blocks))\n");
        }
        foreach(bool ragged in new[]{false,true})foreach(bool covariance in new[]{false,true})foreach(bool reversal in new[]{false,true})
        {
            double[][] items={
                new[]{1.2,0.4,-0.3,2.1,0.8,-1.0,0.2,1.5,-0.6,0.9,0.1,-0.2},
                new[]{1.0,0.7,-0.1,1.8,0.6,-1.3,0.5,1.2,-0.4,1.1,-0.2,0.0},
                new[]{-1.5,-0.2,0.6,-2.4,-0.6,0.8,0.0,-1.9,0.9,-0.6,-0.3,0.5},
                new[]{0.9,0.5,0.1,1.7,1.0,-1.1,0.4,1.4,-0.3,1.2,0.0,0.1}};
            if(ragged){items[1][4]=missing;items=items.Select(v=>v.Concat(new[]{missing}).ToArray()).ToArray();}
            var(run,output)=Run(covariance?"PCRCovariance":"PCRCorrelation",new(){["ci"]=0.95,["correctForReversal"]=reversal,["data"]=Frame(items.Select((v,i)=>("Chosen item "+(i+1),v)).ToArray())});
            double[] eigen=Bags(output.ParameterBag).Where(b=>b.ContainsKey("eigen")).Select(b=>Convert.ToDouble(b["eigen"].AsObject,inv)).ToArray();
            Check(eigen.Length==4,"PCA native fixture supplies four eigenvalues");
            // Eigenvector signs are arbitrary. Reliability instead verifies the reversal setting
            // through a native follow-on operation, using the same complete subject rows.
            var (_,reliability)=Run("Cronbach",new(){["ci"]=0.95},output.ParameterBag);
            Verify("pca-"+covariance+"-"+reversal+"-"+ragged,run,
                Equal("eig",RScriptWriter.Vector(eigen.Cast<object>().ToArray()),1e-8)+
                Equal("c(alpha(q),alpha(as.data.frame(scale(q))))",Native(reliability.ParameterBag,"raw_t","standard_t"))+
                "stopifnot(nrow(q)=="+(ragged?"11L":"12L")+",!anyNA(q))\n");
        }
        foreach(bool ragged in new[]{false,true})foreach(bool weighted in new[]{false,true})
        {
            double[] data=ragged?new[]{1.0,2,missing,4,0,10,6,8,9,3}:new[]{1.0,2,3,4,5,10,6,8,9,3};
            double[] weights=ragged?new[]{3.0,1,2,1,5,1,missing,2,1,0}:new[]{3.0,1,2,1,5,1,2,2,1,0};
            var(run,output)=Run("Gini",new(){["gamma"]=0.95,["boots"]=200,["seed"]=7,["data"]=Frame(("Selected positive measure",data)),["weights"]=weighted?Frame(("Frequency",weights)):null});
            Verify("gini-"+weighted+"-"+ragged,run,Equal("c(n,sd(imr)/mean(imr),G,G*n/(n-1))",Native(output.ParameterBag,"n","cv","gini","gini-unbiased"))+"stopifnot(!anyNA(imr),all(imr>0))\n");
        }
        foreach(bool ragged in new[]{false,true})foreach(bool baseline in new[]{false,true})
        {
            double[] d1={12,15,11,14,13,16},p1={10,12,9,11,12,13},b1={9,11,8,10,11,12},d2={14,13,15,12,16,14,17},p2={11,10,12,10,13,11,13},b2={10,9,11,9,12,10,12};
            if(ragged){p1[2]=missing;if(baseline)b2[1]=missing;else d2[1]=missing;}
            var(run,output)=Run("Crossover",new(){["gamma"]=0.95,["group1drug"]=Frame(("First drug",d1)),["group1placebo"]=Frame(("First placebo",p1)),["group1baseline"]=baseline?Frame(("First baseline",b1)):null,["group2drug"]=Frame(("Second drug",d2)),["group2placebo"]=Frame(("Second placebo",p2)),["group2baseline"]=baseline?Frame(("Second baseline",b2)):null});
            Verify("crossover-"+baseline+"-"+ragged,run,Equal("c(mean(drug1),mean(placebo1),mean(d1),mean(placebo2),mean(drug2),mean(d2),mean(d1)-mean(d2),mean(c(drug1,drug2)-c(placebo1,placebo2)))",Native(output.ParameterBag,"grp1_p1","grp1_p2","grp1_diff","grp2_p1","grp2_p2","grp2_diff","treatment_diff","relative_diff"))+"stopifnot(length(d1)=="+(ragged?"5L":"6L")+",length(d2)=="+(ragged?"6L":"7L")+")\n");
        }
        foreach(bool ragged in new[]{false,true})
        {
            double[] x=Enumerable.Range(1,28).Select(i=>(double)i).ToArray(),y=x.Select(v=>10*Math.Sin(v/4)+v+(v%3)-1).ToArray();
            if(ragged){x[4]=missing;y[10]=missing;}
            var(run,output)=Run("LOESS",new(){["outcome"]=Frame(("Measured Y",y)),["predictors"]=Frame(("Chosen X",x)),["model"]="measured.y~chosen.x",["title"]="Selected LOESS",["ylab"]="Chosen response",["xlab"]="Chosen predictor",["span"]=0.8,["degree"]="2",["plotFits"]=false,["plotFitsAndCi"]=true,["ci"]=0.95,["saveFitsAndSe"]=false,["saveRScript"]=false});
            string setup="plotted_lines<-list()\nlines<-function(x,y,...) { plotted_lines[[length(plotted_lines)+1L]]<<-list(x=x,y=y);graphics::lines(x,y,...) }\n";
            Verify("loess-"+ragged,run,Equal("c(m$n,m$enp,m$s,p$df)",Native(output.ParameterBag,"observations","parameters","residualse","df"),1e-7)+"stopifnot(nrow(d)==28L,m$n=="+(ragged?"26L":"28L")+",length(plotted_lines)==3L)\n"+Equal("plotted_lines[[1]]$x","sort(as.numeric(m$x[,1]))")+Equal("plotted_lines[[1]]$y","as.numeric(p$fit[order(m$x[,1])])"),setup);
        }
    }
}
