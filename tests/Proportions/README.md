# The Proportions menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Proportions -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about five seconds. One part can be run alone by naming it: `benchmarks`, `definitions` or `limits`.

The analyses are the single proportion, with its exact and mid-P values and the confidence limits of Clopper and Pearson and of Wilson; paired proportions, with the exact test of the pairs that differ and the limits of the difference by the score method of Newcombe; and two independent proportions, with the limits of the difference by the score method of Miettinen and Nurminen, the exact two sided mid-P value and the normal deviate. The limits of the difference of two independent proportions are also those of the risk difference of the 2 by 2 chi-square test, which `tests/ChiSquare` checks, and of each study of the risk difference meta-analysis.

**The cases of the benchmarks** (`benchmarks`). 981 cases: 651 single proportions of 1 to 1,000 million observations, with none, all and some responding, null proportions from 0 to 1, and counts from 6 standard deviations below what is expected to 40 above it in samples of up to 50 million; 147 sets of paired proportions of 1 to 900 million pairs, of which some have no pairs that differ, pairs that differ all one way, or as many one way as the other; and 183 pairs of independent proportions in samples of 1 to 1,000 million, of which some have none or all responding in one sample or in both, samples of 3 against a million, and numbers that are not whole numbers. 12,615 figures are compared with their benchmarks, from which a figure may differ by one part in a hundred million, and no case is to take 2 seconds. The benchmarks are worked out in R 4.6.1, without packages, by the scripts that are beside them:

```
Rscript --vanilla cases-prop.R cases-prop.txt
Rscript --vanilla figures-prop.R cases-prop.txt r-prop.txt
```

- The exact P value of one side is the less of the probability of a count that is no more than the one observed and that of a count that is no less; that of two sides is the probability of the counts that are no more probable than the one observed. The mid-P value of one side is that of one side less half the probability of the count observed, and that of two sides is twice it.
- The limits of Clopper and Pearson are quantiles of the beta distribution. The limits of Wilson are the proportions that are z of their own standard errors from the proportion observed, found as roots on the scale of the logarithm of the odds, so that a limit near 0 has all its figures.
- The exact test of paired proportions is that of the less of the two numbers of pairs that differ, of which either way is as likely. The limits of the difference are from the score limits of the two proportions and the correlation of the two responses, which has a correction for continuity if it is above 0.
- The limits of the difference of two independent proportions are the differences at which the score statistic has the value of chi-square for the confidence level. The proportions that are most likely with a difference are at an end of the range that they can have or at a root of the cubic that the derivative of the likelihood comes to, whichever has the greatest likelihood.
- The exact two sided mid-P value of two independent proportions is from the hypergeometric probabilities of the first count, with the totals of the table given: twice the less of the two tails, each less half the probability of the table observed.

A file of cases has on each line the kind of the analysis, a name and what the analysis is given. A file of figures has a key and a figure on each line.

**Cases drawn at random, against the definitions** (`definitions`, `Definitions.cs`). The figures are worked out in the tests, by another route than that of the benchmarks and than that of the program. Nothing of the program is used to make what is expected.

- 400 single proportions of up to 3,000 observations: the probability of every count from the logarithms of factorials, which are made by adding logarithms; the limits of Clopper and Pearson by bisection of the tail that defines each of them; the limits of Wilson by bisection; and the normal deviate of a confidence level from the integral of the density.
- 600 sets of paired proportions of up to 24,000 pairs, of which some have no pairs that differ one way and some none that agree: the exact and mid-P values from the probabilities of the counts, and the limits of the difference from score limits that are found by bisection.
- 600 pairs of independent proportions in samples of up to 8,000, of which a third have none responding in one sample and all in the other: each limit of the difference is put into the score statistic, which is to have the value of chi-square for the confidence level, with the most likely proportions found by a search of the likelihood; the exact mid-P value is from the probability of every first count; and the normal deviate is from the pooled proportion.

**At the limits** (`limits`, `Limits.cs`). What is refused, and with what words; a null proportion of 0 or 1, with which the count can have one value only; none and all responding, for which the report names the interval of one side; numbers that are not whole numbers, which are rounded, a half to the even number; single proportions of 1 to 3 million observations against the definition, whose P values are binomial and are named so; the largest sample that can be asked for (2,147,483,647), against the normal deviate; no pairs that differ, one pair, pairs that differ all one way (a half to the power of their number), and 1,100 to 2 million pairs that differ against the definition; one sample without responders against one with none but responders, in samples of 1 to 2,500 of one size and not, for which the limit is the root of the statistic with the most likely proportions from a formula; none and all responding in both samples; and the exact mid-P value of samples of thousands to millions against the definition.

**What is not checked here.** The reports as they are shown. Samples of more than 2,147,483,647, which the program does not ask for.

The run is the same every time. The exit code is 0 if every check passes.
