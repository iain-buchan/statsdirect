# The Parametric Methods menu: the cases that the reports are given, and what is expected of them, worked out from the
# definitions of the methods.
#  - The t tests, the z tests and the F test: the statistics from the means and variances of the samples, and the P values and
#    confidence limits from the distributions of R.  The degrees of freedom of the test with unequal variances are those of
#    Welch and Satterthwaite.
#  - The confidence limits of a Poisson mean: the means with which the total or more, and the total or fewer, have half of
#    what the confidence level leaves, found as roots of the Poisson probabilities.
#  - The reference range: the mean and as many standard deviations either way as the normal distribution has for the
#    interval, of the values and of their logarithms, with the limits of each end from its standard error,
#    sqrt(s^2 / n + z^2 s^2 / (2 n)); the quantiles are the values at the places q (n + 1) of the sample in order.
#  - Normality: the skewness and the kurtosis from the moments about the mean, with the tests of each; the W of Shapiro and
#    Wilk with its P value, as R has them; the W' of Shapiro and Francia as the squared correlation of the sample in order
#    with the normal scores of its places.  The moments and W are worked out of the values less the first of them, which
#    is exact, so that values far from 0 and near one another keep their figures.
#
# A case (cases.txt): the report, a name, and the inputs as key=value, with semicolons between them; "data" has the columns of
# a table, with bars between the columns and commas between the numbers, and "*" for a number that is missing.
# What is expected (expected.txt): "report|name|output", and the figure; for the rows of a list of the report the output is
# "*list.rows" for their number and "*list[i].output" for an output of row i; "refused" has words of the refusal; "missing" is
# a figure that there is none of; a figure with a comma after it is one that the report has with the comma.
#
# usage: Rscript --vanilla make-benchmarks.R     (in this folder; it takes a few seconds)
args <- c("cases.txt", "expected.txt")
cases <- character(0); expected <- character(0); count <- 0
num <- function(x) if (is.character(x)) x else if (is.na(x)) "missing" else if (is.logical(x)) (if (x) "true" else "false") else if (is.infinite(x)) (if (x > 0) "Infinity" else "-Infinity") else format(x, digits = 17, scientific = abs(x) < 1e-4 && x != 0 || abs(x) >= 1e15, trim = TRUE)
column <- function(x) paste(sapply(x, function(v) if (is.na(v)) "*" else format(v, digits = 17, scientific = abs(v) < 1e-4 && v != 0 || abs(v) >= 1e15, trim = TRUE)), collapse = ",")
frame <- function(...) paste(sapply(list(...), column), collapse = "|")
case <- function(.report, .inputs, ...) {
  count <<- count + 1
  name <- sprintf("c%04d", count)
  cases <<- c(cases, paste(.report, name, paste0(names(.inputs), "=", sapply(.inputs, num), collapse = ";"), sep = "\t"))
  want <- list(...)
  for (k in names(want)) expected <<- c(expected, paste0(.report, "|", name, "|", k, "\t", num(want[[k]])))
}
set.seed(20260929)
levels <- c(0.95, 0.99, 0.8)
# the power of a two sided t test, from the non-central t distribution with both tails, as a number from 0 to 1
t_power <- function(df, ncp, alpha) { tc <- qt(alpha / 2, df, lower.tail = FALSE); suppressWarnings(pt(tc, df, ncp, lower.tail = FALSE) + pt(-tc, df, ncp)) }
# samples: of few and many values, near 0 and far from it, wide and narrow
samples <- list(
  rnorm(12, 50, 8), rnorm(9, 47, 3), rnorm(200, 0, 1), rnorm(30, 1e8, 2), rnorm(25, 1e8 + 1, 2), rexp(40, 0.1), round(rnorm(15, 120, 15)),
  c(3.1, 4.7), rnorm(60, -5, 1e-6), rnorm(8, 1e-150, 1e-151), rnorm(500, 1e12, 1e5), c(1, 2, 3, 4, 100))
pairs <- list(c(1, 2), c(1, 3), c(4, 5), c(6, 7), c(8, 2), c(3, 9), c(10, 10), c(11, 4), c(12, 1), c(2, 1))

