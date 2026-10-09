# Checks of the selection of data by group identifiers

A console project that drives the selection of data from a worksheet with a stand-in for the grid, for the part of it that needs no window: the combination of the identifier columns into one classifier of the rows (`CellArrayProcessor`, mode `CategoryCombineAllColumns`), which is what "select groups by identifier" works from.

The checks are made on a build of the program, as those of `tests/Nonparametric` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`.

```
dotnet run -c Release
```

**What is checked.** The identifiers of a column of texts: two groups without blanks; a blank identifier cell, which keeps its row as a missing identifier so that the identifiers are as many as the rows selected; an asterisk, which is a blank; the label of a missing value, "* (missing)", which is a missing identifier and not a group; two identifier columns, where a blank in either makes the row's identifier missing; and blank rows at the end of the range. Also the rule of the refusal of a value beside a blank identifier (`RowWithoutIdentifier` in `GridSelectionProcessor`), which the selection of one data column by identifiers and the grouped covariance selection apply: the row named is the first, counted from 1 among the rows selected, that has a value but no identifier, and none when a blank identifier has only a missing value beside it.

**What these checks showed when they were written (8 October 2026, from the audit of the Mac port).** A row whose identifier cell was blank was left out of the identifiers, so that they were fewer than the rows selected: the data then had to be cut to their number ("all columns selected must be the same length"), and the values after the blank were paired with the wrong groups without a warning. A row that has a value but no identifier is now refused by its row number, and a group whose values are all missing is refused by name; those two refusals need the window and are not checked here, but the row that the first of them names is (9 October 2026: the grouped covariance selection, which still left such a value out in silence, applies the same rule to its Y replicates and its X column).
