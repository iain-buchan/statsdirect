# The Parametric Methods menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Parametric -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take a few seconds.

The reports of the menu are the unpaired, single sample and paired t tests, the t tests from summary statistics, the normal distribution (z) tests of one and of two samples, the F (variance ratio) test, the confidence interval of a Poisson mean, the reference range, and the tests of normality. Each report is given the inputs of a case, and what it gives the report is compared with figures that are worked out from the definitions of the methods.

**The cases of the benchmarks.** `benchmarks/cases.txt` has the cases and `benchmarks/expected.txt` what is expected of them; `benchmarks/make-benchmarks.R` made both, with R 4.6.1 and no packages, in a few seconds. Where the figures are from:

- the t tests, the z tests and the F test: the statistics from the means and variances of the samples, and the P values and confidence limits from the distributions of R; the degrees of freedom of the test with unequal variances are those of Welch and Satterthwaite;
- the confidence limits of a Poisson mean: the means with which the total of the counts or more, and the total or fewer, have half of what the confidence level leaves, found as roots of the Poisson probabilities, which shares nothing with the chi-square values of the program;
- the reference range: the formulas of the normal and the log-normal range with the standard errors of their ends, and the quantiles as the values at the places q (n + 1) of the sample in order;
- normality: the skewness and the kurtosis from the moments about the mean, with the tests of each written from their formulas; the W of Shapiro and Wilk and its P value as R has them; the W' of Shapiro and Francia as the squared correlation of the sample in order with the normal scores of its places.

The samples are twelve, of 2 to 500 values: near 0 and far from it (values of 100,000,000 that differ by 2, and of a million million that differ by 100,000), wide and narrow (a spread of a millionth, values of 1e-150), whole numbers, and one with an outlier; with missing values in the paired tests; at confidence levels of 80%, 95% and 99%.

**Values far from 0 and near one another** are where a sum of squares that is formed from raw sums loses its figures, and where a benchmark does too: the moments and W are worked out here of the values less the first of them, which is exact. When the checks were first made the program was right for such a sample and the benchmark was off in the eighth figure.

**The ends of what can be entered, and what is refused,** are among the cases: a confidence level that is not above 0 and below 1, for which 0.95 is taken; two columns of different length in the paired t test; values of 0 and below, which have no geometric mean; values that are all the same; a sample of one value; counts that are not whole numbers; standard deviations of 0 or below and samples of fewer than two in the summary tests, fewer than 8 values and a reference interval that is not above 0% and below 100% in the reference range, which are refused with words that are to be in the message.

**What these checks showed when they were written.** The z tests gave P = 0 for a P value below 1e-16. The reference range of log-normal data in the single sample z test was wrong for values far from 1 and near one another, and the geometric mean left out values of 0 and below without a word. The t tests and z tests took a confidence level of 0% or 100% as it is. A standard deviation below 0 was taken by the summary tests. Two columns of different length stopped the paired t test.

**What is not checked here.** The reports as they are shown, the power that the t tests print (the routines of power are checked against their definitions in the work on the Sample Size menu), the confidence limits of the quantiles of the reference range (which belong to the nonparametric methods), the test of skewness and kurtosis together, and the plots.

The run is the same every time. The exit code is 0 if every check passes.