# ---- unpaired t test and F (variance ratio) test, from data and from a summary
for (p in pairs) for (level in levels) {
  x <- samples[[p[1]]]; y <- samples[[p[2]]]; if (p[1] == p[2]) y <- y * 1.5 + y[1]
  n1 <- length(x); n2 <- length(y); m1 <- mean(x); m2 <- mean(y); v1 <- var(x); v2 <- var(y); a <- 1 - level
  df <- n1 + n2 - 2; pooled <- ((n1 - 1) * v1 + (n2 - 1) * v2) / df; se <- sqrt(pooled * (1 / n1 + 1 / n2)); t <- (m1 - m2) / se
  sw <- sqrt(v1 / n1 + v2 / n2); tw <- (m1 - m2) / sw; dfw <- (v1 / n1 + v2 / n2)^2 / ((v1 / n1)^2 / (n1 - 1) + (v2 / n2)^2 / (n2 - 1))
  want <- list(mean_0 = m1, mean_1 = m2, n0 = n1, n1 = n2, error = se, df = df, t = t, p_1 = pt(-abs(t), df), p_2 = 2 * pt(-abs(t), df),
               from = m1 - m2 - qt(a / 2, df, lower.tail = FALSE) * se, to = m1 - m2 + qt(a / 2, df, lower.tail = FALSE) * se,
               error_unequal = sw, df_unequal = dfw, t_unequal = tw, p_1_unequal = pt(-abs(tw), dfw), p_2_unequal = 2 * pt(-abs(tw), dfw),
               from_unequal = m1 - m2 - qt(a / 2, dfw, lower.tail = FALSE) * sw, to_unequal = m1 - m2 + qt(a / 2, dfw, lower.tail = FALSE) * sw)
  do.call(case, c(list("RptTUnpaired", list(data = frame(x, y), gamma = level)), want))
  do.call(case, c(list("RptTUnpairedSummary", list(gamma = level, nx1 = n1, um1 = m1, sd1 = sqrt(v1), nx2 = n2, um2 = m2, sd2 = sqrt(v2))), want))
  if (level == 0.95) {
    top <- if (v1 > v2) 1 else 2
    f <- max(v1, v2) / min(v1, v2); d1 <- if (top == 1) n1 - 1 else n2 - 1; d2 <- if (top == 1) n2 - 1 else n1 - 1
    up <- pf(f, d1, d2, lower.tail = FALSE)
    case("RptVarianceRatio", list(data = frame(x, y)), f = f, df_0 = d1, df_1 = d2, var_0 = max(v1, v2), var_1 = min(v1, v2), p_1 = up, p_2 = 2 * min(up, pf(f, d1, d2)))
  }
}
# ---- single sample and paired t tests
for (i in seq_along(samples)) for (level in levels) for (mu0 in c(0, 48)) {
  x <- samples[[i]]; n <- length(x); m <- mean(x); s <- sd(x); se <- s / sqrt(n); a <- 1 - level; t <- (m - mu0) / se; tq <- qt(a / 2, n - 1, lower.tail = FALSE)
  want <- list(sam_mean = m, size = n, sd = s, df = n - 1, t = t, p_1 = pt(-abs(t), n - 1), p_2 = 2 * pt(-abs(t), n - 1), from = m - mu0 - tq * se, to = m - mu0 + tq * se)
  do.call(case, c(list("RptTSingle", list(data = frame(x), `population-mean` = mu0, gamma = level)), want))
  do.call(case, c(list("RptTSingleSummary", list(gamma = level, nx = n, mu = m, sd1 = s, mu0 = mu0)), want))
}
for (i in c(1, 3, 4, 6, 7, 8, 12)) for (level in levels) {
  x <- samples[[i]]; y <- x + rnorm(length(x), 0.4, 1) * sd(x) / 3
  if (i == 3) y[c(5, 17)] <- NA
  if (i == 6) x[2] <- NA
  d <- (x - y)[!is.na(x - y)]; n <- length(d); m <- mean(d); s <- sd(d); se <- s / sqrt(n); a <- 1 - level; t <- m / se; tq <- qt(a / 2, n - 1, lower.tail = FALSE)
  case("RptTPaired", list(data = frame(x, y), gamma = level, doAgreement = FALSE), mean = m, n = n, sd = s, sem = se, df = n - 1, t = t, tail_1 = pt(-abs(t), n - 1), tail_2 = 2 * pt(-abs(t), n - 1), from = m - tq * se, to = m + tq * se)
  # the differences themselves, in one column
  dd <- x - y
  case("RptTPaired", list(data = frame(dd), gamma = level, doAgreement = FALSE), mean = m, n = n, sd = s, t = t, tail_2 = 2 * pt(-abs(t), n - 1))
}
# ---- normal distribution (z) tests
for (i in c(1, 3, 4, 6, 11)) for (level in levels) for (known in list(NULL, 7.5)) {
  x <- samples[[i]]; n <- length(x); m <- mean(x); s <- sd(x); mu0 <- 48; a <- 1 - level; zq <- qnorm(a / 2, lower.tail = FALSE)
  se <- (if (is.null(known)) s else known) / sqrt(n); zs <- (m - mu0) / se
  want <- list(mean = m, size = n, sd = s, z = zs, p_1 = pnorm(-abs(zs)), p_2 = 2 * pnorm(-abs(zs)), from = m - mu0 - zq * se, to = m - mu0 + zq * se)
  if (all(x > 0)) { l <- log(x); want <- c(want, list(gmean = exp(mean(l)), lrr = exp(mean(l) - zq * sd(l)), urr = exp(mean(l) + zq * sd(l)))) }
  do.call(case, c(list("RptZSingle", c(list(data = frame(x), gamma = level, popmean = mu0), if (is.null(known)) list() else list(popsd = known))), want))
}
for (p in pairs[c(1, 3, 4, 9)]) for (level in levels) {
  x <- samples[[p[1]]]; y <- samples[[p[2]]]; a <- 1 - level; zq <- qnorm(a / 2, lower.tail = FALSE)
  se <- sqrt(var(x) / length(x) + var(y) / length(y)); zs <- (mean(x) - mean(y)) / se
  case("RptZUnpaired", list(data = frame(x, y), gamma = level), error = se, z = zs, p_1 = pnorm(-abs(zs)), p_2 = 2 * pnorm(-abs(zs)), from = mean(x) - mean(y) - zq * se, to = mean(x) - mean(y) + zq * se,
       `*sample[1].mean` = mean(x), `*sample[1].var` = var(x), `*sample[2].size` = length(y), `*warn.rows` = if (length(x) < 30 || length(y) < 30) 1 else 0)
}
# ---- the confidence limits of the mean of counts that have a Poisson distribution: the means with which the total or more, and
# the total or fewer, have half of what the confidence level leaves (for the one sided limits, all of it)
poisson_limits <- function(total, n, tail) {
  lower <- if (total == 0) 0 else uniroot(function(l) ppois(total - 1, exp(l), lower.tail = FALSE, log.p = TRUE) - log(tail), log(total) + c(-40, 5), tol = 1e-14)$root
  upper <- uniroot(function(l) ppois(total, exp(l), log.p = TRUE) - log(tail), log(total + 1) + c(-5, 40), tol = 1e-14)$root
  c(if (total == 0) 0 else exp(lower) / n, exp(upper) / n)
}
for (level in c(0.95, 0.99, 0.6)) {
  counts <- list(c(3, 0, 2, 5, 1), c(0, 0, 0), c(1), c(120, 98, 143, 110), c(2500000, 2600000), c(0, 1, 0, 0))
  want <- list(`*sample.rows` = length(counts))
  for (j in seq_along(counts)) {
    x <- counts[[j]]; two <- poisson_limits(sum(x), length(x), (1 - level) / 2); one <- poisson_limits(sum(x), length(x), 1 - level)
    want[[paste0("*sample[", j, "].mean")]] <- mean(x); want[[paste0("*sample[", j, "].n")]] <- length(x)
    # the level of the two sided interval, and that of each of the one sided limits, which is the same
    want[[paste0("*sample[", j, "].pc2")]] <- 100 * level; want[[paste0("*sample[", j, "].pc1")]] <- 100 * level
    want[[paste0("*sample[", j, "].lower2")]] <- two[1]; want[[paste0("*sample[", j, "].upper2")]] <- two[2]
    want[[paste0("*sample[", j, "].lower1")]] <- one[1]; want[[paste0("*sample[", j, "].upper1")]] <- one[2]
  }
  do.call(case, c(list("RptPoissonConfidenceInterval", list(data = do.call(frame, counts), gamma = level)), want))
}
# ---- reference range: of a normal and of a log-normal distribution, with the confidence limits of each end, and the quantiles
for (i in c(1, 3, 4, 6, 7, 11)) for (level in c(0.95, 0.9)) for (cover in c(0.95, 0.9, 0.5)) {
  x <- samples[[i]]; n <- length(x); m <- mean(x); s <- sd(x); zr <- qnorm((1 - cover) / 2, lower.tail = FALSE); zc <- qnorm((1 - level) / 2, lower.tail = FALSE)
  se <- sqrt(s^2 / n + zr^2 * s^2 / (2 * n))
  # the quantile at the part q of a sample in order: the value at the place q (n + 1), between two values in proportion
  at <- function(q) { v <- sort(x); h <- min(max(q * (n + 1), 1), n); v[floor(h)] + (if (h > floor(h)) (v[floor(h) + 1] - v[floor(h)]) * (h - floor(h)) else 0) }
  want <- list(mean = m, size = n, sd = s, lrr = m - zr * s, urr = m + zr * s, lx_l = m - zr * s - zc * se, ux_l = m - zr * s + zc * se, lx_u = m + zr * s - zc * se, ux_u = m + zr * s + zc * se,
               qxv_any = at((1 - cover) / 2), qxv = at(1 - (1 - cover) / 2))
  if (all(x > 0)) {
    l <- log(x); ml <- mean(l); sl <- sd(l); sel <- sqrt(sl^2 / n + zr^2 * sl^2 / (2 * n))
    want <- c(want, list(`*lognormal.rows` = 1, `*lognormal[1].lrr_lognormal` = exp(ml - zr * sl), `*lognormal[1].urr_lognormal` = exp(ml + zr * sl),
                         `*lognormal[1].lx_l_lognormal` = exp(ml - zr * sl - zc * sel), `*lognormal[1].ux_u_lognormal` = exp(ml + zr * sl + zc * sel)))
  }
  do.call(case, c(list("RptReferenceRange", list(data = frame(x), do_conservative = TRUE, gamma = level, `reference-interval` = cover)), want))
}
# ---- normality: the skewness and the kurtosis of the moments about the mean, with the tests of each, and the test of Shapiro
# and Wilk
skew_p <- function(g1, n) {
  y <- g1 * sqrt((n + 1) * (n + 3) / (6 * (n - 2)))
  b <- 3 * (n^2 + 27 * n - 70) * (n + 1) * (n + 3) / ((n - 2) * (n + 5) * (n + 7) * (n + 9))
  w2 <- -1 + sqrt(2 * (b - 1)); d <- 1 / sqrt(log(sqrt(w2))); al <- sqrt(2 / (w2 - 1))
  2 * pnorm(-abs(d * asinh(y / al)))
}
kurt_p <- function(b2, n) {
  e <- 3 * (n - 1) / (n + 1); v <- 24 * n * (n - 2) * (n - 3) / ((n + 1)^2 * (n + 3) * (n + 5)); x <- (b2 - e) / sqrt(v)
  rb <- 6 * (n^2 - 5 * n + 2) / ((n + 7) * (n + 9)) * sqrt(6 * (n + 3) * (n + 5) / (n * (n - 2) * (n - 3)))
  A <- 6 + 8 / rb * (2 / rb + sqrt(1 + 4 / rb^2))
  zk <- ((1 - 2 / (9 * A)) - ((1 - 2 / A) / (1 + x * sqrt(2 / (A - 4))))^(1 / 3)) / sqrt(2 / (9 * A))
  2 * pnorm(-abs(zk))
}
normal_samples <- list(samples[[1]], samples[[3]], samples[[6]], samples[[7]], samples[[4]], samples[[11]], samples[[12]], c(2.1, 3.4, 1.9, 5.6), rexp(1500, 2), runif(90))
want <- list(`*variable.rows` = length(normal_samples))
for (j in seq_along(normal_samples)) {
  # the differences from the mean are taken of the values less the first of them, which is exact: with values of 1e8 that
  # differ by 2 the mean of the values themselves is not held to enough figures for the third and fourth moments
  x <- normal_samples[[j]]; n <- length(x); x0 <- x - x[1]; d <- x0 - mean(x0); m2 <- mean(d^2); g1 <- mean(d^3) / m2^1.5; b2 <- mean(d^4) / m2^2
  k <- paste0("*variable[", j, "].")
  want[[paste0(k, "n")]] <- n; want[[paste0(k, "mean")]] <- mean(x); want[[paste0(k, "sd")]] <- sd(x)
  want[[paste0(k, "skewness")]] <- if (n >= 8) paste0(format(g1, digits = 17), ",") else g1
  want[[paste0(k, "kurtosis")]] <- if (n >= 8) paste0(format(b2, digits = 17), ",") else b2
  if (n >= 8) { want[[paste0(k, "b1_p")]] <- skew_p(g1, n); want[[paste0(k, "b2_p")]] <- kurt_p(b2, n) }
  sw <- shapiro.test(x0)
  want[[paste0(k, "sw_w")]] <- paste0(format(unname(sw$statistic), digits = 17), ","); want[[paste0(k, "sw_p")]] <- sw$p.value
  if (n >= 5) { o <- sort(x0); sc <- qnorm((seq_len(n) - 0.375) / (n + 0.25)); want[[paste0(k, "sf_w")]] <- paste0(format(cor(o, sc)^2, digits = 17), ",") }
}
do.call(case, c(list("RptNormality", list(data = do.call(frame, normal_samples))), want))
# ---- a confidence level that is not above 0 and below 1 is taken as 0.95
for (level in c(0, 1, 95, -0.5)) {
  x <- samples[[1]]; y <- samples[[2]]; n1 <- length(x); n2 <- length(y); df <- n1 + n2 - 2
  se <- sqrt(((n1 - 1) * var(x) + (n2 - 1) * var(y)) / df * (1 / n1 + 1 / n2)); tq <- qt(0.025, df, lower.tail = FALSE)
  case("RptTUnpaired", list(data = frame(x, y), gamma = level), pc = 95, from = mean(x) - mean(y) - tq * se, to = mean(x) - mean(y) + tq * se)
  case("RptTUnpairedSummary", list(gamma = level, nx1 = n1, um1 = mean(x), sd1 = sd(x), nx2 = n2, um2 = mean(y), sd2 = sd(y)), pc = 95, from = mean(x) - mean(y) - tq * se, to = mean(x) - mean(y) + tq * se)
  s1 <- sd(x) / sqrt(n1); t1 <- qt(0.025, n1 - 1, lower.tail = FALSE)
  case("RptTSingle", list(data = frame(x), `population-mean` = 48, gamma = level), pc = 95, from = mean(x) - 48 - t1 * s1, to = mean(x) - 48 + t1 * s1)
  case("RptTSingleSummary", list(gamma = level, nx = n1, mu = mean(x), sd1 = sd(x), mu0 = 48), pc = 95, from = mean(x) - 48 - t1 * s1, to = mean(x) - 48 + t1 * s1)
  case("RptZSingle", list(data = frame(x), gamma = level, popmean = 48), pc = 95, from = mean(x) - 48 - qnorm(0.975) * s1, to = mean(x) - 48 + qnorm(0.975) * s1)
  sz <- sqrt(var(x) / n1 + var(y) / n2)
  case("RptZUnpaired", list(data = frame(x, y), gamma = level), pc = 95, from = mean(x) - mean(y) - qnorm(0.975) * sz, to = mean(x) - mean(y) + qnorm(0.975) * sz)
  d <- x[1:9] - y; sp <- sd(d) / 3; tp <- qt(0.025, 8, lower.tail = FALSE)
  case("RptTPaired", list(data = frame(x[1:9], y), gamma = level, doAgreement = FALSE), pc = 95, from = mean(d) - tp * sp, to = mean(d) + tp * sp)
}
# ---- two columns of different length: the rows that one of them does not have are without a pair
for (long_first in c(TRUE, FALSE)) {
  x <- c(1, 2, 3, 4); y <- c(2, 3.5); d <- x[1:2] - y
  inputs <- list(data = if (long_first) frame(x, y) else frame(y, x), gamma = 0.95, doAgreement = FALSE)
  s <- if (long_first) 1 else -1
  case("RptTPaired", inputs, n = 2, mean = s * mean(d), sd = sd(d), t = s * mean(d) / (sd(d) / sqrt(2)), tail_2 = 2 * pt(-abs(mean(d) / (sd(d) / sqrt(2))), 1))
}
# ---- the geometric mean and its range are given only if every value is above 0
case("RptZSingle", list(data = frame(c(5, 6, -7, 0, 3)), gamma = 0.95, popmean = 4), mean = 1.4, gmean = NA, lrr = NA, urr = NA)
case("RptZSingle", list(data = frame(c(5, 6, 7, NA, 3)), gamma = 0.95, popmean = 4), size = 4, gmean = exp(mean(log(c(5, 6, 7, 3)))), lrr = exp(mean(log(c(5, 6, 7, 3))) - qnorm(0.975) * sd(log(c(5, 6, 7, 3)))))
# ---- values that are all the same, and a sample of one value
case("RptTSingle", list(data = frame(c(5, 5, 5)), `population-mean` = 4, gamma = 0.95), sd = 0, t = Inf, p_2 = 0)
case("RptTSingle", list(data = frame(c(5, 5, 5)), `population-mean` = 5, gamma = 0.95), sd = 0, t = NA, p_2 = NA)
case("RptVarianceRatio", list(data = frame(c(3, 3, 3, 3), c(4.7, 5.2, 6.1))), f = Inf, var_1 = 0, p_1 = 0)
case("RptTUnpaired", list(data = frame(c(3.1), c(4.7, 5.2, 6.1)), gamma = 0.95), n0 = 1, df = 2, t = (3.1 - mean(c(4.7, 5.2, 6.1))) / sqrt(var(c(4.7, 5.2, 6.1)) * (1 + 1 / 3)), t_unequal = NA,
     say1 = "F test not calculated: a variance is zero or could not be calculated, or a sample has fewer than two values")
