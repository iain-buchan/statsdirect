# The Crosstabs menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Crosstabs -c Release
```

The checks are made on a build of the program, as those of `tests/CoxRegression` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about two minutes. One part can be run alone by naming it: `benchmarks`, `exact`, `ways`, `large`, `progress` or `report`.

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

**The exact test of tables of many columns** (`ways`, `Ways.cs`). 29 tables of 2 to 6 rows and 12 to 100 columns, with 1 to 4 subjects in each column, each as it is and on its side. The method counts the ways of reaching each part of a table, and for 14 of the 19 tables of 12 to 34 columns a count is above 2,147,483,647, which a whole number of 32 bits does not hold: their P values were wrong, with nothing to say so. A 2 by 30 table with 2 in every column (10 columns of 2 and 0, 10 of 1 and 1, 10 of 0 and 2) had P = 0.008247, and its P value is 0.057909; a 6 by 12 table of 24 subjects had 0.001125 for 0.002903. The tables with the totals of such a table are too many to be listed one by one (40 million million for a 4 by 16 table, and a number of 47 figures for the table of 100 columns). They are gone through by kinds: the columns with the same total are a group, a kind of column is the counts that a column of the group can have, and a table is known by the number of columns of each kind; the arithmetic is of whole numbers of any size. That way of going through the tables is itself compared with the list of every table, for 5 small tables. The P value of the report is compared too, for two of the tables.

One of the two bounds of the method, the greatest sum of the logarithms of the factorials of the cells that a table can have, is found by a search that puts as much as can be in one cell after another. The search went through the same part of a table once for each order in which the cells before it could be filled, and with few rows and many columns the time rose steeply with the number of columns: a table of 2 rows with 2 in every column took 3 seconds with 30 columns, 13 with 32, 47 with 34 and 3 minutes with 36, and a 3 by 21 table of 42 subjects took 9 seconds. The search now keeps the parts of a table that it has reached, and the table of 36 columns takes 0.03 seconds. No test of this part is to take 2 seconds; the table of 100 columns takes half a second. The two bounds are compared with the least and the greatest sum of the list of every table for 30 sets of totals of 6 to 13 columns, and with those of the tables gone through by kinds of column for 12 sets of 16 to 100 columns.

**The exact test of tables with large totals, and of tables that were refused** (`large`, `Large.cs`). 53 tables of which all the rows but one have few subjects, and that one has from 80 to 4,000 in each column: the tables with their totals are few enough to be listed, and their column totals are large enough for the keys of the search for the least sum to be above what a whole number of 32 bits holds. The P value of each is compared with that from the list of every table with its totals, and the two bounds of the method with the least and the greatest sum of the list. One of them is the table 1 2 0 1 0 0 / 0 0 1 0 0 1 / 1 1 1 0 1 0 / 294 264 274 201 268 186, whose P value was given as 0.920227, and is 0.865318.

6 tables that were refused at once by the check of the keys of the method, of 5 by 5 to 6 by 6 and 60 to 90 subjects, and 4 tables of 2 rows and 92,678 to 100,000 subjects at the limit of the keys, are compared with benchmarks that are R's `fisher.test`: the P value, or that the test is not made for a table whose keys cannot be held. The benchmarks are made by

```
Rscript --vanilla figures-exact-large.R cases-exact-large.txt r-exact-large.txt
```

**The progress bar of the exact test** (`progress`, `Large.cs`). The analysis is given a progress bar that keeps what it is told: no bar is shown for a test of less than a second; a test of some seconds shows a bar for each stage of its search that it meets after the first second, whose words say which stage it is, and whose shares are from 0 to 1 and in order; each bar is finished once; the P value is the same, to the last bit, as without the bar. A test that is stopped has no P value and no hybrid approximation, and the other figures of the analysis are as they are without the test. What goes wrong in the showing of the bar reaches the caller, and is not taken for a fault of the test. These checks depend on the time that a test takes: the tests that they use take 5 and 14 seconds on the computer on which they were written.

**The report** (`report`, `Reports.cs`): the dialog of the scores gives back what was typed, and the scores of the user are those of the analysis; the questions are put once; each column variable starts from the categories of the row variable; the order of the categories; a subject without a value is left out; the 2 by 2 tables of the strata are pooled as the meta-analysis of the same tables pools them; the random tables of the simulation have the totals of the table, and the mean and variance of a cell, with small totals and with large; the scores that the tests in strata can make from the counts; and what those tests say is wrong with what they are given.

**What is not checked here.** The time that the exact test of a table with large totals takes. For tables of many cells the place at which a look-up starts was corrected, which made the test two to three times as fast (a 4 by 3 table of 900 subjects took 21 seconds, and takes 8), and leaves its P value as it was. How evenly the progress bar of a stage goes with the time is not checked either: in the tables that were timed the share of the nodes of a stage that were done was within 2 to 15% of the share of the time of the stage that had gone.

The run is the same every time. The exit code is 0 if every check passes.
