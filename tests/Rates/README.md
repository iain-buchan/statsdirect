# The Rates menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Rates -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about half a minute. One part can be run alone by naming it: `benchmarks`, `definitions` or `limits`.

The analyses are the comparison of two crude rates, by their difference and by their ratio, with the conditional analysis of the ratio; indirect standardization and the standardized mortality ratio; direct standardization, from columns of a worksheet and from the grid of the screen form; the comparison of two populations that are standardized to the same reference population, with the Poisson and with the binomial model; and the confidence interval of a rate. The exact method of the conditional analysis is checked in `tests/ExactTests` as well.

**The cases of the benchmarks** (`benchmarks`). 587 cases: 169 pairs of crude rates of 1 to 2,000,000 events, in person-times of a fraction to thousands of millions, of which some have a group without events or numbers of events that are not whole numbers; 138 sets of strata for indirect standardization, of 1 to 20 strata and none to 2,000,000 deaths, some with a rate that is missing; 142 for direct standardization, some without events, with a stratum of which every subject has the event, with a stratum of more events than person-time, with a reference size of 0 or with a number missing; 132 pairs of populations of 1 to 12 strata, half with each model, some with strata or populations without events; and 6 rates. 29,130 figures are compared with their benchmarks, from which a figure may differ by one part in a hundred million, and no case is to take 10 seconds (the conditional analysis of 780,000 events takes 2.5). The benchmarks are worked out in R 4.6.1, without packages, from the definitions, by the scripts that are beside them:

```
Rscript --vanilla cases-rates.R cases-rates.txt
Rscript --vanilla figures-rates.R cases-rates.txt r-rates.txt
```

- The limits of the mean of a Poisson count are quantiles of the gamma distribution, which are put back into the Poisson probabilities that define them.
- The exact limits of the ratio of two rates are those of the events of the first group as a count of all the events, from quantiles of the beta distribution; the mid-P limits are roots of the binomial probabilities, and the exact P values are sums of them.
- The limits of the difference of two rates are from the standard error of the difference, each rate with the variance of a Poisson count over its person-time squared.
- The limits of the ratio of two proportions (the binomial model) are the ratios at which the score statistic has the value of chi-square for the confidence level.
- A standardized rate is the mean of the rates of the strata with the weights of the reference population, and its variance is the sum of the weights squared times the variances of the rates. With more events than person-time in a stratum, which is a rate above 1, the binomial model has no figures.

A file of cases has on each line the kind of the analysis, a name and what the analysis is given; a list of numbers, one for each stratum, has commas between them. A file of figures has a key and a figure on each line.

**Cases drawn at random, against the definitions** (`definitions`, `Definitions.cs`). The figures are worked out in the tests, by another route than that of the benchmarks and than that of the program. Nothing of the program is used to make what is expected. A probability is a sum of the terms of the counts over the greatest of them, each term made from the one next to it by the ratio of the two; a limit is found by halving a range until the sum that defines it has the probability that is wanted; the normal distribution is from the series and the continued fraction of the integral of its density; and the proportions that are most likely with a ratio are found by halving the range of a proportion until the likelihood has no slope. What is expected is first looked at itself, at values that are known.

- 300 pairs of crude rates of up to 6,000 events, of which a quarter have a group without events and some have two rates that are the same: the rates, the difference and its limits, chi-square and its P value, the ratio and its exact limits, and the nine figures of the conditional analysis.
- 300 sets of strata for indirect standardization, from two columns and from the grid in turn: the deaths expected, the ratio, its limits, the whole numbers of the report, and the two probabilities.
- 200 sets of strata for direct standardization, from three columns and from the grid in turn, of which 22 have a stratum with more events than person-time: the standardized rate, its standard errors and limits with both models (with the Poisson model only, and a note in the report, for those 22), the improved limits, and the rate and the limits of every stratum.
- 300 pairs of populations, half with each model: the ratio and the limits of every stratum and of all strata together (the exact limits, or the score limits), the crude rates with their limits (of a Poisson count, or of a proportion), the standardized rates with their limits, and the standardized rate ratio.
- 200 rates of up to 100,000 events.

**At the limits** (`limits`, `Limits.cs`). What is refused, and with what words: numbers below zero, a person-time of zero, no events in either group, no deaths expected, no stratum with all its numbers, reference sizes that add up to nothing, more events than person-time with the binomial model of two populations (the Poisson model takes them), a grid without all its columns. A confidence level that is not between 0 and 1, which is taken as 0.95, and a multiplier that is not a number above 0, which is taken as 1; rates by the thousand against rates by the unit. The standardized mortality ratio as a whole number: a ratio of 16,666,667, of which the upper limit times 100 is more than a whole number of 32 bits holds; a ratio below 0.01; a ratio times 100 of 62.5; no deaths; 2,000 million deaths. The same numbers from a worksheet and from the grid of the screen form; strata with a number missing, which are left out, and the labels, which stay with their strata. Direct standardization of a stratum with more events than person-time: the figures of the Poisson model against the definitions, no figures of the binomial model and the note of the report; person-time in thousands against the same in units; as many events as person-time in every stratum; and the check of the grid of the screen form, which takes more events than person-time and refuses a person-time of zero. Two crude rates with the person-times in months for years, and with the groups the other way round; two rates that are the same; a group without cases. Two populations: every row of the ratios against the comparison of two crude rates, strata and populations without events, the rows that are given to the plot (those with a ratio that is not infinite), one stratum, and strata of which every subject has the event. Millions of events against the definitions, with the time of each report.

**What is not checked here.** The reports as they are shown, and the plot as it is drawn.

The run is the same every time. The exit code is 0 if every check passes.
