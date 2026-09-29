# The Clinical Epidemiology menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/ClinicalEpidemiology -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take a few seconds.

The menu has six reports: the risk analysis of a prospective study (relative risk) and of a retrospective study (odds ratio), the diagnostic test, the likelihood ratios of a test with several results, the errors of a screening test, and the number needed to treat. Each report is given the inputs of a case, and what it gives the report is compared with figures that are worked out from the definitions of the methods.

**The cases of the benchmarks.** `benchmarks/cases.txt` has the cases and `benchmarks/expected.txt` what is expected of them; `benchmarks/make-benchmarks.R` made both, with R 4.6.1 and no packages, in a few seconds. Where the figures are from:

- the confidence limits of a proportion (Clopper and Pearson): quantiles of the beta distribution;
- the limits of a ratio of two proportions, which a relative risk and a likelihood ratio are (Koopman): the ratios at which the score is the normal deviate of the confidence level, found as roots between ends that are far apart; the most likely proportions with a ratio are the root of a quadratic;
- the limits of a difference of two proportions (Miettinen and Nurminen): found in the same way, the most likely proportions with a difference being where the slope of the likelihood is 0;
- the conditional estimate of an odds ratio, its exact limits (Fisher and mid-P) and the exact P values: the chances of the first count of the table with its totals given, which are those of the hypergeometric distribution multiplied by the odds ratio to the power of the count;
- the limits of a population attributable risk: the variance of the logarithm of 1 less the risk, from the counts of a sample (of cases and controls, or of a cohort), worked out from the counts and not from the formula of the program;
- a number needed to treat and its limits: 1 over the risk difference and over its limits.

The tables are ten, with counts from 0 to 1,200 and with every cell empty in turn, at confidence levels of 80%, 95% and 99%, with the exposure of the population entered and not entered; screening tests with a sensitivity and a specificity of 0 to 1 in populations with 1 case in 1 to 1 in a million; seven trials for the number needed to treat, with and without events in each group.

**The ends of what can be entered, and what is refused,** are among the cases: counts that are not whole numbers, which are rounded, a half to the even number, before anything is worked out; events and subjects given the other way about; an expected risk given as a percentage; a confidence level that is not above 0 and below 1, for which 0.95 is taken; counts below 0 and counts that come to more than 2,147,483,647, tables without subjects, without exposed subjects or without an outcome, which are refused with words that are to be in the message.

**What these checks showed when they were written.** The risk analysis of a prospective study refused a table in which no subject who was not exposed has the outcome. The change of a predictive value in the diagnostic test report was the difference of two percentages rounded to whole numbers. A likelihood ratio without end was given as none. A count that is not a whole number was rounded for the exact odds ratio and taken as it is for the other figures of the same report, and a count below 0 gave a message of the program that says nothing to a user.

**What is not checked here.** The reports as they are shown, the power that two of the reports print, and the numbers needed to treat that are rounded up.

The run is the same every time. The exit code is 0 if every check passes.
