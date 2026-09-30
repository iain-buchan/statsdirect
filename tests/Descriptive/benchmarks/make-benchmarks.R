# The Descriptive menu: the cases that the reports are given, and what is expected of them, worked out from the definitions
# of the statistics.
#  - The univariate summary, with weights and without: the mean, the variance and the moments about the mean of the values
#    less the first of them, in units of the greatest difference, which keeps the figures of values far from 0 and of values
#    that are very large or very small; the confidence limits from Student's t; the geometric mean from the logarithms.
#  - The quantiles, from their definitions.  Centile type 1 is worked out in whole numbers: the part p is a fraction, the
#    weights are whole numbers, and whether the sum of the weights so far is above p n, or is p n, is a comparison of whole
#    numbers.  Centile type 2 is the value at the place p (n + 1) of the values in order.
#  - The time series summary: the table of times by subjects of each group; of each time the mean, the standard deviation
#    and the quantiles of its observations; of each subject the area under the curve by the trapezium rule, the greatest
#    observation with its time, and the slope of the least squares line up to it; of each group the mean area with its
#    limits from Student's t and from the normal distribution; of two groups the t test for unequal variances; the squared
#    correlation of the areas, and of their logarithms, with the normal scores of their ranks.
#  - The bootstrap of the time series summary is made here with random numbers of R, 10 times: the limits and the P value of
#    the program are made with other random numbers, and are to be within 4.5 standard deviations of the 10 of their mean.
#
# A case (cases.txt, bootstrap-cases.txt): the report, a name, and the inputs as key=value, with semicolons between them; a
# table of numbers has bars between its columns and commas between the numbers, and "*" for a number that is missing; a
# column of labels has commas between the labels.
# What is expected (expected.txt): "report|name|output", and the figure; for the rows of a list of the report the output is
# "*list.rows" for their number and "*list[i].output" for an output of row i; "text.name" is a line of the quick summary;
# "refused" has words of the refusal; "missing" is a figure that there is none of.  Of the bootstrap (bootstrap-expected.txt):
# the mean of the 10 and their standard deviation.
#
# usage: Rscript --vanilla make-benchmarks.R     (in this folder; it takes about a minute, most of it for the bootstrap)
args <- c("cases.txt", "expected.txt")
cases <- character(0); expected <- character(0); count <- 0
fig <- function(v) format(v, digits = 17, scientific = abs(v) < 1e-4 && v != 0 || abs(v) >= 1e15, trim = TRUE)
num <- function(x) if (is.character(x)) x else if (is.na(x)) "missing" else if (is.logical(x)) (if (x) "true" else "false") else if (is.infinite(x)) (if (x > 0) "Infinity" else "-Infinity") else fig(x)
column <- function(x) paste(sapply(x, function(v) if (is.na(v)) "*" else fig(v)), collapse = ",")
frame <- function(...) paste(sapply(list(...), column), collapse = "|")
labels <- function(x) paste(x, collapse = ",")
case <- function(.report, .inputs, .want) {
  count <<- count + 1
  name <- sprintf("c%04d", count)
  cases <<- c(cases, paste(.report, name, paste0(names(.inputs), "=", sapply(.inputs, num), collapse = ";"), sep = "\t"))
  for (k in names(.want)) expected <<- c(expected, paste0(.report, "|", name, "|", k, "\t", num(.want[[k]])))
  invisible(name)
}
set.seed(20260930)

# ---- the quantiles, from their definitions
# centile type 1: the values in order, each with its weight; the weights are made to sum to the number of values; the
# quantile at the part p is the first value at which the sum of the weights so far is above p n, or, if the sum before it is
# p n exactly, the mean of that value and the one before it.  The comparisons are of whole numbers: p is num / den, the weights
# are whole numbers, and "the sum so far is above p n" is "the sum of the weights so far times den is above num times their total".
type1 <- function(x, v, num, den) {
  o <- order(x); u <- x[o]; cs <- cumsum(v[o]); total <- sum(v); n <- length(x)
  if (num == 0) return(u[1])
  if (num == den) return(u[n])
  above <- which(cs * den > num * total)
  i <- if (length(above)) above[1] else n
  if (i > 1 && cs[i - 1] * den == num * total) (u[i - 1] + u[i]) / 2 else u[i]
}
# centile type 2: the value at the place p (n + 1) of the values in order, between two values in proportion
type2 <- function(x, num, den) {
  u <- sort(x); n <- length(u); m <- max(min(num / den * (n + 1), n), 1); i <- floor(m); h <- m - i
  if (i >= n) u[n] else (1 - h) * u[i] + h * u[i + 1]
}

# ---- the summary of a sample, with weights or without: v are the weights as they are given (whole numbers)
titles <- c("Valid data", "Missing data", "Sum", "Mean", "Variance", "Standard deviation", "Variance coefficient", "Standard error of mean",
            "Upper CL", "Lower CL", "Geometric mean", "Skewness", "Kurtosis", "Maximum", "Upper quartile", "Median", "Lower quartile",
            "Interquartile range", "Minimum", "Range", "Centile b", "Centile a")
