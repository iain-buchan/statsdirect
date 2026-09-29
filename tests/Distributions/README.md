# The Distributions menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Distributions -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about a minute. One part can be run alone by naming it: `benchmarks`, `range` or `comparisons`.

The menu is a calculator: the tail probabilities of a value, and the value that has a tail probability, for the normal, Student's t, F, chi-square, studentized range, binomial, Poisson, Kendall's tau, Spearman's rho and non-central t distributions. The checks make the calculator without showing it, fill its boxes as a user fills them, and compare what it puts into its boxes with figures that are worked out from the distributions themselves. The routines of the distributions have checks of their own in `tests/DistributionRegression` and `tests/NoncentralTRegression`; what is checked here is what a user of the menu is shown.

**The cases of the benchmarks** (`benchmarks`). `benchmarks/cases.txt` has the cases and `benchmarks/expected.txt` what is expected of the boxes; `benchmarks/make-benchmarks.R` made both, with R 4.6.1 and no packages, in about three quarters of an hour. Where the figures are from:

- the normal, Student's t, F, chi-square, binomial and Poisson distributions: the functions of R;
- the F value of an upper tail probability: the root of R's probability;
- the non-central t and the studentized range: their definitions, by integration. R's own functions are not the reference for these two. In the far tails, and for the studentized range with few degrees of freedom, they are off by more than the calculator shows: the probability of a studentized range above 20 with 2 degrees of freedom and 2 samples is 0.0049628, which is twice the upper tail of Student's t at 20 divided by the root of 2, and the function of R gives 0.0046680;
- Kendall's S and the Hotelling-Pabst T of Spearman's rho: the chances of every S and T, from counts of the orders that are built up a rank at a time.

The cases go to the ends of what can be entered: tails of 1e-300, thousands and millions of degrees of freedom, two million trials, a mean of a million, 60,000 observations. There are cases of what is to be refused: what is not a number, a number of successes or of events that is not whole or is below 0, an S or a T that there cannot be, a rank correlation outside -1 to 1.

**The room of a figure.** A box shows 15 decimal places, or 15 figures of a probability below 1e-15, or 7 figures of a value above 1e15; the boxes of the studentized range show 7 decimal places. A figure is given the room of what its box can show and a part in a thousand million of itself, unless the benchmark gives the figure a room of its own. Those are the rooms of what the program has a series for, as they were found:

- Kendall's S with more than 50 observations: 0.0000005 (the series is within 0.0000003 with 51 observations and within 0.00000003 with 100); with tens of thousands of observations the figure is that of the normal distribution, and the room 0.0001;
- Spearman's rho with more than 10 pairs: 0.0004, and 0.00005 from 15 pairs, as the help gives them;
- the studentized range with 8 to 20,000 degrees of freedom that are twice the number of samples or more: 0.0000005 with up to 30 samples, and 0.000002 with more (the series is within 0.0000016 with 100 samples, where the probability below the range is small); with a million degrees of freedom, which the program takes as degrees of freedom without end: 0.000005. A value of the studentized range has the room of its probability divided by the slope of the probability at the value.

**The studentized range, gone through** (`range`). The routine serves the multiple comparisons of the analysis of variance too. For 22 numbers of degrees of freedom from 2 to a million, with 2, 3, 8 and 30 samples, and ranges from 0.5 to 60: every probability is given, is within 0 to 1, and is not below that of a smaller range.

**Newman-Keuls comparisons of groups that are far apart** (`comparisons`). Groups with means 10 apart and values within 1 of their means, with 50, 15, 800 and 24 residual degrees of freedom: every difference is tested and is significant, with a P within 0 to 0.05, and every group is given as different from every other. With 50, 15 and 800 degrees of freedom the probability of so large a range used to be refused, the first comparison was taken as not significant, and the comparisons stopped there.

**What these checks showed when they were written.** The lower tail of Student's t and the upper tail of the non-central t were 1 less the other tail, and were shown as 0 below 1e-16; the value of a small probability of that side lost figures, or was refused. A Hotelling-Pabst T of 0 became 1, and an odd T had the probability of the even number two above it. With more than 1290 pairs (Spearman) or 46341 observations (Kendall) a product overflowed. The studentized range was refused where its probability is 1, for some numbers of degrees of freedom (15, 50, 800, 5000 and 25000 among them); its value was not found for an upper tail of a half or more with 20 samples or more; above 25000 degrees of freedom its probability was off by up to 0.0001; and where the distribution is integrated a probability of 1 was above 1 by 1e-12, so that the upper tail was shown as a number below 0.

**What is not checked here.** The report as it is shown, and the buttons of the form. The other multiple comparisons of the analysis of variance.

The run is the same every time. The exit code is 0 if every check passes.