case("RptTUnpaired", list(data = frame(c(1e200, 2e200, 3e200), c(4e200, 5e200, 7e200)), gamma = 0.95), t = NA, say1 = "F test not calculated: a variance is zero or could not be calculated, or a sample has fewer than two values")
# ---- counts that are not whole numbers, or are below 0, have a warning
case("RptPoissonConfidenceInterval", list(data = frame(c(1.5, 2, 3)), gamma = 0.95), `*sample[1].warn` = "(warning - source data not integers)", `*sample[1].mean` = 6.5 / 3)
case("RptPoissonConfidenceInterval", list(data = frame(c(1, 2, -1)), gamma = 0.95), `*sample[1].warn` = "(error - negative values used)", `*sample[1].lower2` = NA, `*sample[1].upper2` = NA)
case("RptPoissonConfidenceInterval", list(data = frame(c(3, 4)), gamma = 0.4), `*sample[1].pc1` = 40, `*sample[1].lower1` = NA, `*sample[1].upper1` = NA, `*sample[1].lower2` = poisson_limits(7, 2, 0.3)[1])

# ---- what is refused, and the words of the refusal
refused <- function(.report, .inputs, .message) case(.report, .inputs, refused = .message)
two <- "must be at least two members in each sample with standard deviations above zero"
one <- "must be at least two members in the sample with a standard deviation above zero"
for (odd in list(c(1, 2, 10, 2), c(10, 0, 10, 2), c(10, -2, 10, 2), c(10, 2, 10, -0.001), c(10, 2, 0, 2)))
  refused("RptTUnpairedSummary", list(gamma = 0.95, nx1 = odd[1], um1 = 5, sd1 = odd[2], nx2 = odd[3], um2 = 6, sd2 = odd[4]), two)
for (odd in list(c(1, 2), c(10, 0), c(10, -2))) refused("RptTSingleSummary", list(gamma = 0.95, nx = odd[1], mu = 5, sd1 = odd[2], mu0 = 4), one)
refused("RptZSingle", list(data = frame(c(5, 6, 7)), gamma = 0.95, popmean = 4, popsd = -2), "The population standard deviation must be positive")
refused("RptReferenceRange", list(data = frame(1:7), do_conservative = TRUE, gamma = 0.95, `reference-interval` = 0.95), "Too few data for this method (minimum 8)")
for (cover in c(0, 1, 95, -0.2)) refused("RptReferenceRange", list(data = frame(1:8), do_conservative = TRUE, gamma = 0.95, `reference-interval` = cover), "the reference interval must be greater than 0% and less than 100%")

writeLines(cases, args[1]); writeLines(expected, args[2])
cat(length(cases), "cases,", length(expected), "figures\n")
