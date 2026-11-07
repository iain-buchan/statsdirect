# R model.frame builds row-linked numeric/factor predictors before complete-case omission.
args <- commandArgs(trailingOnly = TRUE)
destination <- if (length(args)) args[[1]] else "mixed-selection.tsv"
cases <- list(
  "numeric-edge" = data.frame(x = c(NA,1,2,3,4), y = c(10,40,20,90,NA)),
  "categorical-binary" = data.frame(group = factor(c("A","B",NA,"B","A")), y = c(1,2,20,2,1)),
  "categorical-firstna" = data.frame(group = factor(c(NA,"A","B","A","B","C")), y = c(999,3,8,4,6,11)),
  "categorical-three" = data.frame(group = factor(c("A","B","C","A",NA,"B","C","A","B")),
                                 y = c(3,7,12,5,999,8,14,4,6))
)
cases[["mixed-design"]] <- data.frame(z = c(1.1,2.5,1.7,3.3,4.1,6.2,5.3,7.4,8.6),
                                    cases[["categorical-three"]])
cases[["mixed-hidden"]] <- cases[["mixed-design"]][-2,]
rows <- list()
for (name in names(cases)) {
  fit <- lm(y ~ ., data = cases[[name]], na.action = na.omit)
  answers <- c(n = nobs(fit), setNames(unname(coef(fit)), paste0("b", seq_along(coef(fit)) - 1L)))
  for (key in names(answers))
    rows[[length(rows) + 1L]] <- data.frame(case = name, key = key, value = format(answers[[key]], digits = 17))
}
write.table(do.call(rbind, rows), destination, sep = "\t", quote = FALSE, row.names = FALSE)
