# The Analysis of Variance menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/AnalysisOfVariance -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about ten seconds.

The reports of the menu are the one way, two way (randomized blocks), replicated two way, fully nested and Latin square analyses of variance, the crossover trial, the comparisons that follow an analysis (Bonferroni, Tukey, Scheffe, Newman-Keuls and Dunnett), the equality of variance tests, and the means of a nested analysis. Each report is given the inputs of a case, and what it gives the report is compared with figures that are worked out from the definitions of the methods. A report that follows an analysis is given what that analysis caches for it, as the program gives it.

**The cases of the benchmarks.** `benchmarks/cases.txt` has the cases and `benchmarks/expected.txt` what is expected of them; `benchmarks/make-benchmarks.R` made them, with R 4.6.1 and no packages. Where the figures are from:

- the analyses of variance: the sums of squares from the deviations about the means (between groups, within groups, rows, columns, interaction, residual within the cells or by subtraction where the design has no repeats), their degrees of freedom, the variance ratios and the P values from F; a missing repeat of the replicated two way analysis replaced by the mean of its cell with a degree of freedom taken away, as the program does; the Latin square from the totals of rows, columns and treatments; the crossover trial from the differences and the sums of each subject's two periods (the relative effect, the treatment effect with its interval, the period effect and the treatment-period interaction, each by Student's t);
- the comparisons: Bonferroni from Student's t at the level and at the level adjusted for the number of comparisons; Tukey and Newman-Keuls from the studentized range, whose distribution is found by integration of the density of the range of normal variates over the distribution of the residual standard deviation, and its point by the root (the program has the same from a series that is within 5e-7 in probability, which is the room given to a P, and to a q and its limits through the density); Scheffe from F; Dunnett from the distribution of the greatest of the correlated statistics, by a double integration over the common part of the statistics and the residual standard deviation (the program's critical values agree with it to a part in a hundred million); the order of the differences, the stop marker, the Newman-Keuls step-down and the summary of significant differences as the program makes them;
- the equality of variance tests: Levene's test on the absolute differences from the medians, Bartlett's test, and Welch's analysis of variance for unequal variances, from their definitions.

The cases are the examples of the help and others: unequal group sizes, missing values (a row with one left out of the two way analysis, a subject left out of the crossover trial, a repeat replaced in the replicated analysis), values far from 0 (about a million, differing by units), two groups, negative values, a baseline in the crossover trial, codes of a Latin square in any values and any order, confidence levels of 80%, 90%, 99%, and of 0% and 100%, for which 95% is taken, a confidence of 0.05 taken as a 5% test, the comparisons after a randomized blocks analysis as well as after a one way one.

**The ends of what can be entered, and what is refused,** are among the cases, with the words that are to be in the message: a group without observations, one group, groups of one observation (no residual degrees of freedom), observations that are all the same, a residual sum of squares of 0, fewer than two rows or columns, too few blocks for a Latin square, Newman-Keuls with unequal sizes, and the comparisons without a residual degree of freedom.

**What these checks showed when they were written.** A confidence level of 0% or 100% gave the Bonferroni comparisons an interval without end or width, the Tukey comparisons a critical range of 0 with every pair significant, and stopped the crossover trial. The residual sums of squares of the replicated and the nested analyses, and the variances of the crossover trial, lost figures when the values are large beside their spread (a t off by a part in a million). The one way analysis gave asterisks, and the two way ones "Invalid calculation", for what cannot be analysed. The critical values of Dunnett's method and the P values of the studentized range agreed with the integrations.

**What is not checked here.** The reports as they are shown and the charts; the grouped one way and two way analyses of the Regression and Correlation menu, which split a column by a group column and call the same routines; the analysis of agreement, which is checked in `tests/Agreement`.

The run is the same every time. The exit code is 0 if every check passes.
