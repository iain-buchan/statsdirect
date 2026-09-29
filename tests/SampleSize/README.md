# The Sample Size menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/SampleSize -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about ten seconds.

The menu has nine reports: the sample size for a study of a correlation coefficient, for a comparison of survival times, for independent case-control and cohort studies, for matched case-control and paired cohort studies, for the paired and the unpaired t test, and for a survey of a population. Each report is given the inputs of a case, and what it gives the report is compared with figures that are worked out from the definitions of the methods, as the help gives them.

**The cases of the benchmarks.** `benchmarks/cases.txt` has the cases and `benchmarks/expected.txt` what is expected of them; `benchmarks/make-benchmarks.R` made both, with R 4.6.1 and no packages, in about a minute. What is expected:

- a sample size from a formula is the whole part of the value of the formula and 1;
- the sample size for a correlation is the smallest number of pairs at which the power, from Fisher's z with both tails, reaches what is asked; the sample size for a t test is the smallest number at which the power of the two sided test, from the non-central t distribution with both tails, reaches it. The smallest number is looked for by doubling and halving, which shares nothing with the search of the program;
- a number of controls is the smallest whole number that is not below the controls per subject times the subjects;
- for the matched case-control study the probability of exposure of a case is found as the root of the equation that says that the odds ratio of the pairs that differ is the odds ratio entered, and the sums over the numbers of exposed subjects of a set are made with binomial coefficients;
- the note on the power is given if the normal deviate of the other tail is 3.1 or less, and has the power asked and the power asked and half of alpha.

The cases are combinations of powers of 50% to 99%, significance levels of 0.1% to 20%, ratios of controls to subjects that are whole and that are not (0.5, 1.3, 1.1), and effects from small to large; and the ends of what can be entered: a correlation of 0.00001, probabilities that differ by 0.0000000001, ratios of controls of 0.0001 and of 100,000, a difference of 100 standard deviations, a population of 1e300.

**What is refused** is among the cases, with words of the refusal that are to be in the message: a power or an alpha that is not above 0% and below 100%, probabilities outside 0 to 1, ratios of 0 or below, a relative risk that gives a probability above 1, a number of controls per case that is not whole for the matched study, a number of controls of 0 or below, a standard deviation of 0 or below, effects of nothing (equal probabilities, a hazard ratio or odds ratio of 1).

**What these checks showed when they were written.** Independent case-control and cohort studies rounded the number of controls down when the ratio of controls is not whole. The t tests gave 2,147,483,647, with a warning that the sample size is greater than can be displayed, for large differences that need fewer than ten subjects. A power of 100% was given the sample size of a power of 50% by four of the reports, and was replaced by 80% without notice by three. Above 2,147,483,647 pairs the sample size for a correlation was the first estimate.

**What is not checked here.** The reports as they are shown. The routines of power of `Power.cs` that other menus use for the power that their reports print.

The run is the same every time. The exit code is 0 if every check passes.
