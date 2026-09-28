# Benchmarks of the Proportions menu, worked out from the definitions.
# usage: Rscript --vanilla figures-prop.R cases-prop.txt r-prop.txt
args <- commandArgs(trailingOnly = TRUE)
lines <- readLines(args[1])
out <- character(0)
put <- function(key, value) out <<- c(out, paste0(key, "\t", if (is.character(value)) paste0("\"", value, "\"") else if (is.na(value)) "NA" else if (is.infinite(value)) (if (value > 0) "Inf" else "-Inf") else format(value, digits = 15, scientific = TRUE)))
root <- function(f, lo, hi) uniroot(f, c(lo, hi), tol = 1e-15, maxiter = 10000)$root

# the one side confidence level that the report names when all or none respond
oneSided <- function(level) paste0(" [", format(round(100 * (level + (1 - level) / 2), 1), nsmall = 0), "% one-sided CI]")

# The score limits of a proportion (Wilson): the proportions that are z standard errors of their own from the proportion observed
score <- function(r, n, z) {
  p <- r / n
  # The limits are looked for on the scale of the logarithm of the odds, so that a limit near 0 has all its figures.  With none
  # responding the lower limit is 0, and with all responding the upper limit is 1: the other limit is then the root that is not
  # the proportion observed itself
  f <- function(u) { x <- plogis(u); (p - x)^2 - z^2 * x * (1 - x) / n }
  at <- if (r == 0) -700 else if (r == n) 700 else qlogis(p)
  c(if (r == 0) 0 else plogis(root(f, -700, min(at, 36))), if (r == n) 1 else plogis(root(f, max(at, -700), 700)))
}

# The tails of a binomial count: no more than r, no less than r, r itself, and the values that are no more probable than r; the
# values far from what is expected have a probability of nothing and are left out of the sum
tails <- function(r, n, pi) {
  if (pi <= 0) return(list(lower = 1, upper = as.numeric(r == 0), point = as.numeric(r == 0), two = as.numeric(r == 0)))
  if (pi >= 1) return(list(lower = as.numeric(r == n), upper = 1, point = as.numeric(r == n), two = as.numeric(r == n)))
  point <- dbinom(r, n, pi)
  sd <- sqrt(n * pi * (1 - pi))
  from <- max(0, floor(min(r, n * pi - 60 * sd) - 60 * sd - 2)); to <- min(n, ceiling(max(r, n * pi + 60 * sd) + 60 * sd + 2))
  if (to - from > 5e7) { from <- max(0, floor(n * pi - 45 * sd - 2)); to <- min(n, ceiling(n * pi + 45 * sd + 2)) }
  d <- dbinom(from:to, n, pi)
  list(lower = pbinom(r, n, pi), upper = pbinom(r - 1, n, pi, lower.tail = FALSE), point = point, two = min(1, sum(d[d <= point * (1 + 1e-7)])))
}

single <- function(key, n, r, pi, level) {
  put(paste0(key, "|ok"), 1)
  put(paste0(key, "|prop"), r / n)
  put(paste0(key, "|ci_exact"), 100 * level)
  put(paste0(key, "|lower_exact"), if (r == 0) 0 else qbeta((1 - level) / 2, r, n - r + 1))
  put(paste0(key, "|upper_exact"), if (r == n) 1 else qbeta(1 - (1 - level) / 2, r + 1, n - r))
  put(paste0(key, "|warn_exact"), if (r == 0 || r == n) oneSided(level) else "")
  t <- tails(r, n, pi)
  one <- min(t$lower, t$upper)
  put(paste0(key, "|p_1_exact"), one)
  put(paste0(key, "|p_2_exact"), t$two)
  z <- qnorm(1 - (1 - level) / 2)
  w <- score(r, n, z)
  put(paste0(key, "|ci_approx"), 100 * level)
  put(paste0(key, "|lower_approx"), w[1]); put(paste0(key, "|upper_approx"), w[2])
  mid <- one - t$point / 2
  put(paste0(key, "|p_1_approx"), mid)
  put(paste0(key, "|p_2_approx"), min(1, 2 * mid))
}