summary_of <- function(x, v = NULL, level = 0.95, kind = 1, a = c(50, 1000), b = c(950, 1000)) {
  weighted <- !is.null(v)
  if (!weighted) v <- rep(1, length(x))
  rows <- length(x)
  keep <- !is.na(x) & !is.na(v)
  x <- x[keep]; v <- v[keep]; n <- length(x)
  if (!(level > 0 && level < 1)) level <- 0.95
  s <- list(valid = n, missing = rows - n)
  if (n == 0) return(c(s, list(sum = NA, mean = NA, var = NA, sd = NA, vc = NA, sem = NA, ucl = NA, lcl = NA, gm = NA, skew = NA, kurt = NA, max = NA, uq = NA, median = NA, lq = NA, iqr = NA, min = NA, range = NA, cb = NA, ca = NA, weights = 0)))
  w <- v * n / sum(v)
  s$weights <- sum(v)
  s$sum <- sum(w * x)
  # the values less the first of them, which is exact for values near one another, and the differences from their mean in
  # units of the greatest of them
  y <- x - x[1]
  s$mean <- x[1] + sum(w * y) / n
  d <- y - sum(w * y) / n
  unit <- if (max(abs(d)) > 0) max(abs(d)) else 1
  d <- d / unit
  d <- d - sum(w * d) / n
  s$sd <- if (n > 1) sqrt(sum(w * d^2) / (n - 1)) * unit else NA
  s$var <- s$sd^2
  if (!is.na(s$var) && is.infinite(s$var)) s$var <- NA
  s$vc <- if (!is.na(s$sd) && s$mean != 0) s$sd / s$mean else NA
  s$sem <- s$sd / sqrt(n)
  tq <- if (n > 1) qt((1 - level) / 2, n - 1, lower.tail = FALSE) else NA
  s$ucl <- s$mean + tq * s$sem; s$lcl <- s$mean - tq * s$sem
  s$gm <- if (all(x > 0)) exp(sum(w * log(x)) / n) else NA
  m2 <- sum(w * d^2) / n; m3 <- sum(w * d^3) / n; m4 <- sum(w * d^4) / n
  s$skew <- if (n > 3 && m2 > 0) m3 / m2^1.5 else NA
  s$kurt <- if (n > 3 && m2 > 0) m4 / m2^2 else NA
  q <- function(num, den) if (kind == 2 && !weighted) type2(x, num, den) else type1(x, v, num, den)
  s$max <- max(x); s$min <- min(x); s$range <- s$max - s$min
  s$uq <- q(3, 4); s$median <- q(1, 2); s$lq <- q(1, 4); s$iqr <- s$uq - s$lq
  inside <- function(p) p[1] >= 0 && p[1] <= p[2]
  s$cb <- if (inside(b)) q(b[1], b[2]) else NA
  s$ca <- if (inside(a)) q(a[1], a[2]) else NA
  s
}
order_of <- c("valid", "missing", "sum", "mean", "var", "sd", "vc", "sem", "ucl", "lcl", "gm", "skew", "kurt", "max", "uq", "median", "lq", "iqr", "min", "range", "cb", "ca")
all_on <- list(`report-valid-data` = TRUE, `report-missing-data` = TRUE, `report-sum` = TRUE, `report-mean` = TRUE, `report-variance` = TRUE, `report-sd` = TRUE,
               `report-variance-coeff` = TRUE, `report-sem` = TRUE, `report-u95cl` = TRUE, `report-l95cl` = TRUE, `report-geometric-mean` = TRUE, `report-skewness` = TRUE,
               `report-kurtosis` = TRUE, `report-maximum` = TRUE, `report-uq` = TRUE, `report-median` = TRUE, `report-lq` = TRUE, `report-iqr` = TRUE, `report-minimum` = TRUE,
               `report-range` = TRUE, `report-udc` = TRUE)
