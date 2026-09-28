# The Exact Tests on Counts menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/ExactTests -c Release
```

The checks are made on a build of the program, as those of `tests/Crosstabs` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about a quarter of a minute. One part can be run alone by naming it: `benchmarks`, `definitions` or `limits`.

The analyses are the sign test, Fisher's exact test, the expanded Fisher-Irwin test, the test of matched pairs (McNemar, and the exact test after Liddell), the exact confidence interval of the odds ratio of a 2 by 2 table, and the confidence interval of a Poisson rate. The analysis of an r by c table, which the menu has too, is checked in `tests/Crosstabs`.

**The cases of the benchmarks** (`benchmarks`). 718 cases: sign tests of 1 to 1,000,000 observations, with none, all and half of them on one side; 206 tables of 2 to 40,000 subjects, some with the same totals both ways, some with empty cells, rows or columns, and some with counts that are not whole numbers, each for Fisher's exact test, the expanded test and the odds ratio; 73 sets of matched pairs; and Poisson rates of 0 to 100,000,000 events, some of them not whole numbers. 43,572 figures are compared with their benchmarks, from which a figure may differ by one part in ten million. The benchmarks are worked out in R 4.6.1, without packages, from the definitions, by the scripts that are beside them:

```
Rscript --vanilla cases-exact.R cases-exact.txt
Rscript --vanilla figures-exact.R cases-exact.txt r-exact.txt
```

- The P values of the sign test and of matched pairs are sums of binomial probabilities, and the limits of a proportion are quantiles of the beta distribution.
- The P values of Fisher's exact test are sums of hypergeometric probabilities; the two sided P value is the sum of the probabilities of the tables that are no more probable than the table observed, a table being taken to be no more probable if its probability is within one part in ten million of that of the table observed.
- The estimate of the odds ratio is the odds ratio with which the first count has its observed value as its mean, and each limit is the odds ratio with which a tail has the probability that is wanted; they are found by a search for the root, with the probabilities of the first count worked out on the scale of logarithms.
- The limits of a Poisson rate are quantiles of the gamma distribution.

A file of cases has on each line the kind of the analysis, a name and what the analysis is given. A file of figures has a key and a figure on each line.

**Cases drawn at random, against the definitions** (`definitions`, `Definitions.cs`). The same figures are worked out in the tests, by another route than that of the benchmarks: probabilities are added up from the logarithms of factorials, which are made by adding logarithms, and each limit is found by bisection of the sum of probabilities that defines it. Nothing of the program is used to make what is expected. About 2,600 tables are put through Fisher's exact test and the expanded test (for all but the largest, every row of the expanded test is compared); 900 tables through the exact odds ratio; 700 sign tests, of up to 20,000 observations; 700 sets of matched pairs, of up to 20,000 pairs that differ; and 400 Poisson rates, of up to 200,000 events. A figure may differ from what is expected by one part in a hundred million (one in a thousand million for Fisher's exact test).

**At the limits** (`limits`). Counts that are not whole numbers are rounded, a half to the even number, and the reports are those of the counts rounded. Fisher's exact test is not made for a table with an empty row or an empty column; the odds ratio of such a table has limits of 0 and infinity and P values of 1. The report of the odds ratio says when a table is too large for the exact method. The one sided P value of the odds ratio is that of Fisher's exact test, for tables whose smaller tail is as small as 1e-20.

**What is not checked here.** The reports as they are shown. The P values of the two chi-square statistics of matched pairs are compared with the benchmarks only.

The run is the same every time. The exit code is 0 if every check passes.
