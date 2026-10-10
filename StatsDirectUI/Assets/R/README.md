# R recipes

The R scripts that "Continue in R" writes for a result: `analysis-recipes.json` maps engine operation names to recipe files under
`recipes/`, and `recipes/helpers.R` goes into every script before the recipe. The files are shared with the Mac version of StatsDirect
(github.com/iain-buchan/statsdirect-mac, `Content/R`, taken at commit 3fadeba) and are kept the same in both: change them there and
copy them here. A recipe is plain base R that reads the run's data and settings through the helpers (`sd_columns`, `sd_parameter`,
`confidence`); the script writer (`StatsDirectUI/R/RScriptWriter.cs`) puts the settings, the results, the history of the inputs and the
data before it. The checks in `tests/RScript` compare the names a recipe reads with the parameters of the operations it serves.