pc <- function(level) { l <- if (level > 0 && level < 1) level else 0.95; format(round(100 * l, 1), trim = TRUE) }
cent <- function(p) paste("Centile", format(100 * p[1] / p[2], trim = TRUE))
# a case of the univariate summary: the columns xs (and the weights v), with what is expected of every figure of every column
describe <- function(xs, v = NULL, level = 0.95, kind = 1, a = c(50, 1000), b = c(950, 1000), save = FALSE, .what = NULL) {
  weighted <- !is.null(v)
  rows <- max(sapply(xs, length))
  want <- list()
  for (j in seq_along(xs)) {
    x <- xs[[j]]; vj <- NULL
    if (weighted) {
      # the rows that have no weight, or a weight of 0, are left out, of every column
      vv <- c(v, rep(NA, max(0, rows - length(v))))[seq_len(rows)]
      xx <- c(x, rep(NA, rows - length(x)))
      there <- seq_len(rows) <= length(x)
      keep <- !is.na(vv) & vv != 0
      x <- xx[keep & there]; vj <- vv[keep & there]
    }
    s <- summary_of(x, vj, level, kind, a, b)
    if (weighted) s$sum <- s$weights
    for (k in seq_along(order_of)) want[[sprintf("*fields[%d].*results[%d].result", k, j)]] <- s[[order_of[k]]]
  }
  want[["*fields.rows"]] <- 22
  want[["*titles.rows"]] <- length(xs)
  want[["*fields[9].title"]] <- paste0("Upper ", pc(level), "% CL of mean")
  want[["*fields[10].title"]] <- paste0("Lower ", pc(level), "% CL of mean")
  want[["*fields[3].title"]] <- if (weighted) "Sum of weights" else "Sum"
  want[["*fields[21].title"]] <- cent(b); want[["*fields[22].title"]] <- cent(a)
  # the form gives the weights the length of the data
  inputs <- c(list(data = do.call(frame, xs)), if (weighted) list(weights = frame(c(v, rep(NA, max(0, rows - length(v))))[seq_len(rows)])), list(gamma = level), all_on,
              list(`report-centile-type` = as.character(kind), `report-udca` = a[1] / a[2], `report-udcb` = b[1] / b[2], `output-to-frame` = save))
  case(if (weighted) "RptWeightedUnivariateSummary" else "RptUnivariateSummary", inputs, c(want, .what))
}

# ---- samples: of few and many values, near 0 and far from it, wide and narrow, with values that are the same
samples <- list(
  rnorm(12, 50, 8), rnorm(9, 47, 3), rnorm(200, 0, 1), rnorm(30, 1e8, 2), rexp(40, 0.1), round(rnorm(15, 120, 15)), c(3.1, 4.7), c(2, 9, 4),
  rnorm(60, -5, 1e-6), rnorm(8, 1e-150, 1e-151), rnorm(500, 1e12, 1e5), c(1, 2, 3, 4, 100), round(runif(100, 0, 20)), c(7, 7, 7, 7, 7),
  c(1, 2, 3, 4), 1:20, 1:19, c(-3, -1, 0, 2, 8, 8, 9), rnorm(100, 300, 0.08), c(1e150, 2e150, 3e150, 5e150, 9e150), rnorm(1000, 10, 3))
for (i in seq_along(samples)) for (kind in 1:2) for (level in c(0.95, 0.99, 0.8)) {
  if (level != 0.95 && !(i %in% c(1, 5, 13))) next
  describe(list(samples[[i]]), level = level, kind = kind)
}
# centiles of many kinds, of samples of sizes for which p n is a whole number and for which it is not
for (i in c(1, 3, 5, 12, 13, 15, 16, 17, 19, 21)) for (kind in 1:2) for (p in list(c(10, 900), c(70, 290), c(1, 999), c(200, 800), c(250, 750), c(125, 975), c(500, 500), c(333, 667)))
  describe(list(samples[[i]]), kind = kind, a = c(p[1], 1000), b = c(p[2], 1000))
# several columns, of different lengths, with missing values, and the table for the worksheet
with_missing <- samples[[1]]; with_missing[c(2, 7)] <- NA
describe(list(samples[[1]], samples[[2]], with_missing, samples[[6]]), save = TRUE)
describe(list(with_missing), save = TRUE)
describe(list(samples[[5]], samples[[16]], samples[[17]], samples[[12]], samples[[13]]), kind = 2, save = TRUE)
# ---- with weights: whole numbers, of which the quantiles are worked out in whole numbers
for (i in c(1, 2, 5, 6, 8, 12, 13, 15, 16, 18)) for (scheme in 1:4) {
  x <- samples[[i]]; n <- length(x)
  v <- switch(scheme, rep(1, n), sample(1:5, n, replace = TRUE), sample(c(1, 2, 49, 100), n, replace = TRUE), rep(7, n))
  describe(list(x), v = v)
  if (scheme == 2) for (p in list(c(10, 900), c(70, 290), c(250, 750), c(125, 975))) describe(list(x), v = v, a = c(p[1], 1000), b = c(p[2], 1000), level = 0.9)
}
# the example of the help, and weights that are not there or are 0, with columns of different lengths
describe(list(c(1, 2, 3, 4, 5)), v = c(2, 1, 3, 1, 1))
describe(list(c(1, 2, 3, 4, 5, 6, 7, 8), c(4, 8, 15, 16, 23, 42), c(5, NA, 7, 9, 11, 13, 17, 19)), v = c(2, 0, 3, NA, 1, 1, 4, 2), save = TRUE)
describe(list(c(1, 2, 3, 4, 5, 6, 7, 8), c(4, 8, 15)), v = c(2, 1, 3, 1, 0, 1, NA, 2))
describe(list(c(1, 2, 3, 4, 5, 6)), v = c(2, 1, 3, 1))
describe(list(c(3, 1, 2, 2, 3)), v = c(1, 1, 2, 3, 1), kind = 2)

