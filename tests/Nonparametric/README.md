# The Nonparametric menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Nonparametric -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about fifteen seconds. A part can be run alone: `dotnet run --project tests/Nonparametric -c Release -- benchmarks` (or `shuffles`, `simulated`, `series`).

The reports of the menu are the Mann-Whitney test, the Wilcoxon signed ranks test, Kendall's and Spearman's rank correlation, nonparametric linear regression, Cuzick's test for trend, the Smirnov test, the confidence interval of a quantile, the Kruskal-Wallis test with its comparisons and the squared ranks test, the Friedman and Cochran Q tests with their comparisons, the Gini coefficient and the diversity indices. Each report is given the inputs of a case, and what it gives the report is compared with figures that are worked out from the definitions of the statistics.

**The cases of the benchmarks.** `benchmarks/cases.txt` has the cases and `benchmarks/expected.txt` what is expected of them; `benchmarks/make-benchmarks.R` made them, with R 4.6.1 and no packages. Where the figures are from:

- the Mann-Whitney test: U is the count of the pairs in which the first sample's value is the greater, ties counting a half; the exact P values are from every way of choosing which of the pooled values are the first sample's, counted over the doubled ranks so that ties are whole numbers, or, without ties, from the distribution of U; the interval of the difference is of the differences of every value of one sample from every value of the other, in order; the limits of theta are the roots of Newcombe's fifth method;
- the Wilcoxon signed ranks test: the exact P values from every assignment of signs to the differences that are not 0, or from the distribution of the statistic without ties; the interval from the averages of every two differences;
- Kendall's correlation: the counts of pairs, the variance of the score with the correction for ties, tau b, the limits, the P values from the normal deviate with and without the continuity correction, and the exact P values from the distribution of the score without ties, which is that of the number of discordant pairs of a random order (the j-th thing is put among the j - 1 before it in one of j places, each equally likely);
- Spearman's correlation: rho as the correlation of the ranks; the exact P values from every order of the second variable's ranks (up to 9 pairs); with ties, from Student's t;
- nonparametric regression: the median of the slopes of every two points with different x, the intercept through the medians, the limits from the distribution of Kendall's score;
- Cuzick's test: T, its expectation and variance, and the correction for ties, from their definitions;
- the Smirnov test: the greatest differences of the two empirical distribution functions, and the exact P values from every way of choosing which of the pooled values are the first sample's;
- the quantile interval: the order statistics whose binomial probabilities are nearest to the tails asked for, or, conservative, at least as far out;
- the Kruskal-Wallis test and the squared ranks test: from the definitions, with the correction for ties; the comparisons: the Dwass-Steel-Critchlow-Fligner test with the range of means with degrees of freedom without end (found here by integration of its density, with the room that the program's series has, a part in two million of the probability) and the Conover-Iman test from Student's t;
- the Friedman and Cochran Q tests and their comparisons: from Conover's definitions of T1, T2 and the comparisons;
- the Gini coefficient from the values in order, with weights repeating a value, and the diversity indices with their large sample standard errors and the estimate of the classes not seen.

The bootstrap figures of the Gini coefficient and the diversity indices are not checked: the program draws them from the clock, so that they differ in every run.

**The ends of what can be entered, and what is refused,** are among the cases: a sample of one value, values that are all the same, samples of more than 100 values (the normal approximation), 40 and 35 values with ties (the exact P from the count of the ways), a deviate of 8.6 (a P of 4e-18), a confidence level of 0% or 100% (95% is taken), a confidence of 0.05 for the comparisons (a 5% test), classes with equal counts, observations that are the same within every block of the Friedman test and a single block (refused with words that are to be in the message), one value in a column, missing values.

**The shuffles of the simulated exact P values** are checked for every order being equally likely: each shuffle is made 60,000 times from one seed with 3 values (240,000 with 4), and the counts of the orders are compared with equal counts by chi-square. The exchange of each place in turn with a place drawn from all the places, which the program used to make, gives 3 values counts of about 8,889 and 11,111 for 10,000, and fails.

**The simulated exact P values** of the Kruskal-Wallis and Friedman tests are compared with the exact P found by the count of every arrangement (1,260 ways of giving 9 values to groups of 3, 2 and 4; 1,296 orders within 4 blocks of 3; 46,656 for Cochran's Q with 6 blocks): the program's P from 200,000 draws with a seed is to be within 4.5 standard errors of it.

**The distribution of Kendall's score** is checked at 1000 observations, the most for which the program counts the orders, against a count made here by the definition (within 1e-12 at every score, and the count of the program in under 2 seconds), and at 1001, where the program uses a series (within 1e-6 at every score, and within a thousandth of itself for a P from 0.001 to 0.05).

**What these checks showed when they were written.** A P value that is a small tail of the normal distribution was found as 1 less the other tail, which is 0 below 1e-16 (Cuzick's test with a deviate of 8.6). The confidence limits of the Mann-Whitney and Wilcoxon tests had 5 decimal places at most. The second Mann-Whitney sample of one value was given the median of the first sample. The P values called exact of Kendall's correlation were from a series above 50 observations, off by 2e-7 with 60. "Numbers are small" was shown after the Friedman test whenever P was 0.05 or less. The simulated exact P values were made with a shuffle in which the orders are not equally likely. Classes with equal counts had no standard errors of the diversity indices. A confidence level of 0% or 100% was taken in eight ways, and the Kruskal-Wallis comparisons took a confidence of 0.05 as the 5% point of the range.

**What is not checked here.** The reports as they are shown and the charts; the bootstrap figures; the LOESS and ROC operations, which are not routines of this file.

The run is the same every time. The exit code is 0 if every check passes.