paired <- function(key, n, r, s, t, level) {
  put(paste0(key, "|ok"), 1)
  put(paste0(key, "|prop_1"), (r + s) / n); put(paste0(key, "|prop_2"), (r + t) / n); put(paste0(key, "|prop_diff"), (s - t) / n)
  m <- s + t; less <- min(s, t)
  one <- pbinom(less, m, 0.5); point <- dbinom(less, m, 0.5)
  put(paste0(key, "|exact.1|cum_1"), one); put(paste0(key, "|exact.1|cum_2"), min(1, 2 * one))
  put(paste0(key, "|exact.1|cum_1_mid"), one - point / 2); put(paste0(key, "|exact.1|cum_2_mid"), min(1, 2 * (one - point / 2)))
  # The limits of the difference (Newcombe): from the score limits of the two proportions and the correlation of the two
  # responses, which has a correction for continuity if it is above 0
  z <- qnorm(1 - (1 - level) / 2)
  a <- r; b <- s; c <- t; d <- n - r - s - t
  one1 <- score(a + b, n, z); one2 <- score(a + c, n, z)
  p1 <- (a + b) / n; p2 <- (a + c) / n
  margins <- (a + b) * (c + d) * (a + c) * (b + d)
  phi <- if (margins == 0) 0 else {
    # the correlation of the two responses over the pairs
    w <- c(a, b, c, d); x <- c(1, 1, 0, 0); y <- c(1, 0, 1, 0)
    mx <- sum(w * x) / n; my <- sum(w * y) / n
    covariance <- sum(w * (x - mx) * (y - my)); spread <- sqrt(sum(w * (x - mx)^2) * sum(w * (y - my)^2))
    if (covariance > 0) max(covariance * n - n / 2, 0) / n / spread else covariance / spread
  }
  dl1 <- p1 - one1[1]; du1 <- one1[2] - p1; dl2 <- p2 - one2[1]; du2 <- one2[2] - p2
  put(paste0(key, "|lower"), (p1 - p2) - sqrt(max(0, dl1^2 - 2 * phi * dl1 * du2 + du2^2)))
  put(paste0(key, "|upper"), (p1 - p2) + sqrt(max(0, du1^2 - 2 * phi * du1 * dl2 + dl2^2)))
  put(paste0(key, "|pc"), 100 * level)
}

# The limits of the difference of two proportions (Miettinen and Nurminen): the differences at which the score statistic has the
# value of chi-square for the confidence level, with the proportions that are most likely for the difference from the formula for
# the root of a cubic
miettinen <- function(x1, n1, x0, n0, level) {
  N <- n1 + n0
  dhat <- x1 / n1 - x0 / n0
  part <- function(x, p) if (x == 0) 0 else if (p <= 0) -Inf else x * log(p)
  loglik <- function(p0, delta) part(x1, p0 + delta) + part(n1 - x1, 1 - p0 - delta) + part(x0, p0) + part(n0 - x0, 1 - p0)
  score <- function(delta) {
    # The proportions that are most likely with the difference delta are at an end of the range that the second of them can have,
    # or at a root of the cubic that the derivative of the likelihood comes to: whichever of these has the greatest likelihood
    lo <- max(0, -delta); hi <- min(1, 1 - delta)
    theta <- n0 / n1
    A <- 1 + theta
    B <- -(1 + theta + x1 / n1 + theta * x0 / n0 + delta * (theta + 2))
    C <- delta^2 + delta * (2 * x1 / n1 + theta + 1) + x1 / n1 + theta * x0 / n0
    D <- -(x1 / n1) * delta * (1 + delta)
    roots <- polyroot(c(D, C, B, A))
    first <- Re(roots)[abs(Im(roots)) < 1e-6 * (1 + abs(Re(roots)))]
    candidates <- c(lo, hi, first - delta)
    candidates <- candidates[candidates >= lo & candidates <= hi]
    likelihoods <- sapply(candidates, loglik, delta = delta)
    p0 <- candidates[which.max(likelihoods)]
    # a root within the range is made exact by steps of Newton on the derivative
    if (p0 > lo && p0 < hi) {
      slope <- function(p) { q <- p + delta; (if (x1 > 0) x1 / q else 0) - (if (n1 > x1) (n1 - x1) / (1 - q) else 0) + (if (x0 > 0) x0 / p else 0) - (if (n0 > x0) (n0 - x0) / (1 - p) else 0) }
      curve <- function(p) { q <- p + delta; -(if (x1 > 0) x1 / q^2 else 0) - (if (n1 > x1) (n1 - x1) / (1 - q)^2 else 0) - (if (x0 > 0) x0 / p^2 else 0) - (if (n0 > x0) (n0 - x0) / (1 - p)^2 else 0) }
      for (i in 1:4) { step <- p0 - slope(p0) / curve(p0); if (is.finite(step) && step > lo && step < hi) p0 <- step }
    }
    p1 <- p0 + delta
    v <- (p1 * (1 - p1) / n1 + p0 * (1 - p0) / n0) * N / (N - 1)
    if (v <= 0) return(if (dhat == delta) 0 else Inf)
    (dhat - delta)^2 / v
  }
  f <- function(delta) min(score(delta), 1e300) - qchisq(level, 1)
  c(if (dhat <= -1) -1 else root(f, -1, dhat), if (dhat >= 1) 1 else root(f, dhat, 1))
}