# ---- the ends: one value, no value, confidence levels and centiles of 0% and 100%
describe(list(c(5)))
describe(list(c(5)), kind = 2)
describe(list(c(NA, 5, NA)))
describe(list(c(-5)))
describe(list(c(NA_real_, NA_real_)))
describe(list(c(5, 5)))
describe(list(c(4, 6)))
describe(list(c(4, 6, NA, 9)), kind = 2)
describe(list(c(0, 0, 0, 0)))
for (level in c(0, 1)) for (i in c(1, 6)) describe(list(samples[[i]]), level = level)
for (i in c(1, 13)) { describe(list(samples[[i]]), a = c(0, 1000), b = c(1000, 1000)); describe(list(samples[[i]]), a = c(0, 1000), b = c(1000, 1000), kind = 2) }
describe(list(c(5)), v = c(3))
describe(list(c(5, 8)), v = c(3, 0))
describe(list(c(5, 8, 9)), v = c(0, 0, 0))

# ---- the quick summary: the lines of its text, which have 6 decimal places
quick <- function(x, level = 0.95) {
  s <- summary_of(x, NULL, level, 1)
  l <- pc(level)
  want <- list(`text.Valid data` = s$valid, `text.Missing` = s$missing, `text.Sum` = s$sum, `text.Mean` = s$mean, `text.Variance` = s$var, `text.Standard deviation` = s$sd,
               `text.Variation coefficient` = s$vc, `text.Standard error of mean` = s$sem, `text.Geometric mean` = s$gm, `text.Skewness` = s$skew, `text.Kurtosis` = s$kurt,
               `text.Maximum` = s$max, `text.95th percentile` = s$cb, `text.Upper quartile` = s$uq, `text.Median` = s$median, `text.Lower quartile` = s$lq,
               `text.Interquartile range` = s$iqr, `text.5th percentile` = s$ca, `text.Minimum` = s$min, `text.Range` = s$range)
  want[[paste0("text.", l, "% Upper CL of mean")]] <- s$ucl
  want[[paste0("text.", l, "% Lower CL of mean")]] <- s$lcl
  case("QuickSummary", list(gamma = level, data = frame(x)), want)
}
for (i in c(1, 2, 3, 4, 5, 6, 7, 12, 13, 16, 18, 19)) quick(samples[[i]])
quick(samples[[1]], 0.99); quick(samples[[1]], 0); quick(samples[[1]], 1); quick(with_missing); quick(c(5)); quick(c(NA_real_, NA_real_))
quick(c(130, 125, 140, 118, 149, 132, 121, 128, 137, 126, 133, 129, 141, 116, 138, 127, 135, 124, 131, 121))

