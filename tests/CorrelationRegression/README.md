# Rank correlation, nonparametric regression and least squares: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/CorrelationRegression -c Release
```

The checks are made on a build of the program, as those of `tests/CoxRegression` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`.

Forty sets of data, with and without ties and missing values and at three levels of confidence, are put through the reports of Kendall's rank correlation, Spearman's rank correlation and nonparametric linear regression. What comes back is compared with figures worked out in `Program.cs` from the definitions: the counts of concordant, discordant and tied pairs, the score and its variance with ties, tau b, gamma and the confidence interval of tau; the exact distribution of Kendall's score, from the numbers of orderings with each number of inversions; rho as the correlation of the ranks, its interval, and its exact distribution by taking every ordering in turn; the median of the slopes of the pairs of points, the intercept through the medians, and the confidence limits of the slope counted in from each end of the ordered slopes.

The least squares fit of simple linear regression is compared with sums taken in decimal arithmetic, for data whose values are large beside their spread (years, dates as day numbers, times in seconds) as well as for data that start at 0.

The run is the same every time. The exit code is 0 if every check passes.
