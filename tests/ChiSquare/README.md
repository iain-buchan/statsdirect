# The Chi-square Tests menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/ChiSquare -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about 40 seconds. One part can be run alone by naming it: `benchmarks`, `large`, `definitions`, `simulations`, `moved` or `limits`.

The analyses are the 2 by 2 chi-square test, with the odds ratio of a case-control study and the risk ratio of a cohort study; the 2 by k chi-square test, with its test for trend and the simulated exact P value of that test; the test of Mantel and Haenszel of tables that are typed; and Woolf's analysis of a series of tables. Of the r by c analysis the simulated exact P values are checked here, and the rest in `tests/Crosstabs`; the test of matched pairs is checked in `tests/ExactTests`, kappa and Maxwell's test in `tests/Agreement`, and the odds ratio meta-analysis, which is the test of Mantel and Haenszel of tables from a worksheet, in `tests/MetaAnalysis`.

**The cases of the benchmarks** (`benchmarks`). 1,286 cases: 189 tables of 4 to 2,000 million subjects, each as a case-control study, a cohort study and neither, some with empty cells, with an empty row or column, or with counts that are not whole numbers; 107 tables of 2 to 30 rows, each without the test for trend, with the scores 1 to k and with scores that are given, some with no successes, a row of nothing, scores that are the same, or counts that are not whole numbers; 19 tables for the simulation; and 72 series of 1 to 20 tables, each for the test of Mantel and Haenszel with and without the exact methods and for Woolf's analysis of tables that are typed and of tables from a worksheet. 72,922 figures are compared with their benchmarks, from which a figure may differ by one part in ten million. The benchmarks are worked out in R 4.6.1, without packages, by the scripts that are beside them:

```
Rscript --vanilla cases-chi.R cases-chi.txt
Rscript --vanilla figures-chi.R cases-chi.txt r-chi.txt
```

- Chi-square is the sum over the cells of the squared difference of the observed and the expected count over the expected count; with the correction for continuity a half is taken from each difference first. V is the correlation of the row and the column over the subjects.
- The limits of Woolf of an odds ratio are those of the logistic regression of the outcome on the group. The exact limits and P values are from the distribution of the first count with the totals of the table given, as in `tests/ExactTests`.
- The limits of the risk ratio are the ratios at which the score statistic has the value of chi-square for the confidence level, and those of the risk difference likewise, with the proportions that are most likely for the difference from the formula for the root of a cubic. The attributable risk is the share of the outcomes that would not have been with the risk of those without the characteristic, and its variance is from the variances and covariances of the four counts.
- The chi-square for trend is the sum of squares of the weighted regression of the proportions on the scores.
- The benchmark of a simulated P value is the probability of the tables whose chi-square for trend is no less than that of the table observed, from every table that has the totals of the table observed. A simulated P value may differ from it by 5 standard errors of a share of the tables drawn.
- The mean of the logarithms of the odds ratios of Woolf's analysis is the fit of a constant by weighted least squares, and the chi-square for heterogeneity is what that fit leaves.
- The chi-square of Mantel and Haenszel is that of R's test, and so are the pooled odds ratio and its limits for the series without an empty cell; with an empty cell the program has a continuity correction, which `tests/MetaAnalysis` checks.

A file of cases has on each line the kind of the analysis, a name and what the analysis is given. A file of figures has a key and a figure on each line.

**Fisher's exact test of large tables** (`large`). 187 tables of 3,000 to 2,000 million, with the first count from 37 standard deviations below what is expected to 36 above it, among them tables with the same totals both ways and tables with a row or a column of few. The P values are compared with sums of hypergeometric probabilities of R (`cases-fisher-large.R`, which makes the cases and the figures), from which they may differ by one part in a thousand million. No table is to take 2 seconds.

**Cases drawn at random, against the definitions** (`definitions`, `Definitions.cs`). The figures are worked out in the tests, by another route than that of the benchmarks and than that of the program. Nothing of the program is used to make what is expected.

