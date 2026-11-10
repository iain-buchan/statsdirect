using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private static void CheckDeferredModels538(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        Directory.CreateDirectory(folder);
        double Num(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        IList<ParameterBag> Block(ParameterBag bag, string key) => (IList<ParameterBag>)bag["*" + key].AsObject;
        string Vec(IEnumerable<double> values) => RScriptWriter.Vector(values.Cast<object>().ToArray());
        string Equal(string expression, IEnumerable<double> expected, double tolerance = 1e-8) =>
            "stopifnot(isTRUE(all.equal(unname(c(" + expression + "))," + Vec(expected) + ",tolerance=" + tolerance.ToString("R", inv) + ",check.attributes=FALSE)))\n";
        Func<DataAcquisitionMode, int[], DataFrame> Sheet(params (string title, double[] values)[] columns)
        {
            RecordCells cells = new(columns.Select(c => new object[] { c.title }.Concat(c.values.Cast<object>()).ToArray()).ToArray());
            int rows = columns[0].values.Length + 1;
            return (mode, selected) =>
            {
                DataFrame frame = new();
                foreach (int column in selected)
                    foreach (var variable in cells.Acquire(column, 0, rows, mode).Variables)
                        frame.Variables.Add(variable);
                return frame;
            };
        }
        DataFrame Select(Func<DataAcquisitionMode, int[], DataFrame> sheet, params int[] columns) => sheet(DataAcquisitionMode.NumericPreserveRows, columns);
        string Verify(string label, OperationRun run, string assertions)
        {
            RScript script = RScriptWriter.Write(Record(run), null, null);
            Check(script.HasRecipe && !script.DataUnavailable, label + " exports acquired model data and selected settings");
            string result = RunR(script.Text + "\n" + assertions + "\ncat('DEFERRED_MODELS_538_OK\\n')\n", Path.Combine(folder, label));
            Check(result.Contains("DEFERRED_MODELS_538_OK"), label + " agrees with native model statistics and availability");
            return result;
        }
        Dictionary<string, object> LogisticAnswers(Func<DataAcquisitionMode, int[], DataFrame> sheet, bool intercept = true) => new()
        {
            ["response"] = Select(sheet, 0), ["total"] = Select(sheet, 1), ["weight"] = Select(sheet, 2), ["predictors"] = Select(sheet, 3),
            ["gamma"] = .95, ["grouping"] = "grouped", ["accuracy"] = "0.00000000001", ["intercept"] = intercept, ["weights"] = true
        };
        ParameterBag LogisticModel(ParameterBag output) => Run("LogisticRegressionModel", new(), output).output.ParameterBag;
        string LogisticMatch(ParameterBag model) =>
            Equal("coef(fit)", Block(model, "parameters").Select(b => Num(b, "coef"))) +
            Equal("summary(fit)$coefficients[,2]", Block(model, "parameters").Select(b => Num(b, "err")), 1e-7) +
            Equal("ll,dev,fit$df.residual,k,pearson", new[] { Num(model,"ll"), Num(model,"dev"), Num(model,"idf"), Num(model,"rank"), Num(model,"x2") });

        // An explicit independent grouped-Pearson oracle: 37 quarter-weight
        // individuals (frequency 9.25), with known occupied bins. This used to
        // overrun the native fixed arrays. No copied HL loop constructs the oracle.
        double[] fractionalX = Enumerable.Range(1, 37).Select(i => (double)i).ToArray();
        double[] fractionalY = { 0,0,0,1,0,0,0,0,1,0,0,1,0,0,1,0,1,0,0,1,1,0,1,0,1,1,0,1,1,0,1,1,1,0,1,1,1 };
        var fractionSheet = Sheet(("Outcome",fractionalY),("Total",Enumerable.Repeat(1d,37).ToArray()),("Prior",Enumerable.Repeat(.25,37).ToArray()),("Dose",fractionalX));
        var (fractionRun, fractionOutput) = Run("LogisticRegression", LogisticAnswers(fractionSheet));
        ParameterBag fractionModel = LogisticModel(fractionOutput.ParameterBag);
        Check(Num(fractionModel,"df_hl") == 8, "HL frequency 9.25 has ten occupied groups and eight df");
        string fractionOracle = "oracle_group<-c(1L,rep(2L,3L),rep(3:9,each=4L),rep(10L,5L))\n" +
            "oracle<-rowsum(cbind(observed=responders*.25,expected=fitted(fit)*.25,total=rep(.25,37)),oracle_group)\n" +
            "oracle_hl<-sum((oracle[,'observed']-oracle[,'expected'])^2/(oracle[,'expected']*(1-oracle[,'expected']/oracle[,'total'])))\n" +
            Equal("oracle_hl,hl,hldf,pchisq(hl,hldf,lower.tail=FALSE)",new[]{Num(fractionModel,"hl"),Num(fractionModel,"hl"),Num(fractionModel,"df_hl"),Num(fractionModel,"p_hl")});
        Verify("hl-fractional-9.25", fractionRun, LogisticMatch(fractionModel) + fractionOracle);

        // The same eight covariates have different prior weights in two blocks.
        // Their combined frequency is 100 each: HL is simply the Pearson sum for
        // the eight whole patterns, regardless of how their two rows are ordered.
        double[] patternResponse = {1,2,2,3,4,5,6,7,7,9,13,18,23,28,31,34};
        double[] patternTotal = Enumerable.Repeat(20d,8).Concat(Enumerable.Repeat(40d,8)).ToArray();
        double[] patternWeight = Enumerable.Repeat(1d,8).Concat(Enumerable.Repeat(2d,8)).ToArray();
        double[] patternDose = Enumerable.Range(0,16).Select(i => (double)(i%8)).ToArray();
        double? firstHl = null;
        for (int permutation = 0; permutation < 8; permutation++)
        {
            Random random = new(147 + permutation);
            int[] order = Enumerable.Range(0,16).OrderBy(_ => random.Next()).ToArray();
            double[] P(double[] values) => order.Select(i => values[i]).ToArray();
            var sheet = Sheet(("Outcome",P(patternResponse)),("Total",P(patternTotal)),("Prior",P(patternWeight)),("Dose",P(patternDose)));
            var (run, output) = Run("LogisticRegression", LogisticAnswers(sheet));
            ParameterBag model = LogisticModel(output.ParameterBag);
            double hl = Num(model,"hl");
            Check(Num(model,"df_hl") == 6 && (firstHl == null || Close(hl,firstHl.Value,1e-10)), "HL combines equal covariates across weights and is invariant to row permutation " + permutation);
            firstHl ??= hl;
            string oracle = "oracle<-rowsum(cbind(observed=responders*weight,expected=fitted(fit)*total*weight,total=total*weight),predictors[[1]])\n" +
                "oracle_hl<-sum((oracle[,'observed']-oracle[,'expected'])^2/(oracle[,'expected']*(1-oracle[,'expected']/oracle[,'total'])))\n" +
                Equal("oracle_hl,hl,hldf",new[]{hl,hl,6d});
            Verify("hl-tied-patterns-"+permutation,run,LogisticMatch(model)+oracle);
        }

        // Pattern equality uses the actual doubles and retains first occurrence.
        double[] exactX = { .1+.2,.3,1,2,3 }, exactY = {2,5,8,12,17};
        var exactSheet = Sheet(("Outcome",exactY),("Total",Enumerable.Repeat(20d,5).ToArray()),("Prior",Enumerable.Repeat(1d,5).ToArray()),("Dose",exactX));
        var (exactRun, exactOutput) = Run("LogisticRegression", LogisticAnswers(exactSheet));
        ParameterBag exactModel = LogisticModel(exactOutput.ParameterBag);
        Check(Num(exactModel,"idf")+Num(exactModel,"rank")==5,"native preserves distinct adjacent floating-point covariate patterns");
        Verify("logistic-exact-patterns",exactRun,LogisticMatch(exactModel)+"stopifnot(n==5L,identical(predictors[[1]],"+Vec(exactX)+"))\n");

        double[] dose = Enumerable.Range(0,12).Select(i=>1.5+i).ToArray(), total = Enumerable.Repeat(20d,12).ToArray();
        double[] response = {2,3,4,5,6,8,10,11,12,14,15,17}, weights = {1,2,1,2,1,2,1,2,1,2,1,2};
        int headingNumber=0;
        foreach (string title in new[]{"Dose","weight","total","responders","prior_weight","a`b","a\\b","sep","decreasing","na.last","method"})
        {
            var sheet=Sheet(("Outcome",response),("Total",total),("Prior",weights),(title,dose));
            var(run,output)=Run("LogisticRegression",LogisticAnswers(sheet));
            var model=LogisticModel(output.ParameterBag);
            Verify("logistic-heading-"+headingNumber++,run,LogisticMatch(model)+"stopifnot(fit$rank==2L)\n");
        }

        double[] counts = {2,5,9,14,20,11,6,4,8,7,12,16};
        double[] exposures = {100,120,140,160,180,150,110,90,130,120,150,170};
        Dictionary<string,object> PoissonAnswers(Func<DataAcquisitionMode,int[],DataFrame> sheet,bool intercept=true,bool exposed=true)=>new()
        {
            ["response"]=Select(sheet,0),["weight"]=Select(sheet,1),["exposure"]=exposed?Select(sheet,2):null,["predictors"]=Select(sheet,3),
            ["gamma"]=.95,["grouping"]="individual",["accuracy"]="0.00000000001",["intercept"]=intercept,["weights"]=true,["has-exposure"]=exposed?"true":"false"
        };
        string PoissonMatch(ParameterBag model)=>Equal("coef(fit)",Block(model,"unscaled").Select(b=>Num(b,"coef")))+
            Equal("summary(fit)$coefficients[,2]",Block(model,"unscaled").Select(b=>Num(b,"err")),1e-7)+
            Equal("ll,dev,fit$df.residual,k,pearson,scale",new[]{Num(model,"ll"),Num(model,"dev"),Num(model,"idf"),Num(model,"rank"),Num(model,"x2_pearson"),Num(model,"sp")});
        headingNumber=0;
        foreach(string title in new[]{"Dose","weight","response","exposure","log_exposure","prior_weight","a`b"})
        {
            var sheet=Sheet(("Outcome",counts),("Prior",weights),("Exposure",exposures),(title,dose));
            var(run,output)=Run("PoissonRegression",PoissonAnswers(sheet));
            var model=Run("PoissonRegressionModel",new(),output.ParameterBag).output.ParameterBag;
            Verify("poisson-heading-"+headingNumber++,run,PoissonMatch(model));
        }

        // Zero priors are equivalent to removing those observations, for every
        // fitted statistic and for the active residual sequence used by DW.
        foreach(int zero in new[]{0,5,11})
        foreach(bool intercept in new[]{false,true})
        {
            double[] w=(double[])weights.Clone();w[zero]=0;
            var sheet=Sheet(("Outcome",counts),("Prior",w),("Exposure",exposures),("Dose",dose));
            var(run,output)=Run("PoissonRegression",PoissonAnswers(sheet,intercept));
            var model=Run("PoissonRegressionModel",new(),output.ParameterBag).output.ParameterBag;
            Check(double.IsFinite(Num(model,"ll")) && Num(model,"idf")==11-Num(model,"rank"),"Poisson zero prior is absent from likelihood and df " + zero+"-"+intercept);
            Verify("poisson-zero-"+zero+"-"+intercept,run,PoissonMatch(model)+"stopifnot(length(response)==11L,all(weight>0))\n");
        }
        {
            double[] yy=(double[])counts.Clone(),xx=(double[])dose.Clone(),ee=(double[])exposures.Clone(),ww=weights.Select(v=>v*.25).ToArray();
            yy[3]=Constant.MISSING;xx[8]=Constant.MISSING;ee[9]=0;ww[0]=0;ww[5]=Constant.MISSING;
            var sheet=Sheet(("Outcome",yy),("Prior",ww),("Exposure",ee),("weight",xx));
            var(run,output)=Run("PoissonRegression",PoissonAnswers(sheet));
            var model=Run("PoissonRegressionModel",new(),output.ParameterBag).output.ParameterBag;
            Check(Num(model,"idf")==5,"Poisson jointly excludes missing inputs, zero exposure and zero prior weights");
            Verify("poisson-zero-fractional-missing",run,PoissonMatch(model)+"stopifnot(length(response)==7L,all(weight>0),all(exposure>0),all(complete.cases(response,predictors,weight,exposure)))\n");
        }
        double[] outcome = dose.Select((v,i)=>4+2*v+Math.Sin(i)*3).ToArray();
        headingNumber=0;
        foreach(string title in new[]{"Dose","weights","outcome","a`b"})
        foreach(int zero in new[]{-1,0,5,11})
        foreach(bool intercept in new[]{false,true})
        {
            double[] w=(double[])weights.Clone();if(zero>=0)w[zero]=0;
            var sheet=Sheet(("Outcome",outcome),("Prior",w),(title,dose));
            var(run,output)=Run("MultipleLinearRegression",new(){["outcome"]=Select(sheet,0),["weights"]=Select(sheet,1),["predictors"]=Select(sheet,2),["ci"]=.95,["calculateIntercept"]=intercept,["useWeights"]=true});
            var anova=Run("MultipleLinearRegressionAnova",new(){["ci"]=.95},output.ParameterBag).output.ParameterBag;
            string match=Equal("coef(fit)",AcquisitionCoefficients(output.ParameterBag))+
                Equal("fit$df.residual,summary(fit)$sigma,sum(diff(residuals(fit))^2)/sum(residuals(fit)^2),100*summary(fit)$r.squared,100*summary(fit)$adj.r.squared",new[]{Num(anova,"res_df"),Num(anova,"mse"),Num(anova,"dw"),Num(anova,"r2"),Num(anova,"ra2")});
            Verify("mlr-heading-zero-"+headingNumber++,run,match+"stopifnot(nobs(fit)=="+(zero<0?12:11)+"L,all(weights>0))\n");
        }
        {
            double[] yy=(double[])outcome.Clone(),xx=(double[])dose.Clone(),ww=weights.Select(v=>v*.25).ToArray();
            yy[3]=Constant.MISSING;xx[8]=Constant.MISSING;ww[0]=0;ww[5]=Constant.MISSING;ww[11]=0;
            var sheet=Sheet(("Outcome",yy),("Prior",ww),("outcome",xx));
            var(run,output)=Run("MultipleLinearRegression",new(){["outcome"]=Select(sheet,0),["weights"]=Select(sheet,1),["predictors"]=Select(sheet,2),["ci"]=.95,["calculateIntercept"]=true,["useWeights"]=true});
            var anova=Run("MultipleLinearRegressionAnova",new(){["ci"]=.95},output.ParameterBag).output.ParameterBag;
            Verify("mlr-zero-fractional-missing",run,Equal("coef(fit)",AcquisitionCoefficients(output.ParameterBag))+
                Equal("fit$df.residual,summary(fit)$sigma,sum(diff(residuals(fit))^2)/sum(residuals(fit)^2)",new[]{Num(anova,"res_df"),Num(anova,"mse"),Num(anova,"dw")})+
                "stopifnot(nobs(fit)==7L,all(weights>0),all(complete.cases(outcome,predictors,weights)))\n");
        }
        foreach(string model in new[]{"PoissonRegression","MultipleLinearRegression"})
        {
            var sheet=Sheet(("Outcome",counts),("Prior",new double[12]),("Exposure",exposures),("Dose",dose));
            try
            {
                if(model=="PoissonRegression")Run(model,PoissonAnswers(sheet));
                else Run(model,new(){["outcome"]=Select(sheet,0),["weights"]=Select(sheet,1),["predictors"]=Select(sheet,3),["ci"]=.95,["calculateIntercept"]=true,["useWeights"]=true});
                Check(false,model+" must refuse an empty positive-weight sample");
            }
            catch(Exception ex) when(ex.GetBaseException().Message.Contains("positive prior weight"))
            {
                Check(true,model+" refuses all-zero weights with a clear message");
            }
        }

        double[] times={6,7,10,15,19,25,2,4,8,12,14,20,23,30},events={1,1,1,1,0,1,1,1,1,0,1,1,1,0};
        double[] treatment={0,1,0,1,0,1,0,1,0,1,0,1,0,1},strata={1,1,1,1,1,1,1,2,2,2,2,2,2,2};
        headingNumber=0;
        foreach(string title in new[]{"Treatment","time","censor","sd_stratum","a`b"})
        foreach(bool stratified in new[]{false,true})
        {
            var sheet=Sheet(("Time",times),("Event",events),(title,treatment),("Stratum",strata));
            var(run,output)=Run("CoxRegression",new(){["times"]=Select(sheet,0),["events"]=Select(sheet,1),["predictors"]=Select(sheet,2),["strata"]=stratified?sheet(DataAcquisitionMode.CategoryReplaceMissing,new[]{3}):null,["accuracy"]=1e-11,["centre-continuous-covariates"]=true,["useTimesAdjustment"]=false});
            double coefficient=Num(Block(output.ParameterBag,"pred")[0],"b");
            string coxText=Verify("cox-heading-"+headingNumber++,run,Equal("coef(fit)",new[]{coefficient})+
                "stopifnot(identical(all.vars(fit$call$formula),c(sd_own[1:2],names(predictors),if(!is.null(stratum))sd_own[3])))\n");
            Check(Regex.IsMatch(coxText,@"^Call:\s*coxph\(formula\s*=\s*Surv\(",RegexOptions.Multiline),
                "Cox displays the fitted survival formula for " + title + ", stratified = " + stratified);
            var(plotRun,_)=Run("CoxHazardPlots",new(){["use-tics"]=true,["use-markers"]=true,["group"]=stratified?new[]{true}:new[]{true,false}},output.ParameterBag);
            Verify("cox-heading-plots-"+headingNumber,plotRun,"stopifnot(curve_count==2L,all(is.finite(curves$surv)),all(curves$surv>=0 & curves$surv<=1))\n");
            foreach(double confidence in new[]{.8,.9,.95,.99})
            {
                var(follow,ratios)=Run("CoxHazardRatios",new(){["gamma"]=confidence},output.ParameterBag);
                var row=Block(ratios.ParameterBag,"hazard")[0];
                string result=Verify("cox-confidence-"+headingNumber+"-"+confidence,follow,
                    Equal("exp(coef(fit)),exp(coef(fit)-qnorm((1+confidence)/2)*sqrt(diag(vcov(fit)))),exp(coef(fit)+qnorm((1+confidence)/2)*sqrt(diag(vcov(fit))))",new[]{Num(row,"ec"),Num(row,"ell"),Num(row,"eul")})+
                    "stopifnot(confidence=="+confidence.ToString("R",inv)+")\n");
                Check(result.Contains((100*confidence).ToString("G",inv)+"% CI ="),"Cox hazard-ratio report labels selected confidence "+confidence);
            }
        }
        double[] pair={1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10};
        double[] isCase=Enumerable.Range(0,20).Select(i=>i%2==0?1d:0).ToArray(),exposed={1,0,1,0,1,1,0,1,1,0,0,0,1,0,1,1,0,0,1,0};
        headingNumber=0;
        foreach(string title in new[]{"Exposure","PAIRID","LBWT","a`b"})
        {
            var sheet=Sheet(("Pair",pair),("Case",isCase),(title,exposed));
            var(run,output)=Run("ConditionalLogisticRegression",new(){["stratum"]=sheet(DataAcquisitionMode.CategoryReplaceMissing,new[]{0}),["case-control"]=Select(sheet,1),["predictors"]=Select(sheet,2),["gamma"]=.9,["accuracy"]="0.000000001",["show-counts"]=false});
            Verify("conditional-heading-"+headingNumber++,run,Equal("coef(fit)",Block(output.ParameterBag,"est").Select(b=>Num(b,"b")),1e-7));
        }

        // Tightening glm's epsilon also tightens its internal QR tolerance. A
        // constant predictor then appeared estimable and one fit did not converge.
        // Warm starts must refine covariance while keeping the aliased rank.
        foreach(bool logistic in new[]{false,true})
        {
            double[] constant=Enumerable.Repeat(logistic?2d:0d,12).ToArray();
            var sheet=logistic?Sheet(("Outcome",response),("Total",total),("Prior",weights),("Constant",constant)):
                Sheet(("Outcome",counts),("Prior",weights),("Exposure",exposures),("Constant",constant));
            var(run,output)=Run(logistic?"LogisticRegression":"PoissonRegression",logistic?LogisticAnswers(sheet):PoissonAnswers(sheet,true,false));
            var model=Run(logistic?"LogisticRegressionModel":"PoissonRegressionModel",new(),output.ParameterBag).output.ParameterBag;
            Check(Num(model,"rank")==1&&Num(model,"x2df")==0&&Num(model,"x2p")==Constant.MISSING,"native aliased model has rank one and no LR probability "+logistic);
            string rendered=Verify("aliased-glm-"+logistic,run,Equal("fit$rank,fit$df.residual,df_lr",new[]{Num(model,"rank"),Num(model,"idf"),Num(model,"x2df")})+
                "stopifnot(fit$converged,!fit$boundary,is.na(coef(fit)[2]),all(is.finite(summary(fit)$coefficients)),all(summary(fit)$coefficients[,2]>0))\n");
            Check(Regex.IsMatch(rendered,@"Deviance \(likelihood ratio\) chi-square = [^\r\n]*df = 0[^\r\n]*P = \*")&&
                Regex.IsMatch(rendered,@"Deviance with all covariates = [^\r\n]*rank = 1"),"R renders aliased rank and unavailable LR P "+logistic);
            if(!logistic)Check(Regex.IsMatch(rendered,@"Scaled G2 = [^\r\n]*df = 0[^\r\n]*P = \*"),"scaled aliased Poisson LR P is unavailable");
        }
        foreach(bool logistic in new[]{false,true})
        {
            double[] aliased=Enumerable.Repeat(1d,12).ToArray();
            var sheet=logistic?Sheet(("Outcome",response),("Total",total),("Prior",weights),("Aliased",aliased),("Dose",dose)):
                Sheet(("Outcome",counts),("Prior",weights),("Exposure",exposures),("Aliased",aliased),("Dose",dose));
            var answers=logistic?LogisticAnswers(sheet):PoissonAnswers(sheet,true,false);answers["predictors"]=Select(sheet,3,4);
            var(run,output)=Run(logistic?"LogisticRegression":"PoissonRegression",answers);
            var model=Run(logistic?"LogisticRegressionModel":"PoissonRegressionModel",new(),output.ParameterBag).output.ParameterBag;
            Check(Num(model,"rank")==2&&Num(model,"x2df")==1,"native fits later real Dose after an aliased first predictor "+logistic);
            var row=Block(model,logistic?"parameters":"unscaled").First(b=>b["par"].AsString=="Dose");
            double beta=Num(row,"coef"),half=1.959963984540054*Num(row,"err");
            double[] expected={Math.Exp(beta-half),Math.Exp(beta+half)};
            string rendered=Verify("aliased-first-dose-"+logistic,run,
                Equal("fit$rank,fit$df.residual,df_lr",new[]{Num(model,"rank"),Num(model,"idf"),Num(model,"x2df")})+
                Equal("exp(ci['Dose',])",expected,1e-7)+
                "stopifnot(fit$converged,!fit$boundary,is.na(coef(fit)['Aliased']),all(is.finite(summary(fit)$coefficients)))\n");
            string limits=logistic?@"^Dose odds ratio = [-+0-9.eE]+ +\( *([-+0-9.eE]+) to ([-+0-9.eE]+) *\)":
                @"^Dose +[-+0-9.eE]+ +[-+0-9.eE]+ +([-+0-9.eE]+) to ([-+0-9.eE]+)";
            Match line=Regex.Match(rendered,limits,RegexOptions.Multiline);
            Check(line.Success&&Math.Abs(double.Parse(line.Groups[1].Value,inv)-expected[0])<5.1e-7&&
                Math.Abs(double.Parse(line.Groups[2].Value,inv)-expected[1])<5.1e-7,"rendered Dose Wald limits use the estimable coefficient's row "+logistic);
            Check(Regex.IsMatch(rendered,@"^Aliased \* error: drop this variable \*",RegexOptions.Multiline) &&
                !Regex.IsMatch(rendered,@"^Aliased (?:odds ratio =|b\d+ =|= 1:)",RegexOptions.Multiline),
                "the replay names the aliased predictor without an estimable result "+logistic);
        }

        // No-intercept fits have no intercept-null comparison under the report's
        // convention. Saturated fits still have defined likelihood, deviance and
        // (with an intercept) likelihood-ratio comparisons; residual tests do not.
        foreach(bool intercept in new[]{false,true})
        foreach(bool saturated in new[]{false,true})
        {
            double[] yy=saturated?new[]{2d,5,8,13}:response,xx=saturated?new[]{0d,1,0,0}:dose,tt=Enumerable.Repeat(20d,yy.Length).ToArray(),ww=Enumerable.Repeat(1d,yy.Length).ToArray();
            var sheet=Sheet(("Outcome",yy),("Total",tt),("Prior",ww),("X1",xx),("X2",saturated?new[]{0d,0,1,0}:xx.Select(v=>v*v).ToArray()),("X3",saturated?new[]{0d,0,0,1}:xx.Select(v=>Math.Sin(v)).ToArray()),("X4",saturated?new[]{1d,0,0,0}:xx));
            var answers=LogisticAnswers(sheet,intercept);if(saturated)answers["predictors"]=Select(sheet,intercept?new[]{3,4,5}:new[]{3,4,5,6});
            var(run,output)=Run("LogisticRegression",answers);
            var model=LogisticModel(output.ParameterBag);
            bool saturatedFit=Num(model,"idf")<=0;
            Check((Num(output.ParameterBag,"p")==Constant.MISSING)==saturatedFit,"initial logistic goodness P requires positive residual df");
            if(saturatedFit)Check(output.ParameterBag["w"].AsString=="","initial saturated logistic P has no significance marker");
            Check(double.IsFinite(Num(model,"ll")) && Num(model,"ll")<0 && double.IsFinite(Num(model,"dev")),"logistic likelihood and deviance remain defined "+intercept+"-"+saturated);
            foreach(string key in new[]{"devx","x2dev","r2","r2i"})Check((Num(model,key)==Constant.MISSING)==!intercept,"logistic null comparison availability "+key+"-"+intercept+"-"+saturated);
            Check((Num(model,"p_good")==Constant.MISSING)==saturatedFit && (Num(model,"hl")==Constant.MISSING)==saturatedFit,"logistic residual tests require positive residual df");
            string assertions=Equal("ll,dev",new[]{Num(model,"ll"),Num(model,"dev")})+
                "stopifnot(is.na(lr)=="+(!intercept?"TRUE":"FALSE")+",is.na(r2)=="+(!intercept?"TRUE":"FALSE")+",is.na(r2i)=="+(!intercept?"TRUE":"FALSE")+",is.na(hl)=="+(saturatedFit?"TRUE":"FALSE")+")\n";
            string rendered=Verify("logistic-availability-"+intercept+"-"+saturated,run,assertions);
            Check(rendered.Contains("Deviance (likelihood ratio) chi-square = *")==!intercept,"rendered logistic LR availability follows native contract");
            if(saturatedFit)Check(model["p"].AsObject is string message&&message.Contains("saturated model")&&
                Regex.IsMatch(rendered,@"Pearson chi-square goodness of fit = [^\r\n]*P = \*"),"saturated logistic Pearson P is unavailable in native and R reports");
        }
        foreach(bool intercept in new[]{false,true})
        {
            // The last two original rows have no prior weight; four active
            // rows span a saturated model with fixed Poisson dispersion one.
            var sheet=Sheet(("Outcome",new[]{2d,5,8,13,7,11}),("Prior",new[]{1d,1,1,1,0,0}),("Exposure",Enumerable.Repeat(1d,6).ToArray()),
                ("X1",new[]{0d,1,0,0,1,1}),("X2",new[]{0d,0,1,0,1,1}),("X3",new[]{0d,0,0,1,1,1}),("X4",new[]{1d,0,0,0,1,1}));
            var answers=PoissonAnswers(sheet,intercept,false);answers["predictors"]=Select(sheet,intercept?new[]{3,4,5}:new[]{3,4,5,6});
            var(run,output)=Run("PoissonRegression",answers);
            var model=Run("PoissonRegressionModel",new(),output.ParameterBag).output.ParameterBag;
            Check(Num(model,"idf")==0 && double.IsFinite(Num(model,"ll")) && Num(model,"ll")<0,"saturated Poisson keeps defined likelihood "+intercept);
            Check(Num(output.ParameterBag,"p_dev")==Constant.MISSING,"initial saturated Poisson goodness P is explicitly unavailable");
            Check(output.ParameterBag["w"].AsString=="","initial saturated Poisson P has no significance marker");
            Check(Num(model,"p_dev")==Constant.MISSING && Num(model,"sp")==Constant.MISSING,"saturated Poisson has no residual test or estimated scale");
            foreach(string key in new[]{"devx","x2dev","r2","r2i"})Check((Num(model,key)==Constant.MISSING)==!intercept,"saturated Poisson null comparison availability "+key+"-"+intercept);
            string assertions=Equal("ll,dev,coef(fit),summary(fit)$coefficients[,2]",new[]{Num(model,"ll"),Num(model,"dev")}.Concat(Block(model,"unscaled").Select(b=>Num(b,"coef"))).Concat(Block(model,"unscaled").Select(b=>Num(b,"err"))),1e-7)+
                "stopifnot(fit$df.residual==0L,is.na(scale),is.na(lr)=="+(!intercept?"TRUE":"FALSE")+",is.na(r2)=="+(!intercept?"TRUE":"FALSE")+",is.na(r2i)=="+(!intercept?"TRUE":"FALSE")+")\n";
            string rendered=Verify("poisson-saturated-availability-"+intercept,run,assertions);
            Check(rendered.Contains("Deviance (likelihood ratio) chi-square = *")==!intercept && rendered.Contains("Over-dispersion scale parameter = *"),"rendered saturated Poisson keeps only defined diagnostics");
            Check(Num(model,"p_pearson")==Constant.MISSING&&Regex.IsMatch(rendered,@"Pearson goodness of fit = [^\r\n]*P = \*"),"saturated Poisson Pearson P is unavailable in native and R reports");
        }
    }
}
