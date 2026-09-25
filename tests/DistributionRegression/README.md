# Beta, F and Student t regression tests

Run from the repository root with the .NET 10 SDK:

```text
dotnet run --project tests/DistributionRegression/DistributionRegression.csproj -c Release
```

The project compiles the program's own beta, F and t source files and nothing else of the program: the two constants those files take from Numerics.cs (the smallest normal double and the missing-value sentinel) are supplied by Constants.cs.

It stops at the first failure. It checks reference values computed in 70-digit arithmetic, the closed forms of the F tail on 2 numerator degrees of freedom and of the t quantile on 2, log probabilities where the probability itself underflows, the monotonicity of the inverse and the consistency of its two coordinates, endpoints, invalid arguments, the chi-square and normal limits at infinite degrees of freedom and the point mass with both infinite, the far tails and quantiles at infinite degrees of freedom, the chi-square and gamma routines against their closed forms on 2 degrees of freedom, and that an inverse which runs out of iterations reports a failure rather than an answer.

The routines were also checked against R 4.6.1 through the program's reports and calculator (analysis of variance, confidence intervals, the rate ratio report and the help's worked examples); these tests do not replace those checks.