- 1,188 tables of 2 by 2: chi-square as the sum over the cells, the two coefficients, and the limits of Woolf. A P value is from the incomplete gamma function, by its series and its continued fraction, and the normal deviate of a confidence level by bisection.
- 1,191 tables of 2 by k, of up to 20 million in a cell: chi-square as the sum over the rows of the squared difference of the proportion of the row from the proportion of all the rows; the chi-square for trend from the sum of the products of those differences and the differences of the scores from their mean; and what is left.
- 59 tables for the simulated P value of the test for trend, each against every table with the same totals.
- 600 series of 1 to 8 tables: Woolf's analysis as a weighted mean, with the chi-square for heterogeneity as a sum of squares about the mean; the chi-square of Mantel and Haenszel from the expectation and the variance of the first count of each table, and the pooled odds ratio with its limits; and every figure of the tables that are typed against the figure of the same tables from a worksheet, with and without the exact methods.

**The simulated exact P values** (`simulations`, `Simulations.cs`). Every table that has the totals of a table is gone through, with its probability.

- The tables that are drawn: for 8 sets of totals, of which some have columns or rows of 1, every table that is drawn is to have the totals, and each table is to be drawn as often as its probability says.
- The test for trend of 60 tables with the same total in every row, which have the chi-square of their mirror image, with the scores 1 to k and with scores that are not whole numbers.
- The four simulated P values of the r by c analysis (chi-square, G-square, trend, equality of the mean scores) of 32 tables, with the scores 1 to k and with scores that are not whole numbers; and none for a test that there is not.

**Scores that are moved and stretched, and tables of many subjects** (`moved`, `Moved.cs`). A test that has scores is the same whatever number is added to all the scores, and whatever number they are multiplied by; and the tables of a simulation that are counted are those whose statistic is no less than that of the table observed, however many subjects there are.

- 40 tables of the r by c analysis and 30 of the 2 by k test, with scores that are whole numbers, in eighths and in tenths, each with 10 sets of scores made from its own: with 1,000, 1,000,000, 1,000 million, a million million and -1,000,000 added, times 0.001, 7, 1,000,000 and -1, and with 1,000,000 added and times 1,024. The simulated P values are to be the same, to the last figure, as with the scores as they are, from the same seed; and the chi-squares for trend and for the equality of the mean scores, the correlation, and what is left of the chi-square of the 2 by k test are to be the same to 9 figures. A score in tenths is not held as it is typed, and with a number added to it the part that is lost is greater: the scores are moved as decimal numbers are, so that 0.7 and 1,000,000 make the number that 1000000.7 is held as.
- 10 tables of 2 by 2 with 2,000 to 5 million subjects, of which the first count is 1 to 700 from what is expected. Chi-square, the chi-square for trend and that for the equality of the mean scores put the tables with the totals in one order, and G-square has that order too if the rows, or the columns, have the same total: the simulated P values are then of the same tables, and are to be the same. Each simulated P value is compared with the probability of the tables that are to be counted, from the probabilities of every first count, which are worked out here from that of the most probable count outwards; it may differ by 5 standard errors.
- 6 tables of 2 by 3 and 3 by 3 with 150 to 1,200 subjects, near to what is expected and far from it: each of the four simulated P values against the probability from every table with the same totals.

With 1,000,000 added to the row scores 1, 2, 3 of the table 1 9 / 5 5 / 9 1 the simulated P value of the test of the equality of the mean scores was 1, and is 0.00052; the chi-square for trend of the table 68 232 / 261 187 was 92.03, and is 92.26; and the simulated P value of G-square of the table 1250006 1249994 / 1249994 1250006 was 1, where the probability is 0.9921.

**At the limits** (`limits`, `Limits.cs`). What is refused, and with what words; what a report says when it has no test, no risk ratio or no exact method; counts that are not whole numbers, which are rounded for the exact methods and for the simulation; the option of the exact methods of the test of Mantel and Haenszel, which is for the limits of each table too, whatever the preference of the meta-analysis menu; the continuity correction of a table with an empty cell, from its definition; and 24 tables of 2 by k of up to 3,500 million subjects whose chi-square is small (`cases-many.R`), whose total chi-square is compared to 12 figures and chi-square for trend to 9.

**What is not checked here.** The reports as they are shown. The risk ratio's power, which is text. The exact pooled odds ratio of large series, which takes minutes and is checked in `tests/MetaAnalysis` for smaller ones.

The run is the same every time. The exit code is 0 if every check passes.
