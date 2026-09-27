# Sets of studies for the other analyses of the Meta-analysis menu, written as a file of cases:
#   rate   events and person-time of the first group, events and person-time of the second
#   cont   number, mean and standard deviation of the first group, and of the second
#   prop   events and number
#   cor    correlation and number
#   gen    statistic, standard error, lower and upper limit (the limits at the level of the case, on the plain scale)
args <- commandArgs(trailingOnly = TRUE)
out <- file(args[1], "w")
put <- function(name, kind, x, level = 0.95) {
  cat(sprintf("case\t%s\t%s\t%d\t%s\n", name, kind, nrow(x), format(level)), file = out)
  write.table(format(x, digits = 15, trim = TRUE), out, sep = "\t", row.names = FALSE, col.names = FALSE, quote = FALSE)
}
set.seed(8128)
levels <- c(0.95, 0.9, 0.99)
n <- 0
for (k in c(1, 2, 3, 4, 6, 10)) for (size in c(30, 300, 5000)) for (rep in 1) {
  n <- n + 1
  level <- levels[1 + n %% 3]
  # rates
  t1 <- round(size * exp(rnorm(k, 0, 0.6)), 1) + 1; t2 <- round(size * exp(rnorm(k, 0, 0.6)), 1) + 1
  h <- exp(rnorm(k, log(0.08), 0.4))
  put(sprintf("rate%03d", n), "rate", cbind(rpois(k, t1 * h * 1.3), t1, rpois(k, t2 * h), t2), level)
  # continuous
  ne <- round(size / 5 * exp(rnorm(k, 0, 0.5))) + 3; nc <- round(size / 5 * exp(rnorm(k, 0, 0.5))) + 3
  se <- round(runif(k, 4, 12), 2); sc <- round(se * runif(k, 0.7, 1.4), 2)
  me <- round(50 + rnorm(k, 3, 3) + rnorm(k, 0, se / sqrt(ne)), 2); mc <- round(50 + rnorm(k, 0, sc / sqrt(nc)), 2)
  put(sprintf("cont%03d", n), "cont", cbind(ne, me, se, nc, mc, sc), level)
  # proportions
  m <- round(size / 3 * exp(rnorm(k, 0, 0.7))) + 2
  p <- plogis(rnorm(k, qlogis(c(0.02, 0.3, 0.9)[1 + n %% 3]), 0.5))
  put(sprintf("prop%03d", n), "prop", cbind(rbinom(k, m, p), m), level)
  # correlations
  m <- round(size / 3 * exp(rnorm(k, 0, 0.7))) + 5
  r <- round(tanh(rnorm(k, atanh(c(0.3, -0.2, 0.7)[1 + n %% 3]), 0.15) + rnorm(k, 0, 1 / sqrt(m - 3))), 3)
  put(sprintf("cor%03d", n), "cor", cbind(r, m), level)
  # a statistic with its standard error
  s <- round(exp(rnorm(k, log(0.3), 0.6)), 4)
  y <- round(exp(rnorm(k, 0.2, 0.2) + rnorm(k, 0, s) / 3), 4)
  z <- qnorm(1 - (1 - level) / 2)
  put(sprintf("gen%03d", n), "gen", cbind(y, s, y - z * s, y + z * s), level)
}
# at the limits
put("rate.zero", "rate", cbind(c(0, 3, 5, 0, 7, 2), c(120, 150, 90, 60, 200, 80), c(2, 0, 4, 0, 9, 1), c(110, 160, 95, 70, 210, 75)))
put("rate.same", "rate", cbind(c(4, 4, 4, 4), c(100, 100, 100, 100), c(4, 4, 4, 4), c(100, 100, 100, 100)))
put("rate.equal", "rate", cbind(c(5, 10, 8, 3), c(100, 200, 160, 60), c(10, 5, 4, 6), c(200, 100, 80, 120)))
put("cont.same", "cont", cbind(rep(20, 4), rep(10, 4), rep(2, 4), rep(20, 4), rep(9, 4), rep(2, 4)))
put("cont.nosd", "cont", cbind(c(20, 25, 30, 18), c(10, 11, 9, 12), c(0, 2, 3, 2), c(20, 25, 30, 18), c(9, 9, 9, 9), c(0, 2, 2, 3)))
put("cont.small", "cont", cbind(c(2, 3, 2, 4, 3), c(10, 11, 9, 12, 10), c(1, 2, 3, 2, 1), c(2, 2, 3, 3, 4), c(9, 9, 9, 9, 9), c(1, 2, 2, 3, 2)))
put("prop.none", "prop", cbind(c(0, 0, 0, 0, 0), c(12, 40, 25, 18, 60)))
put("prop.all", "prop", cbind(c(12, 40, 25, 18, 60), c(12, 40, 25, 18, 60)))
put("prop.mixed", "prop", cbind(c(0, 40, 1, 18, 0, 7), c(12, 40, 25, 18, 60, 7)))
put("prop.ones", "prop", cbind(c(0, 1, 1, 0, 1), c(1, 1, 1, 1, 1)))
put("cor.same", "cor", cbind(rep(0.4, 5), rep(30, 5)))
put("cor.zero", "cor", cbind(c(0, 0.1, -0.1, 0, 0.05), c(30, 40, 50, 60, 70)))
put("cor.high", "cor", cbind(c(0.99, 0.95, -0.98, 0.999, 0.9), c(10, 8, 12, 6, 20)))
put("gen.same", "gen", cbind(rep(1.5, 5), rep(0.2, 5), rep(1.5 - 1.96 * 0.2, 5), rep(1.5 + 1.96 * 0.2, 5)))
close(out)
cat("cases written to", args[1], "\n")
