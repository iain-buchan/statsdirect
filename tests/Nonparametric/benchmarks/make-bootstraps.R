# The benchmarks of the bootstraps of tests/Nonparametric: the Gini coefficient and the diversity indices.  A bootstrap is made of
# random numbers, so a bootstrap made elsewhere agrees with the program's only as far as the chance of the draws allows: each
# figure is given as the mean and the standard deviation of 10 bootstraps made here with random numbers of R's own, each of the
# draws asked for, worked out from the definitions as the program works them out.  A case: the report, a name, and the inputs,
# with the seed that the program is to use; what is expected: "report|name|output<TAB>mean<TAB>standard deviation".
# R 4.6.1, no packages.  usage: Rscript --vanilla make-bootstraps.R bootstrap-cases.txt bootstrap-expected.txt
args <- commandArgs(trailingOnly = TRUE)
cases <- character(0); expected <- character(0); count <- 0
fig <- function(v) format(v, digits = 17, scientific = abs(v) < 1e-4 && v != 0 || abs(v) >= 1e15, trim = TRUE)
column <- function(x) paste(sapply(x, function(v) if (is.na(v)) "*" else fig(v)), collapse = ",")
# a pick among B sorted values at the probability q, as the program picks it: (B - 1) q rounded to the nearest whole number
# (half to even), plus 1
pick <- function(sorted, q) sorted[round((length(sorted) - 1) * q) + 1]
level_of <- function(gamma) if (gamma <= 0 || gamma >= 1) 0.95 else gamma
case <- function(.report, .inputs, .figures) {   # .figures: a list of 10 lists of figures, one for each bootstrap
  count <<- count + 1
  name <- sprintf("b%04d", count)
  cases <<- c(cases, paste(.report, name, paste0(names(.inputs), "=", sapply(.inputs, as.character), collapse = ";"), sep = "\t"))
  for (k in names(.figures[[1]])) {
    v <- sapply(.figures, function(f) f[[k]])
    expected <<- c(expected, paste0(.report, "|", name, "|", k, "\t", fig(mean(v)), "\t", fig(sd(v))))
  }
}

# ---- the Gini coefficient: of the n positive values in order (a weight repeats a value), the sum of (2 j - n - 1) x_j over n times
# the sum; a bootstrap of the values with replacement; the bias G less the mean of the re-sampled coefficients, their standard
# deviation, the percentile limits, and the BCa limits with z0 from the share of re-sampled coefficients below G and the
# acceleration from the jackknife; each limit also times n / (n - 1)
gini_of <- function(x) { x <- sort(x); n <- length(x); sum((2 * seq_len(n) - n - 1) * x) / (n * sum(x)) }
gini_boot <- function(r, gamma, B) {
  n <- length(r); g <- gini_of(r); cit <- qnorm(1 - (1 - gamma) / 2)
  gb <- sort(replicate(B, gini_of(sample(r, n, replace = TRUE))))
  q <- (1 - gamma) / 2
  jack <- sapply(seq_len(n), function(i) gini_of(r[-i])); jm <- mean(jack)
  accel <- if (sum((jm - jack)^2) > 0) sum((jm - jack)^3) / (6 * sum((jm - jack)^2)^1.5) else 0
  z0 <- qnorm(sum(gb < g) / B)
  p1 <- pnorm(z0 + (z0 - cit) / (1 - accel * (z0 - cit))); p2 <- pnorm(z0 + (z0 + cit) / (1 - accel * (z0 + cit)))
  u <- n / (n - 1)
  list(`*data[1].bias` = g - mean(gb), `*data[1].se` = sd(gb), `*data[1].from` = pick(gb, q), `*data[1].to` = pick(gb, 1 - q),
       `*data[1].BCafrom` = pick(gb, p1), `*data[1].BCato` = pick(gb, p2),
       `*data[1].from-unbiased` = pick(gb, q) * u, `*data[1].to-unbiased` = pick(gb, 1 - q) * u,
       `*data[1].BCafrom-unbiased` = pick(gb, p1) * u, `*data[1].BCato-unbiased` = pick(gb, p2) * u)
}
gini_case <- function(x, w = NULL, gamma = 0.95, B = 2000, seed) {
  keep <- !is.na(x) & (if (is.null(w)) TRUE else !is.na(w)); xx <- x[keep]; ww <- if (is.null(w)) rep(1, sum(keep)) else floor(w[keep])
  pos <- xx > 0; r <- rep(xx[pos], ww[pos])
  figures <- lapply(1:10, function(rep) { set.seed(seed * 100 + rep); gini_boot(r, level_of(gamma), B) })
  inputs <- list(gamma = gamma, data = column(x)); if (!is.null(w)) inputs$weights <- column(w); inputs$boots <- B; inputs$seed <- seed
  case("RptGini", inputs, figures)
}
set.seed(20260930)
incomes <- c(12, 18, 25, 31, 40, 47, 55, 68, 90, 150)
gini_case(incomes, seed = 11)
gini_case(incomes, c(3, 1, 2, 2, 1, 4, 1, 1, 2, 1), 0.9, seed = 12)
gini_case(round(rexp(50, 0.01)) + 1, gamma = 0.99, B = 4000, seed = 13)
gini_case(c(59, 43, 39, 24, 22), seed = 2001)                                # the example of the help

