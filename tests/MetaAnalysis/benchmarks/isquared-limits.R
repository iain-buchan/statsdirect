# The exact limits of I-squared that Limits.cs holds the program to: for each set (Q, the number of studies and the confidence
# level) the non-centrality parameter at which the non-central chi-square distribution function at Q is each tail probability,
# found by uniroot to a tolerance of 1e-15, and put on the scale of I-squared, 100 lambda / (df + lambda).  The search is made
# twice, of R's pchisq and of the same distribution written out as a Poisson mixture of central chi-squares; the last two figures
# of each line are the relative differences of the two answers.
options(digits = 17)
lim <- function(q, df, level, cdf) {
  one <- function(prob) {
    if (cdf(q, df, 0) <= prob) return(0)
    f <- function(l) cdf(q, df, l) - prob
    hi <- max(1, q) + 1000
    while (f(hi) > 0) hi <- hi * 2
    uniroot(f, c(0, hi), tol = 1e-15, maxiter = 10000)$root
  }
  l <- c(one(1 - (1 - level) / 2), one((1 - level) / 2))
  100 * l / (df + l)
}
r_cdf <- function(q, df, l) pchisq(q, df, ncp = l)
mix_cdf <- function(q, df, l) { j <- 0:2000; sum(dpois(j, l / 2) * pchisq(q, df + 2 * j)) }
for (case in list(c(7.4077290540652, 3, .95), c(7.38, 3, .95), c(12.5, 6, .95), c(3.9, 2, .95), c(40, 10, .99), c(5.2, 2, .9))) {
  q <- case[1]; k <- case[2]; level <- case[3]
  a <- lim(q, k - 1, level, r_cdf); b <- lim(q, k - 1, level, mix_cdf)
  cat(sprintf("q=%.13f k=%d level=%.2f  pchisq: %.15g %.15g  mixture: %.15g %.15g  rel diff %.2e %.2e\n", q, k, level, a[1], a[2], b[1], b[2],
              abs(a[1] - b[1]) / a[1], abs(a[2] - b[2]) / a[2]))
}
