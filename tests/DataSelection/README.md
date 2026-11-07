# Checks of the selection of data by group identifiers

A console project that drives the selection of data from a worksheet with a stand-in for the grid, for the part of it that needs no window: the combination of the identifier columns into one classifier of the rows (`CellArrayProcessor`, mode `CategoryCombineAllColumns`), which is what "select groups by identifier" works from.

The checks are made on a build of the program, as those of `tests/Nonparametric` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`.

```
dotnet run -c Release
```

**What is checked.** The identifiers of a column of texts: two groups without blanks; a blank identifier cell, which keeps its row as a missing identifier so that the identifiers are as many as the rows selected; an asterisk, which is a blank; the label of a missing value, "* (missing)", which is a missing identifier and not a group; two identifier columns, where a blank in either makes the row's identifier missing; and blank rows at the end of the range. Also the rule of the refusal of a value beside a blank identifier (`RowWithoutIdentifier` in `GridSelectionProcessor`), which the selection of one data column by identifiers and the grouped covariance selection apply: the row named is the first, counted from 1 among the rows selected, that has a value but no identifier, and none when a blank identifier has only a missing value beside it. Also the placing of the rows of the long layout in their groups for the analysis of covariance (`PlaceByGroup`), with missing values left out as the layout in separate columns leaves them out of each series: a blank replicate of y out of its level, a row whose x is blank out of its group; the analysis of such a layout has the figures worked out by hand from the definition (common slope sum of squares 16, between the slopes 1, residual 1/6 on 1 degree of freedom, slopes 1.5 and 2.5) and the same figures, number for number, as the layout in separate columns.

**What these checks showed when they were written (8 October 2026, from the audit of the Mac port).** A row whose identifier cell was blank was left out of the identifiers, so that they were fewer than the rows selected: the data then had to be cut to their number ("all columns selected must be the same length"), and the values after the blank were paired with the wrong groups without a warning. A row that has a value but no identifier is now refused by its row number, and a group whose values are all missing is refused by name; those two refusals need the window and are not checked here, but the row that the first of them names is (9 October 2026: the grouped covariance selection, which still left such a value out in silence, applies the same rule to its Y replicates and its X column). Later on 9 October 2026, again from the Mac port: under groups by identifier the analysis of covariance took a blank x or y cell into its sums as the missing-value number, so that its statistics were wrong without a warning; the rows are now placed with the missing values left out, as the layout in separate columns has them.
## Row-preserving grouped selection (7 November 2026)

The public cell-processing entry point and the production group builder are tested together using a grid that follows SpreadsheetGear's visible-row contract. The second tuple item is the number of visible rows, not the number hidden; the older stand-in has been corrected accordingly.

The new checks cover 200-row selections with leading, trailing and interior missing outcomes, blank tails, filtered first/interior/last rows, optional selected headers, fully filtered data, missing identifiers and groups without observations. They retain ordinary unequal-length separate samples and explicitly selected positional ranges on different sheets/offsets. Three unequal groups exercise both retain-missing and skip-missing acquisition, while paired groups retain their missing positions.

Pooled, Welch and paired t statistics, sample sizes, means, degrees of freedom, P values and confidence limits are compared with the committed base-R oracle in `benchmarks/row-alignment.tsv`. Regenerate it with:

```powershell
Rscript --vanilla tests/DataSelection/benchmarks/row-alignment.R tests/DataSelection/benchmarks/row-alignment.tsv
```

R 4.6.2's `t.test.formula` and `t.test.default` source were inspected: grouping precedes independent missing-value removal; paired data instead use complete pairs. The tests also call the actual recorded-column replay boundary for mixed header decisions and verify workbook, sheet, range, mode and origin metadata. The new acquisition mode persists the row-preserving decision through rereads; legacy numeric acquisition is unchanged.

These console checks do not automate the selection dialog or establish that the reporter's unavailable workbook has this exact layout. The reported error path was independently reproduced against the original 5.3.4 assembly before applying the fix.

Matched observation definitions explicitly opt into row preservation with `preserve-row-alignment`; `same-length` alone is insufficient because it also describes count tables and bar values. The selection mode is retained in worksheet origins so rereading recorded data makes the same decision. Independent samples continue to remove edge missing values independently and may have unequal lengths.

The paired-column regression was also reproduced in the original 5.3.4 assembly: `x = 1:100` with its first value missing and `y = 2*i + (i %% 3)` gave a mean difference of -50 after accepting tail padding, whereas R's complete-pair result is -52, with 99 pairs in both. This is why testing sample sizes alone cannot detect the error.

`MatchedSelectionChecks` invokes the actual equal-length acquisition path and recorded-column replay, compares paired t, signed-rank, Pearson and Spearman calculations with R (including nonmonotone data), and deserializes the real operation definitions to protect the opt-in boundary. Its oracle is regenerated with `Rscript --vanilla tests/DataSelection/benchmarks/matched-selection.R tests/DataSelection/benchmarks/matched-selection.tsv`. An unexpected selection dialog fails the calling test thread rather than waiting for user input.

`GroupedRepeatedSelectionChecks` invokes the actual treatment/block and repeated long-layout readers in both orientations. It covers missing identifiers alongside missing outcomes, blank tails, hidden rows, selected headers, incomplete global blocks and refusal of observations with no identifier. Each retained value and its block or replicate position is checked.
