# Cox regression: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/CoxRegression -c Release
```

The Cox regression cannot be compiled on its own, as the routines of the other regressions can, because its report routines use the program's frames, parameters and charts. The checks are therefore made on a build of the program: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`.

Each set of data is put through the regression and then through every report that follows it, which are given their parameters as the program passes them on. What comes back is compared with figures worked out in `Program.cs` from the definitions:

- the coefficients that make the partial likelihood greatest, by Newton's method, with their standard errors from the inverse of the matrix of second derivatives, and the likelihood ratio chi-square; tied times by Breslow's approximation;
- the baseline survival by the product-limit estimate, solved by bisection at a time with tied events, and the baseline cumulative hazard;
- the Cox-Snell, martingale and deviance residuals, the leverage and the proportionality constant of each record;
- the points of the plot of deviance residuals against time, each of which should be a record's residual against that record's time;
- the hazard ratios with their confidence limits, and the model analysis.

There are eleven sets that are fixed and sixty that are made at random from a fixed seed, with and without tied times, strata, records that stand for several subjects, centring, and missing values of the time, the event code, a predictor and the stratum. A record with a missing value should be left out of everything, with a warning, and the values saved to the worksheet should keep the rows of their records.

`OtherChecks.cs` checks that a predictor which does not vary, or which is determined by others, is dropped wherever it stands among the predictors; the iteration for the baseline survival at a time with tied events, on 200,000 sets of hazard ratios; the limit on the iterations of the fit; the figures of a record that the fit itself leaves out; and what is said when a fit cannot converge.

`InfiniteCoefficient.cs` checks what is done when a predictor separates the subjects who had the event early from the rest, so that its coefficient has no finite estimate: the fit should end with a warning that names the predictor, and the other coefficients and the log likelihood should be those of the model in which the predictor makes strata, which is what the likelihood tends to. It also checks that a predictor which puts every event in order, so that the fit cannot converge, is said not to have converged; and that at the precision which the dialog box offers, 0.000000001, which is read from the definition of the operation, the coefficients of 150 ordinary sets of data are within 0.00000005 of those that make the likelihood greatest, with no warning of an infinite coefficient.

The run is the same every time. The exit code is 0 if every check passes.
