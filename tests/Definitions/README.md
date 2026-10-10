# Checks of the operation definitions

A console project that reads the operation definitions in `StatsDirectUI/Assets/Operations` and checks what can be checked without a build of the program. The folder is the repository's unless another is given with `-p:Operations=...`.

```
dotnet run -c Release
```

**What is checked.** A parameter may be asked for on a condition (`acquire-if-true`), an expression over the parameters acquired so far. A condition that names a parameter which is itself asked for only on a condition must first ask whether it is there (`parameters.ContainsKey`), because the bag's indexer throws "Cannot find parameter" for one that is not, and the operation stops with an internal error. Every definition's conditions are read and any such condition is reported.

**What this check showed when it was written (9 October 2026, from the Mac port).** LOESS curve fitting asked for the confidence level on the condition `parameters["plotFitsAndCi"].AsBoolean`, but that parameter is asked for only with one predictor, so with two or more the operation stopped with "Internal error: Cannot find parameter 'plotFitsAndCi'" before the fit. No other definition had a condition of this kind.
