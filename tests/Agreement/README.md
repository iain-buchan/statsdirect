# The Agreement menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Agreement -c Release
```

The checks are made on a build of the program, as those of `tests/CoxRegression` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take less than a minute.

**Agreement of continuous measurements** (`Intraclass.cs`). The example of the help and thirty sets of data, some with missing values, some large beside their spread, are put through the report. The intraclass correlation coefficient and its limits are compared with a one way analysis of variance whose sums of squares are taken in decimal arithmetic; so are the within-subjects standard deviation, the repeatability, the limits of agreement, Kendall's rank correlation of the subjects' standard deviations with their means, and what is passed to the three plots.

**Agreement of categories** (`Kappa.cs`, `KappaRaters.cs`, `KappaSimulated.cs`, `Symmetrise.cs`). For two raters, forty tables and twenty-four pairs of columns of ratings, with linear, quadratic and given weights (symmetric and not): kappa, weighted kappa, Scott's pi and Gwet's AC1 from their definitions, and each standard error by the delta method carried out numerically; Maxwell's test and the generalised McNemar test, with its degrees of freedom when pairs of categories are empty; the interval of a 2 by 2 table as the roots of the chi-square of goodness of fit. For three or more raters, the kappa of each category and of all together, with standard errors that are also compared with the spread of kappa in ratings drawn at random without agreement. The simulated P of kappa is compared with the P from every table that has the totals of the table observed. The lists of categories of two raters are made the same for raters who do not use the same categories.

**Agreement of categories at its limits** (`KappaBoundary.cs`, `MaxwellGroups.cs`). Forty-eight tables of raters who agree on every subject, for which every measure should be 1, the standard errors behind the confidence intervals 0 and the limits 1, with the standard errors for the tests as for any other table; tables with one subject of many off the diagonal; and a table with one category only, for which kappa has no value. Four hundred sparse tables, in half of which the raters disagree within groups of categories only, for which Maxwell's chi-square, its degrees of freedom and P are compared with the quadratic form in the generalised inverse of the matrix of all the categories, from its latent roots and vectors by Jacobi's method.

**The universal agreement measure** (`Universal.cs`). The example of the help and twenty-four sets of measurements, with and without a standard: the mean, variance and skewness of delta are compared with those of every dealing of each observer's measurements among the objects, listed one by one; the P value with the gamma distribution, over the whole range of skewness; the simulated P with the P of every dealing; and the comparison of two values of R with the reports that its figures come from.

The distribution functions that the checks use are worked out in `Program.cs` from series and continued fractions, so that nothing of the program's own arithmetic is relied upon. The run is the same every time. The exit code is 0 if every check passes.