# ---- the diversity indices: a bootstrap draws the N individuals with the chances of their classes; the bias and the standard
# deviation of each index over the re-samples; normal limits from that standard deviation with Student's t on B - 1 degrees of
# freedom; and the centred bootstrap-t limits: each re-sample's index less the observed, over the large sample standard error of
# the re-sample, is a deviate, and the half width is half the difference of the deviates at the two percentiles times the
# large sample standard error of the observed index
diversity_boot <- function(n, gamma, B) {
  N <- sum(n); p <- n / N
  simpson <- 1 - sum(n * (n - 1)) / (N * (N - 1)); shannon <- (N * log(N) - sum(n * log(n))) / N
  simse <- sqrt(max(0, (sum(p^3) - sum(p^2)^2) / (0.25 * N))); shanse <- sqrt(max(0, (sum(n * log(n)^2) - sum(n * log(n))^2 / N) / N^2))
  draws <- replicate(B, {
    b <- as.vector(rmultinom(1, N, p)); bp <- b / N; bb <- b[b > 0]
    s <- 1 - sum(bb * (bb - 1)) / (N * (N - 1)); h <- (N * log(N) - sum(bb * log(bb))) / N
    xs <- (sum(bp^3) - sum(bp^2)^2) / (0.25 * N); xh <- (sum(bb * log(bb)^2) - sum(bb * log(bb))^2 / N) / N^2
    c(s, h, (s - simpson) / sqrt(xs), (h - shannon) / sqrt(xh))
  })
  citt <- qt(1 - (1 - gamma) / 2, B - 1); q <- (1 - gamma) / 2
  sb <- draws[1, ]; hb <- draws[2, ]; sz <- sort(draws[3, ]); hz <- sort(draws[4, ])
  half_s <- (pick(sz, 1 - q) - pick(sz, q)) / 2 * simse; half_h <- (pick(hz, 1 - q) - pick(hz, q)) / 2 * shanse
  list(`*var[1].bias-simpson` = simpson - mean(sb), `*var[1].se-simpson-bootstrap` = sd(sb),
       `*var[1].from-simpson-bootstrap` = simpson - citt * sd(sb), `*var[1].to-simpson-bootstrap` = simpson + citt * sd(sb),
       `*var[1].from-simpson-bootstrap-t` = simpson - half_s, `*var[1].to-simpson-bootstrap-t` = simpson + half_s,
       `*var[1].bias-shannon` = shannon - mean(hb), `*var[1].se-shannon-bootstrap` = sd(hb),
       `*var[1].from-shannon-bootstrap` = shannon - citt * sd(hb), `*var[1].to-shannon-bootstrap` = shannon + citt * sd(hb),
       `*var[1].from-shannon-bootstrap-t` = shannon - half_h, `*var[1].to-shannon-bootstrap-t` = shannon + half_h)
}
diversity_case <- function(counts, gamma = 0.95, B = 2000, seed) {
  n <- counts[!is.na(counts) & counts > 0 & counts == floor(counts)]
  figures <- lapply(1:10, function(rep) { set.seed(seed * 100 + rep); diversity_boot(n, level_of(gamma), B) })
  case("RptDiversity", list(gamma = gamma, data = column(counts), boots = B, seed = seed), figures)
}
diversity_case(c(30, 12, 8, 5, 3, 2, 1, 1, 1), seed = 5)
diversity_case(c(30, 13, 9, 8, 7, 7, 7, 6, 6, 5, 2, 2, 2, rep(1, 13)), seed = 1)   # the example of the help
diversity_case(rpois(40, 6) + 1, 0.99, 3000, seed = 7)

writeLines(cases, args[1]); writeLines(expected, args[2])
cat(length(cases), "cases,", length(expected), "figures\n")
