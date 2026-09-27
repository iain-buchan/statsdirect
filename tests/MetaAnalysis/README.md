# The Meta-analysis menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/MetaAnalysis -c Release
```

The checks are made on a build of the program, as those of `tests/CoxRegression` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take a few seconds. One part can be run alone by naming it: `bin`, `other` or `limits`.

Sets of studies are put through the ten reports of the menu, and every figure of each report that has a benchmark is compared with it: the estimate, limits and weights of each study, the pooled estimates with their limits and tests, Cochran's Q, I-squared and its limits, the variance between the studies, and the bias indicators. A figure may differ from its benchmark by one part in ten million; a limit of I-squared by the exact method by one in a million, which is what the search for it is good to.

**The benchmarks** (`benchmarks`) are figures of R 4.6.1 with the packages meta 8.5.0 and metafor 5.2.1, and figures worked out in R from the distributions themselves. The scripts that made them are beside them, and make them again:

```
Rscript --vanilla cases-bin.R cases-bin.txt
Rscript --vanilla cases-other.R cases-other.txt
Rscript --vanilla figures-bin.R cases-bin.txt r-bin.txt
Rscript --vanilla figures-other.R cases-other.txt r-other.txt
Rscript --vanilla figures-exact.R cases-bin.txt x-bin.txt
Rscript --vanilla figures-exact.R cases-other.txt x-other.txt
```

A file of cases has a line `case`, name, kind, number of studies and confidence level for each set, and then a row for each study. A file of figures has a key and a figure on each line; the key is the name of the set, the setting, the analysis and the name that the report gives the figure. The settings are those of "Calculation Options": `x1` or `x0` for the exact methods on or off, `tacc` or `cc5` for the treatment arm continuity correction or 0.5, `d1` or `d0` for the continuity correction delayed or not.

**Odds ratio, Peto odds ratio, relative risk and risk difference** (`cases-bin.txt`, 49 sets under four settings): sets of 1 to 10 studies, small and large; sparse sets with empty cells; sets in which a whole group has no events, or events in every subject; studies without an event in either group, with an event in every subject, of one subject, and with a group of nobody. The Mantel-Haenszel chi-square is R's `mantelhaen.test`, and the statistic of Breslow and Day is worked out in the script.

**Incidence rates, effect size, proportion, correlation and summary** (`cases-other.txt`): 18 drawn sets of each kind and sets at the limits (no events, equal rates, studies that are all the same, correlations near 1, proportions of 0 and of 1).

**From the distributions themselves** (`figures-exact.R`): the conditional (Fisher) limits of the odds ratio of each study, and the pooled odds ratio and rate ratio by conditional maximum likelihood with their exact and mid-P limits and P values, from the distribution of the total given the margins of every table; the score limits of the relative risk (Koopman) and of the risk difference (Miettinen and Nurminen); the limits of g from the non-central t distribution; the exact limits of the rate ratio. R's `mantelhaen.test(exact = TRUE)` is not used as a benchmark: its estimate and limits are found by a search that is good to four figures only.

**Where the program is not compared with a function of R**, and why:

- Rank correlation (Begg and Mazumdar): the P value is exact when there are no ties, as that of R's `cor.test` is; meta's `metabias` gives the P value of a normal deviate. Values that differ by rounding alone are tied for the program; the script rounds them to 12 figures first.
- Egger's indicator in the report of the Peto odds ratio is made from the Peto odds ratios; meta makes it from the odds ratios pooled by inverse variance.
- The indicator of Harbord and Egger for the relative risk is made from the score and its variance with the sizes of the groups as they are; meta has N / 4 in the place of n1 n2 / N, which is the same only if the groups are of one size. The benchmark is a regression made in the script.
- The standardised mean difference has the variance n / (n1 n2) + d^2 / (2 n), as metafor has; meta has another.
- The method of Schmidt and Hunter has the variance of sampling error from the mean number of subjects; metafor has it from the number of each study. The benchmark is worked out in the script, and the mean correlation is metafor's.
- A proportion pooled from studies without an event is given as 0, with a lower limit of 0 (and the other way about): a rule of the program, which the script has too.

**At the limits** (`Limits.cs`): studies that cannot be pooled are refused with a message that says why; the studies that are left out of the pooling are marked; Sato's lower limit, when the pooled odds ratio is infinite, is compared with its definition; and the standard errors that the bias assessment plots of the risk difference and of the incidence rates are given, with the exact methods, are compared with theirs.

The run is the same every time. The exit code is 0 if every check passes.