# ---- the time series summary
# least squares slope of y on x, of the pairs that are there
slope <- function(x, y) { k <- !is.na(y); x <- x[k]; y <- y[k]; if (length(x) < 2) NA else sum((x - mean(x)) * (y - mean(y))) / sum((x - mean(x))^2) }
series <- function(time, obs, id, group = NULL, level = 0.95, zero = NULL, .more = list()) {
  l <- if (level > 0 && level < 1) level else 0.95
  g <- if (is.null(group)) rep("all", length(time)) else group
  add <- !is.null(zero) && zero && !any(time == 0)
  inputs <- c(list(times = frame(time), observations = frame(obs), subjectIds = labels(id)), if (!is.null(group)) list(groups = labels(group)),
              if (!is.null(zero)) list(addZeroObservationAtZeroTime = zero), list(ci = level, doExactP = FALSE))
  # a row without an observation is as if it were not there, and the same observation again is taken once; the subjects are in
  # the order of the first places of their labels in the whole column
  all_ids <- unique(id); all_groups <- unique(g)
  there <- !is.na(obs) & !duplicated(data.frame(time, obs, id, g))
  time <- time[there]; obs <- obs[there]; id <- id[there]; g <- g[there]
  want <- list(ciOutput = 100 * l)
  names_g <- all_groups
  aucs <- list()
  for (gi in seq_along(names_g)) {
    k <- g == names_g[gi]
    tt <- time[k]; oo <- obs[k]; ii <- id[k]
    ids <- all_ids[all_ids %in% ii]
    times <- sort(unique(tt)); if (add) times <- sort(unique(c(0, times)))
    m <- matrix(NA_real_, length(times), length(ids))
    for (r in seq_along(tt)) m[match(tt[r], times), match(ii[r], ids)] <- oo[r]
    total <- sum(k)
    if (add) { total <- total + sum(is.na(m[1, ])); m[1, is.na(m[1, ])] <- 0 }
    p <- sprintf("*group[%d].", gi)
    want[[paste0(p, "groupName")]] <- names_g[gi]
    want[[paste0(p, "subjects")]] <- length(ids)
    want[[paste0(p, "totalObservations")]] <- total
    want[[paste0(p, "meanObservationsPerTimePoint")]] <- total / length(times)
    want[[paste0(p, "*time.rows")]] <- length(times)
    for (ti in seq_along(times)) {
      y <- m[ti, ]; y <- y[!is.na(y)]; n <- length(y); q <- sprintf("%s*time[%d].", p, ti)
      want[[paste0(q, "time")]] <- times[ti]; want[[paste0(q, "observations")]] <- n
      want[[paste0(q, "mean")]] <- if (n) mean(y) else NA
      want[[paste0(q, "sd")]] <- if (n > 1) sd(y) else NA
      want[[paste0(q, "se")]] <- if (n > 1) sd(y) / sqrt(n) else NA
      want[[paste0(q, "median")]] <- if (n) type1(y, rep(1, n), 1, 2) else NA
      want[[paste0(q, "iqr")]] <- if (n) type1(y, rep(1, n), 3, 4) - type1(y, rep(1, n), 1, 4) else NA
    }
    auc <- numeric(0); tmax <- numeric(0); slopes <- numeric(0)
    want[[paste0(p, "*subject.rows")]] <- length(ids)
    for (si in seq_along(ids)) {
      y <- m[, si]; have <- !is.na(y); q <- sprintf("%s*subject[%d].", p, si)
      th <- times[have]; yh <- y[have]
      a <- if (length(th) > 1) sum(diff(th) * (head(yh, -1) + tail(yh, -1)) / 2) else 0
      top <- which(y == max(yh))[1]
      sl <- slope(times[1:top], y[1:top])
      want[[paste0(q, "subjectId")]] <- ids[si]
      want[[paste0(q, "baseline")]] <- y[1]
      want[[paste0(q, "min")]] <- min(yh); want[[paste0(q, "max")]] <- max(yh)
      want[[paste0(q, "timeToMax")]] <- times[top]
      want[[paste0(q, "slopeToMax")]] <- sl
      want[[paste0(q, "auc")]] <- a
      auc <- c(auc, a); tmax <- c(tmax, times[top]); slopes <- c(slopes, sl)
    }
    n <- length(auc); se <- sd(auc) / sqrt(n)
    want[[paste0(p, "aucMean")]] <- mean(auc); want[[paste0(p, "aucSd")]] <- if (n > 1) sd(auc) else NA; want[[paste0(p, "aucSe")]] <- if (n > 1) se else NA
    tq <- if (n > 1) qt((1 - l) / 2, n - 1, lower.tail = FALSE) else NA; zq <- qnorm((1 - l) / 2, lower.tail = FALSE)
    want[[paste0(p, "aucTLcl")]] <- mean(auc) - tq * se; want[[paste0(p, "aucTUcl")]] <- mean(auc) + tq * se
    want[[paste0(p, "aucZLcl")]] <- mean(auc) - zq * se; want[[paste0(p, "aucZUcl")]] <- mean(auc) + zq * se
    one <- function(z, a, b) { z <- z[!is.na(z)]; if (length(z)) type1(z, rep(1, length(z)), a, b) else NA }
    want[[paste0(p, "medianAuc")]] <- one(auc, 1, 2); want[[paste0(p, "iqrAuc")]] <- one(auc, 3, 4) - one(auc, 1, 4)
    want[[paste0(p, "medianTimeToMax")]] <- one(tmax, 1, 2); want[[paste0(p, "iqrTimeToMax")]] <- one(tmax, 3, 4) - one(tmax, 1, 4)
    want[[paste0(p, "medianSlopeToMax")]] <- one(slopes, 1, 2); want[[paste0(p, "iqrSlopeToMax")]] <- one(slopes, 3, 4) - one(slopes, 1, 4)
    sv <- slopes[!is.na(slopes)]
    want[[paste0(p, "meanSlopeToMax")]] <- if (length(sv)) mean(sv) else NA
    want[[paste0(p, "meanSlopeToMaxSD")]] <- if (length(sv) > 1) sd(sv) else NA
    aucs[[gi]] <- auc
  }
  want[["*group.rows"]] <- length(names_g)
  # the normal plots: the areas of all the groups against their normal scores, of which the square of the correlation is given
  all_auc <- unlist(aucs)
  can <- function(z) length(z) >= 2 && all(is.finite(z)) && max(z) > min(z)
  r2 <- function(z) cor(z, qnorm(rank(z) / (length(z) + 1)))^2
  not <- character(0)
  if (can(all_auc)) { want[["*aucNormal.rows"]] <- 1; want[["*aucNormal[1].rSquareNormal"]] <- r2(all_auc) }
  else { want[["*aucNormal.rows"]] <- 0; not <- c(not, "Normal plot for AUC not drawn: it needs two areas or more that are not all the same.") }
  if (all(all_auc > 0) && can(log10(all_auc))) { want[["*aucLogNormal.rows"]] <- 1; want[["*aucLogNormal[1].rSquareLogNormal"]] <- r2(log10(all_auc)) }
  else { want[["*aucLogNormal.rows"]] <- 0; not <- c(not, if (all(all_auc > 0)) "Normal plot for log(AUC) not drawn: it needs two areas or more that are not all the same." else "Normal plot for log(AUC) not drawn: an area is zero or below.") }
  want[["*aucNotPlotted.rows"]] <- length(not)
  for (i in seq_along(not)) want[[sprintf("*aucNotPlotted[%d].why", i)]] <- not[i]
  if (length(names_g) == 2) {
    a1 <- aucs[[1]]; a2 <- aucs[[2]]; v1 <- var(a1) / length(a1); v2 <- var(a2) / length(a2)
    df <- (v1 + v2)^2 / (v1^2 / (length(a1) - 1) + v2^2 / (length(a2) - 1)); se <- sqrt(v1 + v2); d <- mean(a1) - mean(a2); t <- d / se
    tq <- qt((1 - l) / 2, df, lower.tail = FALSE)
    want <- c(want, list(`*groupComparison[1].t` = t, `*groupComparison[1].se` = se, `*groupComparison[1].df` = df, `*groupComparison[1].p2` = 2 * pt(-abs(t), df),
                         `*groupComparison[1].aucDifference` = d, `*groupComparison[1].aucDifferenceLcl` = d - tq * se, `*groupComparison[1].aucDifferenceUcl` = d + tq * se,
                         `*groupComparison[1].group1Title` = names_g[1], `*groupComparison[1].group2Title` = names_g[2]))
  }
  want[names(.more)] <- .more
  case("RptTimeSeriesSummary", inputs, want)
}
# the example of the help (Bland 2000): zidovudine in the blood of patients with and without malabsorption
zt <- c(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 30, 30, 30,
  30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 60, 60,
  60, 60, 60, 60, 60, 60, 60, 60, 60, 60, 60, 60, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 120,
  120, 120, 120, 120, 120, 120, 120, 120, 120, 120, 120, 120, 120, 150, 150, 150, 150, 150, 150, 150, 150, 150,
  150, 150, 150, 150, 150, 180, 180, 180, 180, 180, 180, 180, 180, 180, 180, 180, 180, 180, 180, 240, 240, 240,
  240, 240, 240, 240, 240, 240, 240, 240, 240, 240, 240, 300, 300, 300, 300, 300, 300, 300, 300, 300, 300, 300,
  300, 300, 300, 360, 360, 360, 360, 360, 360, 360, 360, 360, 360, 360, 360, 360, 360)
