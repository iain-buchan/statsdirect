using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.R;

internal static partial class Program
{
    private static void CheckRecipeGeneralSimplicity()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-general-simplicity-" + Guid.NewGuid().ToString("N"));
        int number = 0;
        void Probe(string description, string script, string assertions)
        {
            string result = RunR("options(device=function(...) grDevices::svg('plot-%03d.svg',...))\n"
                + "near <- function(a,b) isTRUE(all.equal(as.numeric(a),as.numeric(b),tolerance=1e-9,check.attributes=FALSE))\n"
                + script + "\n" + assertions + "\ncat('GENERAL_OK\\n')\n", Path.Combine(folder, (++number).ToString()));
            Check(result.Contains("GENERAL_OK"), description);
        }
        string Recipe(string file, string settings) => settings + "\n" + RRecipes.Recipe(file);
        string Example(string operation, params (string key, object value)[] settings)
        {
            JsonObject example = JsonNode.Parse(File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".json"))).AsObject();
            JsonObject values = example["settings"].AsObject();
            foreach (var setting in settings) values[setting.key] = JsonSerializer.SerializeToNode(setting.value);
            using JsonDocument document = JsonDocument.Parse(example.ToJsonString());
            using JsonDocument record = JsonDocument.Parse(RecordOf(document.RootElement));
            return RScriptWriter.Write(record.RootElement, null, null).Text;
        }

        foreach (double level in new[] { .8, .95, .99 })
        {
            string confidence = level.ToString("R", inv);
            Probe("single-proportion exact and Wilson intervals use " + level,
                Recipe("single-proportion.R", $"n<-37;r<-11;pi0<-.4;confidence<-{confidence}"), """
                alpha <- (1-confidence)/2
                expected <- c(qbeta(alpha,r,n-r+1),qbeta(1-alpha,r+1,n-r))
                z <- qnorm(1-alpha); p <- r/n
                centre <- (p+z*z/(2*n))/(1+z*z/n)
                half <- z*sqrt(p*(1-p)/n+z*z/(4*n*n))/(1+z*z/n)
                stopifnot(near(ci,expected),near(wilson,centre+c(-1,1)*half))
                """);
            Probe("McNemar exact interval uses " + level,
                Recipe("mcnemar.R", $"counts<-matrix(c(21,6,13,17),2);confidence<-{confidence}"),
                "stopifnot(near(exact$conf.int,c(qbeta((1-confidence)/2,b,cc+1),qbeta((1+confidence)/2,b+1,cc))))");
            Probe("standardized-rate component intervals use " + level,
                Recipe("stdrr.R", $"a<-c(4,7,12);b<-c(3,5,9);pt1<-c(100,140,180);pt2<-c(160,210,240);ref<-NULL;strata<-NULL;per<-1000;confidence<-{confidence}"), """
                events <- sum(a); exposure <- sum(pt1)
                expected <- c(events/exposure,qchisq((1-confidence)/2,2*events)/(2*exposure),qchisq((1+confidence)/2,2*(events+1))/(2*exposure))*per
                stopifnot(near(e,expected),near(attr(crude$conf.int,'conf.level'),confidence))
                stopifnot(all(vapply(seq_along(a),function(i) near(exact[,i],c(
                  pt2[i]/pt1[i]*a[i]/(b[i]+1)/qf((1+confidence)/2,2*(b[i]+1),2*a[i]),
                  pt2[i]/pt1[i]*(a[i]+1)/b[i]*qf((1+confidence)/2,2*(a[i]+1),2*b[i]))),logical(1))))
                """);
            Probe("time-series bootstrap uses " + level,
                Recipe("time-series-summary.R", $"time<-rep(0:2,8);patient<-rep(1:8,each=3);zidovudine<-unlist(lapply(1:8,function(i)c(0,i,i+1)));group<-NULL;zero_at_start<-FALSE;confidence<-{confidence}"),
                "stopifnot(near(bootstrap_ci,mean(auc)+quantile(tstar," + (level == .8 ? "c(.1,.9)" : level == .95 ? "c(.025,.975)" : "c(.005,.995)") + ",type=2,na.rm=TRUE)*se))\n"
                + (level == .95 ? "" : "stopifnot(!near(bootstrap_ci,mean(auc)+quantile(tstar,c(.025,.975),type=2,na.rm=TRUE)*se))"));
        }

        Probe("weighted summaries sort paired weights and omit missing or zero-weight rows",
            Recipe("weighted-univariate-summary.R", "value<-c(100,NA,1,2,3,99,6);weight<-c(1,5,1,1,1,0,NA);confidence<-.9"),
            "stopifnot(n==4,near(m,26.5),near(v,var(c(100,1,2,3))),centile(.5)==2.5,centile(.25)==1.5,centile(.75)==51.5)");
        Probe("weighted centiles retain scaled-weight and exact-boundary conventions",
            Recipe("weighted-univariate-summary.R", "value<-c(9,1,5,3);weight<-c(1,2,3,2);confidence<-.99"),
            "stopifnot(near(sum(w),4),near(m,4),near(v,8),centile(.25)==2,centile(.5)==4,centile(.75)==5)");

        Probe("a singleton integer series remains the chosen integer",
            Recipe("random-series.R", "x<-6;y<-6;seed<-10"), "stopifnot(identical(as.integer(series),6L))");
        Probe("a singleton discrete uniform range stays at its bound",
            Recipe("random-uniform-ab.R", "rows<-5;cols<-2;a<-6;b<-6;kind<-'integer';seed<-10"), "stopifnot(all(x==6),identical(dim(x),c(5L,2L)))");
        foreach (bool balance in new[] { false, true })
            Probe("pair randomization honors recorded balance=" + balance,
                Example("RandomPairs", ("balance", balance), ("pairs", 12)),
                $"stopifnot(identical(balanced,{(balance ? "TRUE" : "FALSE")}),length(allocation)==12)\n"
                + (balance ? "stopifnot(all(table(allocation)==6))" : "set.seed(seed);stopifnot(identical(allocation,sample(orders,pairs,replace=TRUE)))"));
        Probe("odd pair counts use independent allocation rather than truncating the table",
            Example("RandomPairs", ("balance", true), ("pairs", 5)), "stopifnot(!balanced,length(allocation)==5)");
        Probe("fixed blocks use the recorded block size and a balanced shorter final block",
            Example("RandomBlock", ("n", 22), ("b", 6)), "stopifnot(identical(as.numeric(sizes),c(6,6,6,4)),length(allocation)==22,all(table(allocation)==11))");
        Probe("blank block size selects one random-block allocation",
            Example("RandomBlock", ("b", null)), "stopifnot(random_size,sum(sizes)==n,all(sizes%%t==0),length(allocation)==n)");

        foreach (int[] cells in new[] { new[] { 65, 16, 6, 13 }, new[] { 0, 7, 9, 4 }, new[] { 7, 3, 3, 7 }, new[] { 10, 1, 1, 10 } })
            Probe("hypergeometric tails preserve Fisher and mid-P conventions for " + string.Join(",", cells),
                Recipe("fisher-exact.R", "teeth<-matrix(c(" + string.Join(",", cells) + "),2,byrow=TRUE)"), """
                support <- max(0,k-n):min(k,m); masses <- dhyper(support,m,n,k)
                expected_tail <- if(a>expected)sum(masses[support>=a]) else sum(masses[support<=a])
                stopifnot(near(one_sided,expected_tail),near(two_sided,sum(masses[masses<=observed*(1+1e-7)])),near(mid_p,expected_tail-observed/2))
                """);
        foreach (int[] events in new[] { new[] { 4, 12 }, new[] { 0, 9 }, new[] { 10, 0 } })
            Probe("conditional rate tails retain exact and tightly inverted mid-P limits for " + string.Join(",", events),
                Recipe("rate-compare-two.R", $"a<-{events[0]};b<-{events[1]};pt1<-730;pt2<-810;confidence<-.8"), """
                masses <- dbinom(0:m,m,pt1/pt)
                stopifnot(near(up,sum(masses[(a+1):(m+1)])),near(down,sum(masses[1:(a+1)])),near(two,sum(masses[masses<=observed*(1+1e-7)])))
                if(a>0)stopifnot(abs(mid_tail(lower_mid*pt1/(lower_mid*pt1+pt2),FALSE)-(1-confidence)/2)<1e-8)
                if(b>0)stopifnot(abs(mid_tail(upper_mid*pt1/(upper_mid*pt1+pt2),TRUE)-(1-confidence)/2)<1e-8)
                """);
        Probe("Miettinen score inversion remains while mid-P uses standard hypergeometric tails",
            Recipe("proportions-unpaired.R", "n1<-37;r1<-11;n2<-42;r2<-20;confidence<-.9"), """
                support <- max(0,n1-(N-r1-r2)):min(n1,r1+r2); masses<-dhyper(support,r1+r2,N-r1-r2,n1)
                stopifnot(near(lower_tail,sum(masses[support<=r1])-observed/2),near(upper_tail,sum(masses[support>=r1])-observed/2),abs(score(lower)-z)<1e-7,abs(score(upper)+z)<1e-7)
                """);
        foreach (double level in new[] { .8, .99 })
        {
            Probe("regression correlation power remains defined at 5% for confidence " + level,
                Recipe("simple-linear-regression.R", $"increase<-c(8,7,10,12,10,15,14,18);weight<-1:8;confidence<-{level.ToString("R", inv)}"),
                "stopifnot(near(power,pnorm(z-qnorm(.975))+pnorm(-z-qnorm(.975))),near(attr(ct$conf.int,'conf.level'),confidence))");
            Probe("unpaired t power remains defined at 5% for confidence " + level,
                Recipe("unpaired-t.R", $"high<-c(8,10,12,9,14,11,15,10,13,16);low<-c(7,9,8,11,6,12,9,7,10,8);confidence<-{level.ToString("R", inv)}"), """
                pooled <- sqrt((var(high)+var(low))/2)
                expected <- power.t.test(n=length(high),delta=mean(high)-mean(low),sd=pooled,sig.level=.05,type='two.sample',strict=TRUE)$power
                printed <- capture.output(report(equal,'equal'))
                stopifnot(any(grepl(sprintf('%.2f%%',100*expected),printed,fixed=TRUE)))
                """);
            Probe("Gini bootstrap intervals honor selected confidence " + level,
                Recipe("gini.R", $"imr<-c(2,3,5,8,11,16,20,31,39,51);weights<-NULL;boots<-400;seed<-2001;confidence<-{level.ToString("R", inv)}"),
                "stopifnot(near(ci$percent[1],confidence),near(ci$bca[1],confidence))");
        }

        // The changed worked examples retain independent data and mathematical references.
        // Check them here as well as in the complete recipe suite, so this focused mode
        // verifies that removing teaching output did not change the selected answers.
        HashSet<string> changed = new(StringComparer.Ordinal)
        {
            "chi-woolf-screen.R", "chi-woolf-worksheet.R", "crosstabs.R", "false-results.R", "fisher-exact.R",
            "follow-up-life-table.R", "gini.R", "mcnemar.R", "proportions-unpaired.R", "r-by-c.R",
            "random-blocks.R", "random-pairs.R", "random-series.R", "random-uniform-ab.R", "rate-compare-two.R",
            "rate-direct-screen.R", "rate-direct.R", "replicate-two-way.R", "simple-linear-regression.R", "single-proportion.R",
            "single-sample-t.R", "size-paired-t.R", "size-survival.R", "size-unpaired-t.R", "stdrr.R", "time-series-summary.R",
            "two-way.R", "unpaired-t.R", "weighted-univariate-summary.R"
        };
        foreach (string path in Directory.GetFiles(Metadata("Examples"), "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement example = document.RootElement;
            if (!changed.Contains(example.GetProperty("file").GetString())) continue;
            string operation = example.GetProperty("operation").GetString();
            using JsonDocument record = JsonDocument.Parse(RecordOf(example));
            string script = RScriptWriter.Write(record.RootElement, null, null).Text;
            string actual = Figures(RunR(script, Path.Combine(folder, "examples", operation)));
            if (example.TryGetProperty("nocompare", out JsonElement nc) && nc.ValueKind == JsonValueKind.True)
            {
                Check(actual.Length > 0, operation + " selected worked example runs (random values checked separately)");
                continue;
            }
            string expected = Figures(RunR(File.ReadAllText(Path.ChangeExtension(path, ".R")), Path.Combine(folder, "reference", operation)));
            bool cut = example.TryGetProperty("cut", out JsonElement c) && c.ValueKind == JsonValueKind.True;
            bool subset = example.TryGetProperty("subset", out JsonElement sb) && sb.ValueKind == JsonValueKind.True;
            HashSet<string> expectedFigures = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            Check(actual == expected || (cut && actual.Length > 0 && expected.StartsWith(actual + " ", StringComparison.Ordinal))
                || (subset && actual.Length > 0 && actual.Split(' ').All(expectedFigures.Contains)),
                operation + " selected worked-example figures match its independent reference");
        }
    }
}
