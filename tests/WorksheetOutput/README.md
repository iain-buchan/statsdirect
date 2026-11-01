# Checks of output frames written to a picked sheet

A console project that drives the real document host: it opens the main window off screen, without start-up, settings writes, IPC or update dialogs, makes two grids, and calls the host's frame output as the template processor does for an `output-frame` step. The checks are made on a build of the program, as those of `tests/InteractiveRWindows` are: `StatsDirect.dll` of the Release configuration, or of the folder given with `-p:StatsDirectBin=...`.

```
dotnet run -c Release
```

Set `STATSDIRECT_WORKSHEET_OUTPUT_ARTIFACTS` to a folder to have the results written there as `results.json`.

**What is checked.** With the grid of the data active, as it is when the follow-on of a regression is run, and a sheet of another grid picked for the output: the first frame of a step that keeps the selection (`keep-selection="true"`, as the fits and residuals of a multiple linear regression and the fits of LOESS are written) goes to the picked sheet, and the grid of the data is untouched; the frames after it follow it on the picked sheet without adding a sheet; a pick of a new sheet makes one sheet for an operation's frames, not one for each; without a pick a step that keeps the selection still writes after the selection of the active grid; and a pick made afresh, as a later run of the operation makes, is taken again.

**What these checks showed when they were written (1 November 2026).** The host took the selection a step keeps before the sheet the user picked, so the fits and residuals of a multiple linear regression went into the workbook of the data after the current selection whatever sheet was picked, and the pick was ignored; the pick now comes first, and the frames after the first follow it.