zo <- c(0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 0.08, 13.15, 0.08, 0.08, 0.08,
  6.69, 4.28, 0.13, 0.64, 2.39, 3.72, 6.72, 9.98, 1.12, 13.37, 5.7, 0.14, 3.29, 1.33, 8.27, 4.92, 9.29, 1.19,
  3.53, 16.02, 5.48, 7.28, 7.27, 17.61, 3.22, 2.1, 3.47, 1.71, 5.02, 1.22, 6.03, 1.65, 6.28, 8.17, 4.84, 3.46,
  3.77, 3.9, 2.69, 6.37, 1.42, 3.3, 3.98, 1.17, 3.65, 2.37, 2.61, 5.21, 2.3, 2.42, 2.97, 5.53, 1.91, 4.89,
  1.61, 1.81, 1.9, 0.88, 2.32, 2.07, 2.29, 4.84, 1.95, 1.69, 1.78, 7.17, 1.72, 2.11, 1.41, 1.16, 1.24, 0.34,
  1.25, 2.54, 2.23, 2.12, 1.46, 0.7, 1.27, 5.16, 1.22, 1.4, 1.09, 0.69, 1.01, 0.24, 1.02, 1.34, 1.97, 1.5,
  1.49, 0.76, 0.99, 3.84, 1.15, 1.42, 0.49, 0.63, 0.78, 0.37, 0.7, 0.93, 0.73, 1.18, 1.34, 0.47, 0.83, 2.51,
  0.71, 0.72, 0.2, 0.36, 0.52, 0.09, 0.43, 0.64, 0.41, 0.72, 0.77, 0.18, 0.57, 1.31, 0.43, 0.39, 0.17, 0.22,
  0.41, 0.08, 0.21, 0.3, 0.15, 0.41, 0.5, 0.08, 0.38, 0.7, 0.32, 0.28, 0.11, 0.12, 0.42, 0.08, 0.18, 0.2, 0.08,
  0.29, 0.28, 0.08, 0.25, 0.37)
zp <- c(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5,
  6, 7, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
  11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14,
  1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5,
  6, 7, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
  11, 12, 13, 14)
zg <- c("Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Malabsorbtion",
  "Malabsorbtion", "Malabsorbtion", "Malabsorbtion", "Normal", "Normal", "Normal", "Normal", "Normal")
