# Base-R oracle for separately selected but row-linked worksheet variables.
# lm's model.frame applies missing-value deletion to the assembled pairs.
# weighted.mean.default removes missing x (with its weights) when na.rm=TRUE;
# select complete x/weight pairs first, as StatsDirect does.
args <- commandArgs(trailingOnly = TRUE)
destination <- if (length(args)) args[[1]] else "sequential-selection.tsv"
cases <- list(
  edge = list(x = c(NA, 1, 2, 3, 4), y = c(10, 40, 20, 90, NA)),
  "weighted-edge" = list(x = c(NA, 10, 20, 30, 40), y = c(1, 2, 3, 4, NA)),
  "weighted-second" = list(x = c(5, 8, NA, 20, 25), y = c(1, 2, 3, 4, NA)),
  complete = list(x = 1:12, y = c(5, 3, 10, 8, 9, 12, 7, 19, 13, 23, 17, 28))
)
cases$interior <- cases$complete
cases$interior$x[c(1, 6, 12)] <- NA
cases$interior$y[4] <- NA
cases$hidden <- lapply(cases$interior, function(z) z[-c(2, 9)])
rows <- list()
for (name in names(cases)) {
  d <- cases[[name]]
  fit <- lm(y ~ x, data = as.data.frame(d), na.action = na.omit)
  keep <- complete.cases(d$x, d$y)
  answers <- c(slope = unname(coef(fit)[2]), intercept = unname(coef(fit)[1]),
               pearson = cor(d$x[keep], d$y[keep]),
               weightedmean = weighted.mean(d$x[keep], d$y[keep]))
  for (key in names(answers))
    rows[[length(rows) + 1L]] <- data.frame(case = name, key = key, value = format(answers[[key]], digits = 17))
}
write.table(do.call(rbind, rows), destination, sep = "\t", quote = FALSE, row.names = FALSE)
