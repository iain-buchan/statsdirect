# R 4.6.2 gold for the grouped-binomial report's complete-case stacking boundary.
# Omit incomplete rows BEFORE aggregating equal predictor/prior-weight patterns.
out <- if (length(commandArgs(TRUE))) commandArgs(TRUE)[1] else 'logistic-stacking.tsv'
base <- data.frame(x = 0:4, response = c(5, 8, 11, 14, 16), total = 20, prior = 1)
weighted <- base; weighted$prior <- c(1, 2, 1, 3, 1)
duplicate <- rbind(base, data.frame(x = 0, response = 2, total = 10, prior = 1))
individual <- data.frame(x = c(0,0,0,1,1,1,2,2,2,2),
  response = c(0,0,1,1,0,1,1,1,0,1), total = 1, prior = 1)
cases <- list(base = base, weighted = weighted, valid_duplicate = duplicate, individual = individual)
rows <- list()
for (name in names(cases)) {
  dat <- cases[[name]]
  dat <- dat[complete.cases(dat), , drop = FALSE]
  dat <- aggregate(cbind(response, total) ~ x + prior, data = dat, FUN = sum)
  fit <- glm(cbind(response, total - response) ~ x, family = binomial(),
    data = dat, weights = prior, control = glm.control(epsilon = 1e-12, maxit = 200))
  values <- c(intercept = unname(coef(fit)[1]), slope = unname(coef(fit)[2]),
    se_intercept = sqrt(vcov(fit)[1,1]), se_slope = sqrt(vcov(fit)[2,2]),
    dev = deviance(fit), df = df.residual(fit), patterns = nrow(dat))
  rows[[name]] <- data.frame(case = name, key = names(values), value = sprintf('%.17g', values))
}
write.table(do.call(rbind, rows), out, sep = '\t', row.names = FALSE, quote = FALSE)