for (level in c(0.95, 0.9)) series(zt, zo, paste0("p", zp), zg, level)
series(zt[zg == zg[1]], zo[zg == zg[1]], paste0("p", zp[zg == zg[1]]))
# curves that are made: subjects with their own times, observations that are missing, a peak at the start, values below 0
make <- function(subjects, times, peak = 3, noise = 1, drop = 0, shift = 0, groups = 1) {
  time <- numeric(0); obs <- numeric(0); id <- character(0); grp <- character(0)
  for (s in seq_len(subjects)) {
    height <- runif(1, 5, 12) * (if (groups == 2 && s %% 2 == 0) 1.6 else 1)
    y <- height * (times / peak) * exp(1 - times / peak) + rnorm(length(times), 0, noise) + shift
    keep <- rep(TRUE, length(times)); if (drop > 0) keep[sample(seq_along(times), drop)] <- FALSE
    time <- c(time, times[keep]); obs <- c(obs, y[keep]); id <- c(id, rep(paste0("s", s), sum(keep)))
    grp <- c(grp, rep(if (groups == 2 && s %% 2 == 0) "treated" else "control", sum(keep)))
  }
  list(time = time, obs = obs, id = id, group = if (groups == 2) grp else NULL)
}
for (spec in list(list(8, c(0, 1, 2, 4, 6, 8, 12, 24)), list(5, c(0.5, 1, 1.5, 2, 3, 5)), list(20, 0:10), list(3, c(0, 10, 20)), list(12, c(1, 2, 4, 8, 16, 32)))) for (groups in 1:2) for (drop in c(0, 2)) {
  if (drop > 0 && length(spec[[2]]) < 5) next
  d <- make(spec[[1]], spec[[2]], drop = drop, groups = groups)
  for (level in c(0.95, 0.99)) series(d$time, d$obs, d$id, d$group, level, zero = if (any(d$time == 0)) NULL else FALSE)
  if (!any(d$time == 0)) series(d$time, d$obs, d$id, d$group, 0.95, zero = TRUE)
}
# the ends: a peak at the first time, values below 0, one subject, one time, one observation of a subject, levels of 0% and 100%
d <- make(6, c(0, 1, 2, 3), peak = 0.2); series(d$time, d$obs, d$id)
d <- make(6, c(0, 1, 2, 3, 5), shift = -20); series(d$time, d$obs, d$id)
d <- make(1, c(0, 1, 2, 3, 5)); series(d$time, d$obs, d$id)
d <- make(5, c(2)); series(d$time, d$obs, d$id, zero = FALSE)
d <- make(5, c(2)); series(d$time, d$obs, d$id, zero = TRUE)
d <- make(6, c(0, 1, 2, 3, 5), groups = 2); for (level in c(0, 1)) series(d$time, d$obs, d$id, d$group, level)
d <- make(7, c(0, 1, 2, 3, 5), groups = 2, noise = 0.01); d$obs[d$group == "treated"] <- d$obs[d$group == "treated"] * 40; series(d$time, d$obs, d$id, d$group)
series(c(0, 1, 2, 0, 1, 2, 1), c(1, 5, 2, 2, 6, 3, 4), c("a", "a", "a", "b", "b", "b", "c"))
series(c(0, 1, 2, 0, 1, 2, 0, 1, 2), c(1, 5, 2, 2, NA, 3, 1, 4, NA), c("a", "a", "a", "b", "b", "b", "c", "c", "c"))
series(c(0, 1, 2, 0, 1, 2, 0, 1, 2), c(4, 4, 4, 2, 2, 2, 3, 3, 3), c("a", "a", "a", "b", "b", "b", "c", "c", "c"))
# a subject of whom every observation is missing, a time at which every observation is missing, the same observation twice
series(c(0, 1, 2, 0, 1, 2, 0, 1, 2), c(1, 5, 2, NA, NA, NA, 1, 4, 3), c("a", "a", "a", "b", "b", "b", "c", "c", "c"))
series(c(0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3), c(1, 5, NA, 2, 2, 6, NA, 1, 1, 4, NA, 3), c("a", "a", "a", "a", "b", "b", "b", "b", "c", "c", "c", "c"))
series(c(0, 1, 2, 2, 0, 1, 2, 0, 1, 2), c(1, 5, 2, 2, 2, 6, 3, 1, 4, 3), c("a", "a", "a", "a", "b", "b", "b", "c", "c", "c"))
case("RptTimeSeriesSummary", list(times = frame(c(0, 1, 2, 2, 0, 1, 2)), observations = frame(c(1, 5, 2, 2.5, 2, 6, 3)), subjectIds = labels(c("a", "a", "a", "a", "b", "b", "b")), ci = 0.95, doExactP = FALSE),
     list(refused = "Exception: Your data contains multiple, non-identical observations for the same subject and time point; time series summary cannot interpret this. Please remove the duplicate(s)."))
# two groups far apart, of which the first has the smaller areas and of which it has the greater: the P value is small
for (first in c("low", "high")) {
  time <- rep(c(0, 1, 2, 4), 16); id <- rep(paste0("s", 1:16), each = 4); low <- rep(c(TRUE, FALSE), each = 32)
  obs <- (if (first == "low") ifelse(low, 1, 50) else ifelse(low, 50, 1)) * rep(c(1, 3, 2, 1), 16) * (1 + rep(seq(-0.01, 0.01, length.out = 16), each = 4))
  series(time, obs, id, ifelse(low, "one", "two"))
}

