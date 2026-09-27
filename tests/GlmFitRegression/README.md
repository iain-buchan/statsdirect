# Regression routines: checks by calculation

Run with the .NET 10 SDK:

```
dotnet run --project tests/GlmFitRegression -c Release
```

Compiles `StatsDirectUI/Builtins/Regress1.cs` on its own and checks the logistic and Poisson fit by calculation.

The decomposition routines are checked against what their comments say they leave behind: R of the Householder QR factorisation in the upper triangle with the reflections below it, Q'b from the transformation of the right-hand side, and the singular values and P' from the singular value decomposition of the triangle.

Each fit is then checked against the model it should have fitted: the score equations at the coefficients, the covariance matrix against the inverse of X'WX, the leverages against the diagonal of the hat matrix, and the deviance residuals against the deviance. The fits are made with and without a constant, with prior weights (one of them zero), and with predictors left unselected, which must not change the leverages.

The routines of the linear regression, which are in the first half of the same file, are checked in `LinearRegressionChecks.cs`: the means, covariance and correlation; the two singular value decompositions and the fit, inverse and leverages that are made from them; the least squares fit by rotations, with and without weights and with a predictor that adds nothing to the others; the solving and inverting of its triangle; the Durbin-Watson statistic and the intervals about a prediction; and the area under a polynomial of each degree that polynomial regression offers, with the interpolation that finishes it.

The design and the responses are fixed, so the run is the same every time. The exit code is 0 if every check passes.
