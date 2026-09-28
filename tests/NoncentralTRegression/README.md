# Noncentral t: broad regression checks

Build the application, then run from the repository root:

```powershell
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/NoncentralTRegression -c Release
```

The suite uses the actual built StatsDirect assembly. `StatsDirectBin` can select another build, as in the meta-analysis tests. It covers 6,360 reference cases:

- 5,392 CDF cases: degrees of freedom from 1 to 2,147,483,647; noncentrality from -1,000,000 to 1,000,000; both signs; the normal/central-t limits; values near zero; both sides of the algorithm switches; and extremely small tails.
- 576 quantiles, including probabilities 1e-10 and 1-1e-10, checked against independently inverted probability equations.
- 224 calls of the power calculation and 168 pairs of effect-size confidence limits.

The suite also checks CDF bounds, monotonicity, reflection symmetry, infinite t endpoints, and the calculator's upper-tail probabilities, logarithms and quantiles. Small probabilities are compared relatively, not only against an absolute tolerance that could hide large errors. The relative checks cover probabilities between 1e-280 and 1e-8; this does not claim full relative precision throughout the subnormal floating-point range.

## Independent references

`reference.R` regenerates `references.tsv` using base R only. Run it from this folder with `Rscript --vanilla reference.R`. It takes several minutes.

The reference conditions on the **normal variable** in `T=(Z+delta)/sqrt(V/df)` and integrates positive normal-density/gamma-tail terms. Production conditions on the **chi-square denominator** and integrates in a log-square-root coordinate. This avoids using the production algorithm to validate itself. A Taylor expansion around t=0 handles the very narrow transition at |t|<=1e-9 in the reference coordinate. For quantiles and effect-size limits, the previous fixture values serve only as starting guesses: R's `uniroot` independently solves the defining probability equation again.

R's `pt` uses a normal approximation when the noncentrality is large, so it is not an exact reference there. The usual central-t critical values for power are obtained with R's `qt` without a noncentrality parameter. The meta-analysis tests separately retain references integrated in the chi-square variable, giving a second numerical cross-check.

## Regressions caught by the broader checks

- For odd degrees of freedom, the former recurrence's Owen-integral approximation could lose about 3e-8 or give a probability outside [0,1]. These cases now use the mixture integral.
- A fixed absolute integration tolerance could accept inaccurate tiny tails. The integration limits and error tolerance now adapt to the tail probability.
- Evaluating t*S through t*(S-1)+t when S is very small lost digits. The routine now forms t*S directly away from S=1.
- Subtracting a CDF from 1 lost precision in extreme upper-tail quantiles and in the calculator's upper-tail/log-probability functions. Whole-df calls now preserve the smaller tail by reflection.

The application and its references remain double-precision calculations. Passing this finite grid is substantial regression evidence, not a proof for every floating-point input.