writeLines(cases, args[1]); writeLines(expected, args[2])
cat(length(cases), "cases,", length(expected), "figures\n")

# ---- the bootstrap of the time series summary.  Whole subjects are drawn with replacement within each group, as many as the
# group has; for each draw t* = (the mean area - the mean area of the draw) / the standard error of the draw is kept, and the
# limits are the mean area + the centiles of t* times the standard error of the group.  For two groups t* is of the
# difference of the means, and the P value is the part of the draws of which t* is as far from 0 as the t of the data, or
# further, with 1 added to both counts.
area <- function(time, obs) { o <- order(time); t <- time[o]; y <- obs[o]; sum(diff(t) * (head(y, -1) + tail(y, -1)) / 2) }
made <- local({ n <- 12; times <- c(0, 1, 2, 4, 8, 12); h <- runif(n, 5, 12)
                list(name = "made", time = rep(times, n), obs = as.vector(sapply(h, function(a) a * (times / 3) * exp(1 - times / 3) + rnorm(length(times), 0, 0.5))),
                     id = rep(paste0("s", 1:n), each = length(times)), group = NULL) })
sets <- list(list(name = "zidovudine", time = zt, obs = zo, id = paste0("p", zp), group = zg), made)
B <- 100000
cases <- character(0); expected <- character(0)
for (s in sets) for (level in c(0.95, 0.9)) {
  name <- paste0(s$name, "_", level); edge <- (1 - level) / 2
  cases <- c(cases, paste("RptTimeSeriesSummary", name, paste0("times=", column(s$time), ";observations=", column(s$obs), ";subjectIds=", labels(s$id),
                          if (!is.null(s$group)) paste0(";groups=", labels(s$group)), ";ci=", level, ";doExactP=true;iterations=", format(B, scientific = FALSE), ";seed=12345"), sep = "\t"))
  groups <- if (is.null(s$group)) list(all = rep(TRUE, length(s$time))) else lapply(setNames(unique(s$group), unique(s$group)), function(g) s$group == g)
  aucs <- lapply(groups, function(k) sapply(unique(s$id[k]), function(i) area(s$time[k & s$id == i], s$obs[k & s$id == i])))
  draws <- function(a) { n <- length(a); m <- matrix(a[sample.int(n, n * B, replace = TRUE)], B, n); mu <- rowMeans(m); list(mean = mu, var = rowSums((m - mu)^2) / (n - 1) / n) }
  lower <- list(); upper <- list(); p <- numeric(0); dl <- numeric(0); du <- numeric(0)
  for (again in 1:10) {
    d <- lapply(aucs, draws)
    for (g in seq_along(aucs)) {
      a <- aucs[[g]]; se <- sd(a) / sqrt(length(a)); t <- (mean(a) - d[[g]]$mean) / sqrt(d[[g]]$var); t <- t[!is.nan(t)]
      lower[[g]] <- c(if (again > 1) lower[[g]], mean(a) + quantile(t, edge, type = 2, names = FALSE) * se)
      upper[[g]] <- c(if (again > 1) upper[[g]], mean(a) + quantile(t, 1 - edge, type = 2, names = FALSE) * se)
    }
    if (length(aucs) == 2) {
      a1 <- aucs[[1]]; a2 <- aucs[[2]]; diff <- mean(a1) - mean(a2); se <- sqrt(var(a1) / length(a1) + var(a2) / length(a2)); t0 <- diff / se
      t <- (diff - (d[[1]]$mean - d[[2]]$mean)) / sqrt(d[[1]]$var + d[[2]]$var)
      p <- c(p, (sum(abs(t) >= abs(t0), na.rm = TRUE) + 1) / (B + 1))
      t <- t[!is.nan(t)]
      dl <- c(dl, diff + quantile(t, edge, type = 2, names = FALSE) * se); du <- c(du, diff + quantile(t, 1 - edge, type = 2, names = FALSE) * se)
    }
  }
  want <- function(key, v) expected <<- c(expected, paste(paste0("RptTimeSeriesSummary|", name, "|", key), fig(mean(v)), fig(sd(v)), sep = "\t"))
  for (g in seq_along(aucs)) {
    want(sprintf("*group[%d].*bootstrap[1].aucTLclBoot", g), lower[[g]]); want(sprintf("*group[%d].*bootstrap[1].aucTUclBoot", g), upper[[g]])
    want(sprintf("*group[%d].*bootstrap[1].completedIterations", g), rep(B, 10))
  }
  if (length(aucs) == 2) {
    want("*groupComparison[1].*bootstrap[1].pBootstrap", p); want("*groupComparison[1].*bootstrap[1].tBootstrapLcl", dl); want("*groupComparison[1].*bootstrap[1].tBootstrapUcl", du)
    want("*groupComparison[1].*bootstrap[1].iterations", rep(B, 10))
  }
}
writeLines(cases, "bootstrap-cases.txt"); writeLines(expected, "bootstrap-expected.txt")
cat(length(cases), "cases of the bootstrap,", length(expected), "figures\n")
