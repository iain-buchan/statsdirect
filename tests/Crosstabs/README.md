# The Crosstabs menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Crosstabs -c Release
```

The checks are made on a build of the program, as those of `tests/CoxRegression` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about a minute. One part can be run alone by naming it: `benchmarks`, `exact` or `report`.

The same routines serve the r by c chi-square test of the chi-square menu, and the tests in strata serve the generalised Cochran-Mantel-Haenszel tests.

**The tables of the benchmarks** (`benchmarks`). 131 tables are put through the analysis of an r by c table and through the Crosstabs report, in which the subjects are counted into the table, once as the table is and once made symmetrical; tables in strata are put through the report as 2 by 2 tables of a case-control study and of a cohort study, or through the generalised Cochran-Mantel-Haenszel tests. Every figure that has a benchmark is compared with it: the counts and the labels of the categories, the expected counts, the parts of chi-square, the percentages, the warnings, chi-square and G-square with their degrees of freedom and P values, the exact P value, the chi-squares for the equality of the mean scores and for trend, the coefficients of association, gamma and tau-b with their two standard errors, P values and limits, and the three tests in strata. A figure may differ from its benchmark by one part in a million.

The tables are drawn from independence and from association, from 12 to 900 subjects and from 2 by 2 to 6 by 4, some with scores, and there are tables at the limits: perfect association, one row, one column, empty rows and columns, counts that are not whole numbers, strata of one subject.

The benchmarks are worked out in R 4.6.1, without packages, from the definitions, by the scripts that are beside them:

```
Rscript --vanilla cases-xtab.R cases-xtab.txt
Rscript --vanilla figures-xtab.R cases-xtab.txt r-xtab.txt 60
```

- The exact P value is R's `fisher.test`, and for small tables also the sum over every table with the same totals.
- The standard errors of gamma and of tau-b are by the delta method with derivatives taken numerically: the standard error of the estimate from the derivatives of the estimate, and that on the hypothesis of independence from the derivatives of the excess of the pairs in the same order over those in opposite orders.
- The tests in strata are quadratic forms that are written out with matrices; R's `mantelhaen.test` gives the test of any association too.
- The P values that are simulated (100,000 tables, with a seed) are compared with the exact P values of the four statistics, from every table with the same totals: a simulated value may differ from the exact value by 4.5 of its standard errors, and the limits that the program gives with it must hold the exact value.

A file of cases has for each table a line `case`, name, kind (`rc` or `rck`), rows, columns, strata, confidence level, the scores of the rows and of the columns (or `-`), and then a row of counts for each row of the table, stratum after stratum. A file of figures has a key and a figure on each line.

**The exact test against every table with the same totals** (`exact`, `Exact.cs`). 6,004 tables, drawn at random with uneven totals, of up to 5 rows and 7 columns: the P value and the probability of the table are compared with those from a list of every table with the same totals, which is made here. The two bounds that the method uses to leave out parts of its search, the least and the greatest sum of the logarithms of the factorials of the cells, are compared with those of the list for 1,200 sets of totals.

**The report** (`report`, `Reports.cs`): the dialog of the scores gives back what was typed, and the scores of the user are those of the analysis; the questions are put once; each column variable starts from the categories of the row variable; the order of the categories; a subject without a value is left out; the 2 by 2 tables of the strata are pooled as the meta-analysis of the same tables pools them; the random tables of the simulation have the totals of the table, and the mean and variance of a cell, with small totals and with large; the scores that the tests in strata can make from the counts; and what those tests say is wrong with what they are given.

**What is not checked here.** The time that the exact test takes: the place at which a look-up starts was corrected, which made the test of a table with many cells two to three times as fast (a 4 by 3 table of 900 subjects took 21 seconds, and takes 8), and leaves its P value as it was.

The run is the same every time. The exit code is 0 if every check passes.
