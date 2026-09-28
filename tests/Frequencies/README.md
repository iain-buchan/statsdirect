# The Frequencies analysis: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Frequencies -c Release
```

The checks are made on a build of the program, as those of `tests/Crosstabs` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take a few seconds. One part can be run alone by naming it: `order`, `benchmarks`, `columns`, `numbers` or `chart`.

The analysis gives, for each column, the number of its cells, and a table of its values with the frequency of each, the frequency as a percentage of the values that are not missing, and the cumulative frequency and percentage. The rows are in the order of the values or of the frequencies, ascending or descending. The missing values are the first row in every order, and have no part in the percentages or in the cumulative frequencies; where any are missing the report says after the total how many, and how many are not, of which the percentages are.

**The columns.** A column is handed to the analysis as the program makes it from a worksheet: its values are categories, in the order in which they are first met, and a cell that is empty or has an asterisk is a value of the category "* (missing)", down to the last row of the block in which a column has something. The cells that are missing are marked by the routine of the program; the block is taken to be the same rows of every column.

**The order of the labels** (`order`). A label that is a number is before one that is not; numbers are in order of size; labels that are not numbers, and labels that are the same number written in two ways, are in the order of their characters; a label that is empty is before every other. The routine of the program is given every pair and every three of 47 labels (numbers written in many ways, words, labels that begin with figures, signs): a pair is to be in the opposite order when it is given the other way round, three labels are to be in one order, and each pair is to be in the order of the definition, which is written here with decimal numbers.

**The columns of the benchmarks** (`benchmarks`). 12 columns of 6 to 6,000 cells, of numbers and of text, some with missing values, in each of the four orders: 9,204 figures. The benchmarks are worked out in R 4.6.1, without packages, from the definitions, by the scripts that are beside them:

```
Rscript --vanilla cases-frequencies.R cases-frequencies.txt
Rscript --vanilla figures-frequencies.R cases-frequencies.txt r-frequencies.txt
```

A file of cases has for each column a line `case`, name and kind (`numbers` or `text`), and a line of its cells, `NA` for one that is empty. A file of figures has a key and a figure on each line.

**Columns drawn at random** (`columns`). 300 columns of 1 to 60 cells, of numbers, of labels that are not numbers and of both, with and without missing values; 40 blocks of 2 to 5 columns of different lengths; 12 columns of 2,000 to 6,000 cells and up to 584 values; and columns at the limits (one cell, empty cells before the first value and after the last, a column of missing values beside another). Every table, in each of the four orders, is compared with what the definitions give, which is worked out here: the number of cells, what is said after it of the missing values, the row of the missing values, the values and their order, the frequencies, the percentages and the cumulative figures.

**Columns that are handed over as numbers** (`numbers`). The menu hands its columns over as categories; a script can hand over numbers. 69 columns, some that begin with missing values, one with nothing but missing values and one with no values, are compared with the definitions in the same way.

**The frequencies of the bar chart** (`chart`). The routine that makes the frequencies for the bar chart of frequencies puts its labels in the same order: 60 sets of 1 to 3 columns are compared with the labels in the order of the definition, and with the frequency of each label, or its share of the column where there is more than one column.

**What is not checked here.** The reading of the worksheet itself, and the report as it is shown: the checks are of the figures that the analysis hands to the report. A block in which no cell has anything is not checked: the program takes its first row for a missing value. Labels are read as numbers in the way of the country of the computer (a decimal comma in some); the checks are made in one way on every computer, with a decimal point.

The run is the same every time. The exit code is 0 if every check passes.