unpaired <- function(key, n1, r1, n2, r2, level) {
  # numbers that are not whole numbers are rounded, a half to the even number
  n1 <- round(n1); r1 <- round(r1); n2 <- round(n2); r2 <- round(r2)
  put(paste0(key, "|ok"), 1)
  put(paste0(key, "|n_1"), n1); put(paste0(key, "|r_1"), r1); put(paste0(key, "|n_2"), n2); put(paste0(key, "|r_2"), r2)
  put(paste0(key, "|prop_1"), r1 / n1); put(paste0(key, "|prop_2"), r2 / n2); put(paste0(key, "|prop_diff"), r1 / n1 - r2 / n2)
  {
    l <- try(miettinen(r1, n1, r2, n2, level), silent = TRUE)
    if (!inherits(l, "try-error")) { put(paste0(key, "|from"), l[1]); put(paste0(key, "|to"), l[2]) }
    # the mid-P value of Fisher's exact test: twice the less of the two tails, each less half the probability of the table
    a <- r1; b <- n1 - r1; c <- r2; d <- n2 - r2
    if ((a + b) * (c + d) * (a + c) * (b + d) > 0) {
      lower <- phyper(a, a + c, b + d, a + b); upper <- phyper(a - 1, a + c, b + d, a + b, lower.tail = FALSE); point <- dhyper(a, a + c, b + d, a + b)
      put(paste0(key, "|exact2.1|mp"), min(1, 2 * (min(lower, upper) - point / 2)))
    }
  }
  p <- (r1 + r2) / (n1 + n2)
  se <- sqrt(p * (1 - p) * (1 / n1 + 1 / n2))
  if (se > 0) { put(paste0(key, "|se"), se); put(paste0(key, "|z"), (r1 / n1 - r2 / n2) / se) }
  put(paste0(key, "|ci"), 100 * level)
}

done <- 0
for (line in lines) {
  f <- strsplit(line, "\t")[[1]]
  v <- as.numeric(f[-(1:2)])
  key <- paste0(f[1], "|", f[2])
  result <- try({
    if (f[1] == "single") single(key, v[1], v[2], v[3], v[4])
    if (f[1] == "paired") paired(key, v[1], v[2], v[3], v[4], v[5])
    if (f[1] == "unpaired") unpaired(key, v[1], v[2], v[3], v[4], v[5])
  }, silent = TRUE)
  if (inherits(result, "try-error")) cat("FAILED", key, ":", as.character(result))
  done <- done + 1
  if (done %% 200 == 0) cat(done, "cases\n")
}
writeLines(out, args[2])
cat(length(out), "figures\n")
