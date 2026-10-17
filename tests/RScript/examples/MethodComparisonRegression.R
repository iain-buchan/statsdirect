# Method comparison regression: the StatsDirect help example (the creatinine data of
# the mcr package, serum and plasma creatinine in 108 subjects; the test workbook's
# Agreement worksheet columns Serum Creatinine and Plasma Creatinine hold the same
# pairs) in R. StatsDirect runs this analysis through the mcr package, so install it
# first with install.packages("mcr")
if (!requireNamespace("mcr", quietly = TRUE)) install.packages("mcr", repos = "https://cloud.r-project.org")
library(mcr)
data(creatinine)
complete <- complete.cases(creatinine)
serum <- creatinine$serum.crea[complete]
plasma <- creatinine$plasma.crea[complete]

# Deming regression of the test method (plasma) on the reference method (serum) with
# an error ratio of 1, its standard errors and confidence intervals from Linnet's
# jackknife: the package's summary, then its coefficient table to 6 places
fit <- mcreg(serum, plasma, error.ratio = 1, method.reg = "Deming",
             method.ci = "jackknife", mref.name = "Serum Creatinine",
             mtest.name = "Plasma Creatinine")
invisible(printSummary(fit))
