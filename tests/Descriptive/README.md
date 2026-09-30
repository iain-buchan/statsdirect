# The Descriptive menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Descriptive -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about ten seconds, most of it for the bootstrap. A part can be run alone: `dotnet run --project tests/Descriptive -c Release -- benchmarks` (or `bootstrap`).

The reports of the menu are the univariate summary, the weighted univariate summary, the quick univariate summary and the time series summary. Each report is given the inputs of a case, and what it gives the report is compared with figures that are worked out from the definitions of the statistics.

**The cases of the benchmarks.** `benchmarks/cases.txt` has the cases and `benchmarks/expected.txt` what is expected of them; `benchmarks/make-benchmarks.R` made them, with R 4.6.1 and no packages. Where the figures are from:

- the mean, the variance, the skewness and the kurtosis: the moments about the mean of the values less the first of them, in units of the greatest difference from the mean; the confidence limits from Student's t as R has it; the geometric mean from the logarithms;
- the quantiles: from their definitions, and for centile type 1 in whole numbers. The part p is a fraction, the weights are whole numbers, and whether the sum of the weights so far is above p n, or is p n exactly (when two values are averaged), is a comparison of whole numbers, which has no rounding;
- the weighted summary: the weights are made to sum to the number of values, and the mean, the variance and the moments have the weights in their sums;
- the time series summary: the table of times by subjects of each group, and from it the statistics of each time, of each subject (the area under the curve by the trapezium rule over the observations that the subject has, the greatest observation with its time, the slope of the least squares line up to it) and of each group (the mean area with its limits from Student's t and from the normal distribution); of two groups the t test for unequal variances, with the degrees of freedom of Welch and Satterthwaite; the squared correlation of the areas, and of their logarithms, with the normal scores of their ranks.

The samples are 21, of 2 to 1000 values: near 0 and far from it (values of 100,000,000 that differ by 2, and of a million million that differ by 100,000), wide and narrow (a spread of a millionth), very small (1e-150) and very large (1e150), whole numbers with many values that are the same, values that are all the same, and values of 0 and below; with missing values; several columns of different lengths; at confidence levels of 80%, 95% and 99%; with centiles of many percentages, for sizes at which p n is a whole number and at which it is not. The time series are the example of the help and series that are made: of 3 to 20 subjects at 3 to 11 times, in one group and in two, with observations that are missing, with and without a time of 0.

**Values far from 0 and near one another** are where the differences from a mean lose their figures, because the mean itself has been rounded, and where a benchmark does too: the moments are worked out here of the values less the first of them, which is exact. The skewness of the example of the help (100 measurements of the speed of light), worked out in whole numbers without rounding, is -0.018259613963113.

**The bootstrap of the time series summary** is made of random numbers, so that a bootstrap made elsewhere agrees with it only as far as the chance of the draws allows. `benchmarks/bootstrap-expected.txt` has, for each figure, the mean and the standard deviation of 10 bootstraps of 100,000 draws that R made with random numbers of its own. A figure of the program is to be within 4.5 standard deviations of that mean (the standard deviation being that of the difference of one bootstrap from the mean of 10); where the 10 are all the same, which they are when the centile falls on a value that many draws have, the figure is to be theirs. The program is given a seed, so that its figures are the same in every run. A limit that was wrong by more than a part in a few hundred of the width of the interval would fail.

**The ends of what can be entered, and what is refused,** are among the cases: one value, and no value; values that are all the same; a confidence level that is not above 0 and below 1, for which 0.95 is taken; centiles of 0% and 100%; weights that are missing or 0, and columns of different lengths with them; a time series of one subject, of one time, with a subject that has one observation, with a peak at the first time, with observations below 0; a subject of whom every observation is missing, and a group of which every observation is missing, which is left out; two groups whose areas are all the same, and one subject in each of two groups, which are not compared (the bootstrap of the comparison gave P = 1 / (draws + 1) for the first); the same observation twice, and two observations that differ for a subject at a time, which is refused with words that are to be in the message.

**What these checks showed when they were written.** A sample of one value had no median, quartiles or centiles. The skewness of values far from 0 was right to 7 figures. Values of 1e150 had no variance, and values of 1e-150 no skewness. With a confidence level of 0% or 100% the limits of 95% were given under the names of 0% or 100%, and a centile of 7% was named "Centile 7.000000000000001". The time series summary stopped with a message of the program when an area under the curve is 0 or below, when there is one subject, and when the areas are all the same; it counted rows without an observation as observations, and gave a subject without observations an area of 0. Two groups whose areas were all the same were compared, without a t, and the bootstrap gave a P value of 1 / (draws + 1); a group without observations was compared as if it were there.

**What is not checked here.** The reports as they are shown and the charts; a column of labels that has missing values, which the program numbers when it reads the worksheet.

The run is the same every time. The exit code is 0 if every check passes.
