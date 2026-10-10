# R recipes

The analyses that "Continue in R" writes for a result. `analysis-recipes.json` maps engine operation names to recipe files under
`recipes/`. A script for a result is short: the run's data as named vectors (or a matrix of counts), the settings as named values,
then the recipe's analysis, so that the user can run it, edit it, or take it to R or RStudio.

A recipe is the analysis as the help's R code shows it for that method, which reproduces the figures of the help's worked example. It
starts with a header that names the variables it takes; the script writer (`StatsDirectUI/R/RScriptWriter.cs`) writes the run's values
under those names before the body:

    # data: farm, town                 the columns of the run's frames, in order, as vectors; "rest..." takes any further columns as a named list
    # table: counts                    a 2 by 2 table typed in, as a matrix with the labels of its rows and columns
    # matrix: counts                   a frame of counts, its columns the columns of a matrix
    # settings: confidence = gamma     variables from the run's parameters (a confidence level as a proportion)

The recipes were generated from the help's R sections (the topic of each operation, through `Data/Alias.xml`), with the example's data
and settings taken out and the confidence level taken from the run; files marked `# hand-edited` were adjusted by hand after that. The
checks in `tests/RScript` run every recipe on the help's own example, through a run's record, and compare its output with the help's
script, and run the paired t test and the 2 by 2 chi-square against the engine. The files are shared with the Mac version of
StatsDirect, which keeps them the same.
