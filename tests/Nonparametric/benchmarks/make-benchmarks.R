# The benchmarks of tests/Nonparametric: the cases of the Nonparametric menu and what is expected of them, worked out in R from the
# definitions of the methods.  A case: the report, a name, and the inputs.  What is expected: "report|name|output<TAB>figure";
# a figure may carry its own room ("value~part" or "value~room!").  R 4.6.1, no packages.
# usage: Rscript --vanilla make-benchmarks.R cases.txt expected.txt
args <- commandArgs(trailingOnly = TRUE)
cases <- character(0); expected <- character(0); count <- 0
fig <- function(v) format(v, digits = 17, scientific = abs(v) < 1e-4 && v != 0 || abs(v) >= 1e15, trim = TRUE)
num <- function(x) if (is.character(x)) x else if (is.na(x)) "missing" else if (is.logical(x)) (if (x) "true" else "false") else if (is.infinite(x)) (if (x > 0) "Infinity" else "-Infinity") else fig(x)
column <- function(x) paste(sapply(x, function(v) if (is.na(v)) "*" else fig(v)), collapse = ",")
frame <- function(...) paste(sapply(list(...), column), collapse = "|")
case <- function(.report, .inputs, .want) {
  count <<- count + 1
  name <- sprintf("c%04d", count)
  cases <<- c(cases, paste(.report, name, paste0(names(.inputs), "=", sapply(.inputs, num), collapse = ";"), sep = "\t"))
  for (k in names(.want)) expected <<- c(expected, paste0(.report, "|", name, "|", k, "\t", num(.want[[k]])))
  invisible(name)
}
set.seed(20260930)
mid <- function(x) rank(x)                       # mid-ranks, as the program ranks
ties_sum <- function(x) { t <- table(x); sum(t^3 - t) }
median1 <- function(x) { x <- sort(x); n <- length(x); if (n %% 2) x[(n + 1) / 2] else (x[n / 2] + x[n / 2 + 1]) / 2 }
tail_of <- function(z) pnorm(-abs(z))            # the tail beyond a normal deviate on its own side

# ---- Mann-Whitney: U of the first sample is the number of pairs in which its value is the greater, ties counting a half; the
# exact P values are from every way of choosing which of the pooled values are the first sample's (for small samples), or from
# the distribution of U without ties (pwilcox); the interval of the difference is of the differences x - y in order; theta =
# U' / (n1 n2) with the limits of Newcombe's method 5, found as roots
u_of <- function(x, y) sum(outer(x, y, ">")) + 0.5 * sum(outer(x, y, "=="))
# every choice of n1 of the N pooled ranks is equally likely: the number of choices with each sum of ranks is counted over the
# ranks one by one (the ranks doubled, so that mid-ranks are whole numbers), and U = the sum less n1 (n1 + 1) / 2
exact_u <- function(x, y) {
  pooled <- c(x, y); n1 <- length(x); n <- length(pooled); r2 <- as.integer(round(2 * mid(pooled)))
  top <- sum(sort(r2, decreasing = TRUE)[1:n1])
  ways <- matrix(0, n1 + 1, top + 1); ways[1, 1] <- 1     # ways[j + 1, s + 1]: choices of j ranks with sum s
  ways <- matrix(0, n1 + 1, top + 1); ways[1, 1] <- 1
  for (rk in r2) for (j in n1:1) { s <- which(ways[j, ] > 0); s <- s[s + rk <= top + 1]; ways[j + 1, s + rk] <- ways[j + 1, s + rk] + ways[j, s] }
  w <- ways[n1 + 1, ] / choose(n, n1); sums <- (0:top) / 2 - n1 * (n1 + 1) / 2   # U for each sum of ranks
  u <- u_of(x, y)
  list(lower = sum(w[sums <= u + 1e-9]), upper = sum(w[sums >= u - 1e-9]))
}
newcombe <- function(t, m, n, gamma) {
  z <- qnorm(1 - (1 - gamma) / 2)
  f <- function(y, upper) { off <- z * sqrt(y * (1 - y) * (1 + (0.5 * (m + n) - 1) * ((1 - y) / (2 - y) + y / (1 + y))) / (m * n)); if (upper) y - off - t else y + off - t }
  # the root within (0, 1): f is 0 at y = 1 for the lower limit of a theta of 1 and at y = 0 for the upper limit of a theta of 0, roots
  # that are not the limits
  lower <- if (t == 0) 0 else uniroot(function(y) f(y, FALSE), c(0, 1 - 1e-12), tol = 1e-13)$root
  upper <- if (t == 1) 1 else uniroot(function(y) f(y, TRUE), c(1e-12, 1), tol = 1e-13)$root
  c(lower, upper)
}
level_of <- function(gamma) if (gamma <= 0 || gamma >= 1) 0.95 else gamma   # a confidence level of 0% or 100% is taken as 95%
mann_whitney <- function(x, y, gamma = 0.95, exact_by_count = TRUE, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  x <- x[!is.na(x)]; y <- y[!is.na(y)]; n1 <- length(x); n2 <- length(y); N <- n1 + n2
  u <- u_of(x, y); up <- n1 * n2 - u
  ranks <- mid(c(x, y)); r1 <- sum(ranks[1:n1])
  want <- list(obs_1 = n1, obs_2 = n2, median_1 = median1(x), median_2 = median1(y), ranksum = r1, u = u, u_prime = up, pc0 = 100 * gamma, theta = up / (n1 * n2))
  tied <- any(duplicated(c(x, y)))
  if (exact_by_count) {
    e <- if (tied) exact_u(x, y) else list(lower = pwilcox(u, n1, n2), upper = 1 - pwilcox(u - 1, n1, n2))
    want$p_l <- e$lower; want$p_u <- e$upper; want$p_2 <- min(1, 2 * min(e$lower, e$upper))
  } else {
    se <- sqrt(n1 * n2 / 12 * ((N + 1) - ties_sum(c(x, y)) / (N * (N - 1))))
    z <- (u - n1 * n2 / 2) / se
    want$p_l <- pnorm(z); want$p_u <- pnorm(-z); want$p_2 <- 2 * pnorm(-abs(z))
  }
  lim <- newcombe(up / (n1 * n2), n1, n2, gamma); want$tll <- lim[1]; want$tul <- lim[2]
  if (n1 >= 4 && n2 >= 4) {
    a <- (1 - gamma) / 2
    cdf <- pwilcox(0:(n1 * n2), n1, n2)
    k <- which(cdf > a)[1] - 1                   # the least u with P(U <= u) above alpha / 2
    if (k == 0) want[["*noconf.rows"]] <- 1
    else {
      lev <- if (k > 0) cdf[k] else 0             # P(U <= k - 1)
      d <- sort(as.vector(outer(x, y, "-")))
      want[["*conf.rows"]] <- 1
      want[["*conf[1].from"]] <- d[k]; want[["*conf[1].to"]] <- d[length(d) + 1 - k]
      want[["*conf[1].median"]] <- median1(d); want[["*conf[1].pc"]] <- (1 - 2 * lev) * 100; want[["*conf[1].k"]] <- k
    }
  } else want[["*noconf.rows"]] <- 1
  want[names(.more)] <- .more
  case("RptMannWhitney", list(gamma = level, data = frame(x, y)), want)
}
samples <- list(
  a = c(12.1, 9.8, 15.3, 11.2, 8.7, 13.9, 10.4), b = c(14.2, 16.8, 13.1, 17.5, 15.9, 12.8, 18.3, 14.9),
  c = c(3, 5, 5, 7, 8, 8, 8, 12), d = c(4, 5, 6, 8, 9, 11), e = round(rnorm(40, 50, 10)), f = round(rnorm(35, 55, 10)),
  g = rnorm(15, 0, 1), h = rnorm(12, 0.8, 1.5), i = c(1, 2, 3, 4), j = c(5, 6, 7, 8), k = c(2.5, 3.5), l = c(1, 1, 1, 2, 2, 3, 3, 3, 3, 4), m = c(2, 2, 3, 3, 3, 5, 5))
for (p in list(c("a", "b"), c("c", "d"), c("g", "h"), c("i", "j"), c("k", "d"), c("l", "m"), c("a", "k"))) for (gamma in c(0.95, 0.99, 0.8)) {
  if (gamma != 0.95 && !identical(p, c("a", "b"))) next
  mann_whitney(samples[[p[1]]], samples[[p[2]]], gamma)
}
mann_whitney(samples$e, samples$f)                                  # 40 and 35 values with ties: exact by the count of the ways
mann_whitney(c(samples$a, NA, 11.9), c(NA, samples$b))
mann_whitney(rnorm(120), rnorm(110, 0.3), exact_by_count = FALSE)   # more than 100 in each: the normal approximation
mann_whitney(rnorm(120), rnorm(110, 3), exact_by_count = FALSE)     # a deviate of about -13: an upper tail of 1e-38
mann_whitney(c(1, 2, 3, 4, 5), c(6, 7, 8, 9, 10, 11))               # no overlap: U = 0, theta = 1
mann_whitney(c(6, 7, 8, 9, 10, 11), c(1, 2, 3, 4, 5))               # U = n1 n2, theta = 0
mann_whitney(c(3, 3, 3, 3), c(3, 3, 3))                             # all the same
mann_whitney(samples$a, samples$b, 0); mann_whitney(samples$a, samples$b, 1)   # a level of 0% or 100%: 95% is taken
mann_whitney(c(1, 2, 3, 4, 5), c(7)); mann_whitney(c(7), c(1, 2, 3, 4, 5))   # a sample of one value has its median

# ---- Wilcoxon signed ranks: W is the sum of the ranks of the positive differences among the differences that are not 0; the
# exact P values are from every assignment of signs (small samples), or from the distribution without ties (psignrank); the
# interval of the median difference is of the averages of every two differences (each with itself among them), in order, with
# k from the distribution for all n pairs, zero differences among them, as the program takes it
exact_w <- function(d) {
  d <- abs(d[d != 0]); r <- mid(d); n <- length(d)
  signs <- as.matrix(expand.grid(rep(list(c(0, 1)), n)))
  ws <- as.vector(signs %*% r)
  list(ws = ws)
}
wilcoxon <- function(x, y = NULL, gamma = 0.95, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  keep <- if (is.null(y)) !is.na(x) else !is.na(x) & !is.na(y)
  d <- if (is.null(y)) x[keep] else x[keep] - y[keep]
  n <- length(d); nz <- d[d != 0]; n1 <- length(nz); r <- mid(abs(nz)); w <- sum(r[nz > 0]); wmax <- n1 * (n1 + 1) / 2
  want <- list(non_0 = n1, sum = paste("Sum of ranks for positive differences =", fig(w)))
  tied <- any(duplicated(abs(nz)))
  if (n1 <= 200) {
    if (tied) { ws <- exact_w(d)$ws; pl <- mean(ws <= w + 1e-9); pu <- mean(ws >= w - 1e-9) }
    else { pl <- psignrank(w, n1); pu <- 1 - psignrank(w - 1, n1) }
    want$p_l <- pl; want$p_u <- pu; want$p_2 <- min(1, 2 * min(pl, pu))
  } else {
    z <- sum(r * sign(nz)) / sqrt(sum(r^2))
    want$p_l <- pnorm(z); want$p_u <- pnorm(-z); want$p_2 <- min(1, 2 * min(pnorm(z), pnorm(-z)))
  }
  if (n1 >= 4 && n < 200) {
    a <- (1 - gamma) / 2
    cdf <- psignrank(0:(n * (n + 1) / 2), n)
    k <- which(cdf >= a - 1e-12)[1] - 1           # the least w with P(W <= w) at least alpha / 2 (the program nudges alpha down by 10 epsilon)
    if (k == 0) want[["*noconf.rows"]] <- 1
    else {
      lev <- 1 - 2 * psignrank(k - 1, n)
      walsh <- sort(as.vector(outer(d, d, "+")[lower.tri(diag(n), diag = TRUE)] / 2))
      want[["*conf.rows"]] <- 1
      want[["*conf[1].from"]] <- walsh[k]; want[["*conf[1].to"]] <- walsh[length(walsh) + 1 - k]; want[["*conf[1].med_diff"]] <- median1(walsh)
      want[["*conf[1].pc"]] <- paste0(format(round(100 * lev, 1), trim = TRUE), "% confidence interval for difference between population medians:")
      want[["*conf[1].k"]] <- paste0("K = ", k)
    }
  } else if (n1 < 4) want[["*noconf.rows"]] <- 1
  want[names(.more)] <- .more
  case("RptWilcoxon", list(gamma = level, data = if (is.null(y)) frame(x) else frame(x, y)), want)
}
pairs_x <- list(c(1.83, 0.50, 1.62, 2.48, 1.68, 1.88, 1.55, 3.06, 1.30), c(130, 125, 140, 118, 149, 132, 121, 128, 137, 126, 133, 129), round(rnorm(20, 10, 3), 1), c(5, 7, 9, 11, 13, 15))
pairs_y <- list(c(0.878, 0.647, 0.598, 2.05, 1.06, 1.29, 1.06, 3.14, 1.29), c(128, 125, 132, 120, 150, 130, 125, 128, 130, 126, 130, 129), round(rnorm(20, 11, 3), 1), c(6, 6, 8, 12, 12, 16))
for (i in seq_along(pairs_x)) for (gamma in c(0.95, 0.99, 0.9)) { if (gamma != 0.95 && i != 1) next; wilcoxon(pairs_x[[i]], pairs_y[[i]], gamma) }
wilcoxon(pairs_x[[1]] - pairs_y[[1]])                                # the differences in one column
wilcoxon(c(pairs_x[[1]], NA, 2), c(pairs_y[[1]], 1, NA))
wilcoxon(c(2, 4, 6, 8, 10), c(1, 3, 5, 7, 9))                        # every difference positive, 5 pairs
wilcoxon(c(1, 2, 3), c(3, 2, 1))                                     # a zero difference and two that cancel
wilcoxon(round(rnorm(250, 0.2, 1), 2))                               # more than 200: the normal approximation
wilcoxon(pairs_x[[1]], pairs_y[[1]], 0); wilcoxon(pairs_x[[1]], pairs_y[[1]], 1)   # a level of 0% or 100%: 95% is taken

# ---- Kendall: the counts of pairs, the score S = C - D, its variance with the correction for ties, tau b, gamma, the
# Samara-Randles limits, the P values from the normal deviate with and without the continuity correction, and the exact P
# values from the distribution of S without ties, which is that of the number of inversions of a permutation
kendall_dist <- function(n) {  # P(D = d) for the number of discordant pairs of a random permutation of n
  # the j-th value is put among the j - 1 before it in one of j places, each equally likely, and makes 0 to j - 1 new discordant
  # pairs: the distribution is that of the sum of independent uniform counts, each convolution taken with running sums
  p <- 1
  for (j in 2:n) { P <- cumsum(c(p, rep(0, j - 1))); p <- (P - c(rep(0, j), head(P, -j))) / j }
  p
}
kendall <- function(x, y, gamma = 0.95, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  keep <- !is.na(x) & !is.na(y); x <- x[keep]; y <- y[keep]; n <- length(x)
  sx <- sign(outer(x, x, "-")); sy <- sign(outer(y, y, "-")); prod <- sx * sy
  con <- sum(prod[upper.tri(prod)] > 0); dis <- sum(prod[upper.tri(prod)] < 0); s <- con - dis
  tx <- table(x); ty <- table(y); siga <- sum(tx * (tx - 1) / 2); sigb <- sum(ty * (ty - 1) / 2); hn <- n * (n - 1) / 2
  t1 <- function(t) sum(t * (t - 1)); t2 <- function(t) sum(t * (t - 1) * (t - 2)); t3 <- function(t) sum(t * (t - 1) * (2 * t + 5))
  varf <- if (siga > 0 || sigb > 0) (n * (n - 1) * (2 * n + 5) - t3(tx) - t3(ty)) / 18 + t2(tx) * t2(ty) / (9 * n * (n - 1) * (n - 2)) + t1(tx) * t1(ty) / (2 * n * (n - 1)) else n * (n - 1) * (2 * n + 5) / 18
  tau <- s / sqrt((hn - siga) * (hn - sigb))
  c_i <- rowSums(prod) # the score of each point against the others (the diagonal is 0)
  cbar <- 2 * s / n
  vr <- 2 / (n * (n - 1)) * (2 * (n - 2) / (n * (n - 1)^2) * sum((c_i - cbar)^2) + 1 - tau^2)
  cit <- qnorm(1 - (1 - gamma) / 2)
  kz <- s / sqrt(varf); kzcc <- if (s > 0) (s - 1) / sqrt(varf) else if (s < 0) (s + 1) / sqrt(varf) else 0
  pd <- kendall_dist(n)   # P(D = d), d = 0..hn; S = hn - 2 D, so P(S >= s) = P(D <= (hn - s) / 2)
  p_ge <- function(sc) sum(pd[seq_len(floor((hn - sc) / 2) + 1)])
  want <- list(obs = n, con = con, dis = dis, tie = siga + sigb, s = s, ses = sqrt(varf), gam = if (con + dis > 0) s / (con + dis) else NA, tau = tau, pc = 100 * gamma,
               ll = max(-1, tau - cit * sqrt(vr)), ul = min(1, tau + cit * sqrt(vr)), tb = if (siga > 0 || sigb > 0) "tau b" else "tau",
               kz = kz, p_u = pnorm(-kz), p_l = pnorm(kz), p_2 = 2 * pnorm(-abs(kz)),
               kzcc = kzcc, p_ucc = pnorm(-kzcc), p_lcc = pnorm(kzcc), p_2cc = 2 * pnorm(-abs(kzcc)),
               p_uexact = p_ge(s), p_lexact = p_ge(-s), p_2exact = min(1, 2 * min(p_ge(s), p_ge(-s))), `*smallsample.rows` = if (n < 11) 1 else 0)
  want[names(.more)] <- .more
  case("RptKendall", list(gamma = level, data = frame(x, y)), want)
}
kx <- list(c(4, 10, 3, 1, 9, 2, 6, 7, 8, 5), c(1.5, 2.1, 3.3, 4.8, 5.1, 6.2, 7.9), round(rnorm(30, 0, 5)), c(1, 2, 2, 3, 3, 3, 4, 5, 5, 6, 7, 7), c(10, 9, 8, 7, 6), rnorm(60))
ky <- list(c(5, 8, 6, 2, 10, 3, 9, 4, 7, 1), c(2.2, 1.9, 3.1, 4.4, 6.0, 5.7, 8.1), round(rnorm(30, 0, 5)), c(2, 1, 3, 3, 5, 4, 4, 6, 7, 6, 8, 9), c(1, 2, 3, 4, 5), rnorm(60))
for (i in seq_along(kx)) for (gamma in c(0.95, 0.99)) { if (gamma != 0.95 && i != 1) next; kendall(kx[[i]], ky[[i]], gamma) }
kendall(c(kx[[1]], NA, 11), c(ky[[1]], 12, NA))
kendall(c(1, 2, 3, 4), c(1, 2, 3, 4))    # tau 1
kendall(c(1, 2, 3, 4, 5, 6), c(3, 1, 2, 6, 4, 5))
kendall(kx[[1]], ky[[1]], 0); kendall(kx[[1]], ky[[1]], 1)   # a level of 0% or 100%: 95% is taken
kendall(rnorm(120), rnorm(120)); kendall(rnorm(300), rnorm(300) + 0.1 * (1:300))   # the exact P with many observations
k60 <- rnorm(60); kendall(k60, k60 + rnorm(60, 0, 0.1))   # a deviate of about 10: an upper tail of 1e-23

# ---- Spearman: rho is the correlation of the ranks; the exact P values (without ties) are from every ordering of the second
# variable's ranks, of the sum of the squared differences S: the lower side of rho is P(S >= s) and the upper P(S <= s); with
# ties the P values are from t = rho sqrt((n - 2) / (1 - rho^2)) on n - 2 degrees of freedom; Fisher's interval
permutations <- function(n) { if (n == 1) return(matrix(1, 1, 1)); p <- permutations(n - 1); do.call(rbind, lapply(1:n, function(i) cbind(i, p + (p >= i)))) }
spearman <- function(x, y, gamma = 0.95, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  keep <- !is.na(x) & !is.na(y); x <- x[keep]; y <- y[keep]; n <- length(x)
  rx <- mid(x); ry <- mid(y); rho <- cor(rx, ry)
  want <- list(obs = n, rho = rho, `*ties.rows` = if (any(duplicated(x)) || any(duplicated(y))) 1 else 0)
  if (n >= 4 && 1 - abs(rho) >= 1e-12) {
    cit <- qnorm(1 - (1 - gamma) / 2); fz <- atanh(rho)
    want[["*ci.rows"]] <- 1; want[["*ci[1].pc"]] <- 100 * gamma; want[["*ci[1].from"]] <- tanh(fz - cit / sqrt(n - 3)); want[["*ci[1].to"]] <- tanh(fz + cit / sqrt(n - 3))
  } else if (n >= 4) want[["*noci.rows"]] <- 1
  if (n < 4) want[["*lown.rows"]] <- 1
  else {
    want[["*results.rows"]] <- 1
    s <- sum((rx - ry)^2)
    want$ix <- (1 - rho) * n * (n^2 - 1) / 6
    if (any(duplicated(x)) || any(duplicated(y))) {
      if (1 - rho^2 <= 1e-12) p1 <- 0 else { t <- abs(rho) * sqrt(n - 2) / sqrt(1 - rho^2); p1 <- pt(-t, n - 2) }
      pu <- if (rho > 0) p1 else if (rho < 0) 1 - p1 else 0.5; pl <- if (rho > 0) 1 - p1 else if (rho < 0) p1 else 0.5
      want[["*results[1].*results[1].p_u"]] <- pu; want[["*results[1].*results[1].p_l"]] <- pl; want[["*results[1].*results[1].p_2"]] <- min(1, 2 * p1)
    } else if (n <= 9) {
      perms <- permutations(n); ss <- rowSums((matrix(rx, nrow(perms), n, byrow = TRUE) - perms)^2)
      pl <- mean(ss >= s - 1e-9); pu <- mean(ss <= s + 1e-9)
      want[["*results[1].*results[1].p_u"]] <- pu; want[["*results[1].*results[1].p_l"]] <- pl; want[["*results[1].*results[1].p_2"]] <- min(1, 2 * min(pl, pu))
    }
  }
  want[names(.more)] <- .more
  case("RptSpearman", list(gamma = level, data = frame(x, y)), want)
}
for (i in seq_along(kx)) spearman(kx[[i]], ky[[i]])
spearman(c(1, 2, 3, 4, 5, 6, 7, 8), c(2, 1, 4, 3, 6, 5, 8, 7), 0.99)
spearman(c(1, 2, 3, 4, 5, 6, 7, 8, 9), c(9, 8, 7, 6, 5, 4, 3, 2, 1))   # rho -1
spearman(c(1, 2, 3, 4, 5), c(1, 2, 3, 4, 5))                          # rho 1
spearman(c(1, 2, 3), c(3, 1, 2))                                      # 3 pairs
spearman(c(1.5, 2.5, 3.5, 4.5, 5.5, 6.5, 7.5), c(3, 1, 2, 7, 4, 6, 5), 0.9)
spearman(kx[[1]], ky[[1]], 0); spearman(kx[[1]], ky[[1]], 1)   # a level of 0% or 100%: 95% is taken

# ---- nonparametric linear regression: the median of the slopes of every two points with different x, the intercept through
# the medians, and the limits from Kendall's distribution (Conover): r = floor((N - w) / 2) where w is the greatest score with
# an upper tail of at least alpha / 2, the r-th slope from each end; tau b with its two sided P from the corrected deviate
np_regression <- function(y, x, gamma = 0.95, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  keep <- !is.na(x) & !is.na(y); x <- x[keep]; y <- y[keep]; n <- length(x)
  pairs <- combn(n, 2); dx <- x[pairs[2, ]] - x[pairs[1, ]]; dy <- y[pairs[2, ]] - y[pairs[1, ]]
  slopes <- sort((dy / dx)[dx != 0]); N <- length(slopes)
  mdn <- median1(slopes); intercept <- median1(y) - mdn * median1(x)
  hn <- n * (n - 1) / 2; pd <- kendall_dist(n)
  p_ge <- function(sc) sum(pd[seq_len(floor((hn - sc) / 2) + 1)])
  a <- (1 - gamma) / 2
  scores <- seq(hn, -hn, by = -2); w <- scores[which(sapply(scores, p_ge) >= a - 1e-12)[1]]
  r <- floor((N - w) / 2)
  want <- list(obs = n, `*results[1].pc` = 100 * gamma, `*results[1].mdn` = mdn, `*results[1].intercept` = intercept, mdnValue = mdn, interceptValue = intercept)
  if (r < 1) { want[["*results[1].from"]] <- NA; want[["*results[1].to"]] <- NA; want[["*results[1].cinote"]] <- "  (too few pairs for an interval at this level of confidence)" }
  else { want[["*results[1].from"]] <- slopes[r]; want[["*results[1].to"]] <- slopes[N + 1 - r]; want[["*results[1].cinote"]] <- "" }
  # tau b and its P value, as in the Kendall report
  sx <- sign(outer(x, x, "-")); sy <- sign(outer(y, y, "-")); prod <- sx * sy
  s <- sum(prod[upper.tri(prod)]); tx <- table(x); ty <- table(y); siga <- sum(tx * (tx - 1) / 2); sigb <- sum(ty * (ty - 1) / 2)
  t1 <- function(t) sum(t * (t - 1)); t2 <- function(t) sum(t * (t - 1) * (t - 2)); t3 <- function(t) sum(t * (t - 1) * (2 * t + 5))
  varf <- if (siga > 0 || sigb > 0) (n * (n - 1) * (2 * n + 5) - t3(tx) - t3(ty)) / 18 + t2(tx) * t2(ty) / (9 * n * (n - 1) * (n - 2)) + t1(tx) * t1(ty) / (2 * n * (n - 1)) else n * (n - 1) * (2 * n + 5) / 18
  want$tau <- s / sqrt((hn - siga) * (hn - sigb)); want$taulab <- if (siga > 0 || sigb > 0) "tau b" else "tau"
  kz <- if (s > 0) (s - 1) / sqrt(varf) else if (s < 0) (s + 1) / sqrt(varf) else 0
  want$p_2 <- 2 * pnorm(-abs(kz))
  want[names(.more)] <- .more
  case("RptNpRegression", list(gamma = level, outcome = frame(y), predictor = frame(x)), want)
}
np_regression(c(2.1, 3.9, 6.2, 7.8, 10.1, 12.3, 13.8, 16.2, 18.1, 19.9), 1:10)
np_regression(c(2.1, 3.9, 6.2, 7.8, 10.1, 12.3, 13.8, 16.2, 18.1, 19.9), 1:10, 0.99)
np_regression(round(rnorm(25, 0, 3) + 2 * (1:25), 1), 1:25)
np_regression(c(3, 5, 4, 8, 9, 7, 12, 11), c(1, 1, 2, 2, 3, 3, 4, 4))       # ties in x
np_regression(c(5, 3, 8, 7, 9, 2), c(1, 2, 3, 4, 5, 6), 0.99)                # 6 points at 99%: too few slopes
np_regression(c(5, NA, 8, 7, 9, 2, 4), c(1, 2, 3, NA, 5, 6, 7))
np_regression(c(2.1, 3.9, 6.2, 7.8, 10.1, 12.3, 13.8, 16.2, 18.1, 19.9), 1:10, 0)   # a level of 0% or 100%: 95% is taken
np_regression(c(2.1, 3.9, 6.2, 7.8, 10.1, 12.3, 13.8, 16.2, 18.1, 19.9), 1:10, 1)

# ---- Cuzick's test for trend across ordered groups: the scores of the groups, T = the sum over the groups of the score times the
# sum of the ranks in the group, its expectation and variance, and the correction for ties
cuzick <- function(groups, scores = NULL, .more = list()) {
  k <- length(groups); l <- if (is.null(scores)) 1:k else scores
  values <- unlist(lapply(groups, function(g) g[!is.na(g)])); n <- length(values); gn <- sapply(groups, function(g) sum(!is.na(g)))
  r <- mid(values); group <- rep(1:k, gn)
  st <- sum(r * l[group]); ez <- sum(l * gn / n); varz <- sum(l^2 * gn / n) - ez^2
  et <- n * (n + 1) / 2 * ez; vart <- n^2 * (n + 1) / 12 * varz; z <- (st - et) / sqrt(vart)
  want <- list(groups = k, obs = n, ez = ez, varz = varz, t = st, et = et, vart = vart, z = z, p_1 = tail_of(z), p_2 = 2 * tail_of(z))
  tie <- ties_sum(values) / 12
  if (tie != 0) { varttie <- vart * (1 - tie * 12 / (n * (n^2 - 1))); ztie <- (st - et) / sqrt(varttie)
    want[["*ties.rows"]] <- 1; want[["*ties[1].varttie"]] <- varttie; want[["*ties[1].ztie"]] <- ztie; want[["*ties[1].p_1tie"]] <- tail_of(ztie); want[["*ties[1].p_2tie"]] <- 2 * tail_of(ztie) }
  want[names(.more)] <- .more
  inputs <- list(data = do.call(frame, groups)); if (!is.null(scores)) inputs$scores <- frame(scores)
  case("RptCuzick", inputs, want)
}
cuzick(list(c(2.1, 3.4, 1.8, 2.9), c(3.6, 4.1, 3.9, 5.2, 4.4), c(5.1, 6.3, 5.8, 7.2)))
cuzick(list(c(2.1, 3.4, 1.8, 2.9), c(3.6, 4.1, 3.9, 5.2, 4.4), c(5.1, 6.3, 5.8, 7.2)), c(1, 2, 5))
cuzick(list(c(1, 2, 2, 3), c(2, 3, 3, 4, 4), c(4, 5, 5, 6), c(6, 6, 7)))
cuzick(list(round(rnorm(20, 10, 2)), round(rnorm(25, 11, 2)), round(rnorm(15, 12, 2)), round(rnorm(30, 14, 2))))
cuzick(list(c(1, 2, 3, NA), c(NA, 4, 5, 6), c(7, 8, 9)))
cuzick(list(c(10, 12, 14, 11, 13), c(1, 2, 3, 4, 5, 6), c(20, 21, 22)), c(3, 1, 2))
cuzick(list(1:10 + 100, 1:10 + 130, 1:10 + 160))       # a deviate of 5: a P of 2e-7
cuzick(list(1:20 + 100, 1:20 + 130, 1:20 + 160, 1:20 + 190))   # a deviate of 8.6: a P of 4e-18

# ---- Smirnov: D = the greatest difference of the two empirical distribution functions, D+ and D-; exact P values from every
# way of choosing which of the pooled values are the first sample's, with the ties as they are
ecdf_diffs <- function(x, y) { z <- sort(unique(c(x, y))); f1 <- ecdf(x)(z); f2 <- ecdf(y)(z); c(d = max(abs(f1 - f2)), dp = max(0, max(f1 - f2)), dn = max(0, max(f2 - f1))) }
smirnov <- function(x, y, .more = list()) {
  x <- x[!is.na(x)]; y <- y[!is.na(y)]; n1 <- length(x); n2 <- length(y); pooled <- c(x, y)
  d <- ecdf_diffs(x, y)
  picks <- combn(n1 + n2, n1)
  all <- apply(picks, 2, function(i) ecdf_diffs(pooled[i], pooled[-i]))
  want <- list(d = d[["d"]], d_l = d[["dp"]], d_r = d[["dn"]], p = mean(all["d", ] >= d[["d"]] - 1e-9), p_l = mean(all["dp", ] >= d[["dp"]] - 1e-9), p_r = mean(all["dn", ] >= d[["dn"]] - 1e-9))
  want[names(.more)] <- .more
  case("RptSmirnov", list(data = frame(x, y)), want)
}
smirnov(samples$a, samples$b); smirnov(samples$c, samples$d); smirnov(samples$i, samples$j); smirnov(samples$l, samples$m)
smirnov(c(0.61, 0.29, 0.06, 0.59, -1.73, -0.74, 0.51, -0.56, 0.39, 1.64, 0.05, -0.06), c(2.20, 1.66, 1.38, 0.20, 0.36, 0.00, 0.96, 1.56, 0.44))   # 12 and 9 values: 293,930 ways
smirnov(c(1, 2, 3, NA, 5), c(2, NA, 4, 6))

# ---- quantile confidence interval: the quantile at the place q (n + 1) of the values in order; the limits are the order
# statistics whose binomial probabilities are nearest to alpha / 2 and 1 - alpha / 2 (or, conservative, at least as far out),
# with the level that they have
quantile_ci <- function(x, q = 0.5, gamma = 0.95, conservative = FALSE, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  x <- x[!is.na(x)]; r <- sort(x); n <- length(r); a <- (1 - gamma) / 2
  iq <- min(max(q * (n + 1), 1), n); lo <- floor(iq); value <- if (iq == lo) r[lo] else r[lo] + (r[lo + 1] - r[lo]) * (iq - lo)
  cdf <- pbinom(0:n, n, q)   # P(Y <= j), j = 0..n: the chance that at most j values are below the quantile
  if (n > 200) {
    z <- qnorm(1 - a); ll <- n * q - z * sqrt(n * q * (1 - q)); ul <- n * q + z * sqrt(n * q * (1 - q)); capl <- FALSE; capu <- FALSE
    if (ll < 1) { ll <- 0; capl <- TRUE }
    if (ul + 1 > n) { ul <- n - 1; capu <- TRUE }
    if (conservative) {
      while (ll >= 1 && cdf[floor(ll) + 1] > a) ll <- ll - 1
      if (ll < 1) { ll <- 0; capl <- TRUE }
      while (ul + 1 < n && cdf[floor(ul) + 1] < 1 - a) ul <- ul + 1
      if (ul + 1 >= n) { ul <- n - 1; capu <- TRUE }
    }
    lid <- floor(ll); uid <- floor(ul)
  } else {
    if (conservative) {
      ok_u <- which(cdf >= 1 - a); uid <- (ok_u[which.min(abs(cdf[ok_u] - (1 - a)))] - 1)
      ok_l <- which(cdf <= a); lid <- if (length(ok_l)) ok_l[which.min(abs(cdf[ok_l] - a))] - 1 else 0
    } else {
      uid <- which.min(abs(cdf - (1 - a))) - 1; lid <- which.min(abs(cdf - a)) - 1
    }
    capl <- lid < 1; capu <- uid + 1 > n
    if (capl) lid <- 0
    if (capu) uid <- n - 1
  }
  want <- list(`*variable[1].size` = n, `*variable[1].value` = value, `*variable[1].pc` = 100 * gamma, `*variable[1].from` = r[lid + 1], `*variable[1].to` = r[uid + 1],
               `*variable[1].exact` = (cdf[uid + 1] - cdf[lid + 1]) * 100, `*variable[1].type` = if (conservative) "(conservative)" else "(non-conservative)",
               `*variable[1].note_from` = if (capl) "* " else "", `*variable[1].note_to` = if (capu) "* " else "", `*variable[1].quantile_name` = if (q == 0.5) "median" else fig(q))
  want[names(.more)] <- .more
  case("RptQuantile", list(data = frame(x), gamma = level, `conservative-ci` = conservative, quantile = q), want)
}
qx <- list(c(1.3, 2.8, 3.6, 4.2, 5.9, 6.1, 7.4, 8.8, 9.2, 10.5, 11.9), round(rnorm(30, 100, 15), 1), rnorm(200), rnorm(500), c(1, 2, 3, 4, 5, 6, 7), rexp(60))
for (x in qx) for (q in c(0.5, 0.25, 0.9)) for (cons in c(FALSE, TRUE)) quantile_ci(x, q, 0.95, cons)
quantile_ci(qx[[2]], 0.5, 0.99); quantile_ci(qx[[2]], 0.5, 0.8, TRUE); quantile_ci(qx[[4]], 0.05, 0.95, TRUE); quantile_ci(qx[[4]], 0.95, 0.99)
quantile_ci(c(qx[[1]], NA, NA), 0.5); quantile_ci(c(5, 7), 0.5); quantile_ci(1:12, 0.75, 0.9, TRUE)
quantile_ci(qx[[1]], 0.5, 0); quantile_ci(qx[[1]], 0.5, 1)   # a level of 0% or 100%: 95% is taken

# ---- Kruskal-Wallis, its multiple comparisons, and the squared ranks test for variances
# P(range of k standard normal variates <= w): k times the integral over z of the density at z times the probability that the other
# k - 1 are within w above it; the integrand is found where it peaks and integrated about there, and the point of a probability is
# the root
range_p <- function(w, k) {
  if (w <= 0) return(0)
  logf <- function(z) dnorm(z, log = TRUE) + (k - 1) * log(pmax(pnorm(z + w) - pnorm(z), 1e-300))
  peak <- optimize(function(z) -logf(z), c(-w, 0))$minimum
  top <- logf(peak); f <- function(z) exp(logf(z) - top)
  h <- 1e-3; curve <- (logf(peak + h) - 2 * logf(peak) + logf(peak - h)) / h^2; sd <- 1 / sqrt(-curve)
  k * exp(top) * integrate(f, peak - 12 * sd, peak + 12 * sd, rel.tol = 1e-13, abs.tol = 0, subdivisions = 5000)$value
}
range_q <- function(p, k) uniroot(function(w) range_p(w, k) - p, c(0.01, 30), tol = 1e-13)$root
range_density <- function(w, k) (range_p(w + 1e-5, k) - range_p(w - 1e-5, k)) / 2e-5
kruskal <- function(groups, .more = list()) {
  if (!is.null(.more$refused)) return(case("RptKruskal", list(data = do.call(frame, groups)), list(refused = .more$refused)))
  groups <- lapply(groups, function(g) g[!is.na(g)]); k <- length(groups); values <- unlist(groups); N <- length(values); n <- sapply(groups, length)
  r <- mid(values); group <- rep(1:k, n); rs <- tapply(r, group, sum)
  h <- 12 / (N * (N + 1)) * sum(rs^2 / n) - 3 * (N + 1)
  t <- ties_sum(values) / 12
  want <- list(grps = k, df = k - 1, tot_obs = N, t = h, p = pchisq(h, k - 1, lower.tail = FALSE), rlist = paste(format(round(rs / n, 2), trim = TRUE, drop0trailing = TRUE), collapse = ", "))
  if (t != 0) { ha <- h / (1 - 12 * t / (N^3 - N)); want[["*ties.rows"]] <- 1; want[["*ties[1].t_ties"]] <- ha; want[["*ties[1].p_ties"]] <- pchisq(ha, k - 1, lower.tail = FALSE) }
  want[names(.more)] <- .more
  case("RptKruskal", list(data = do.call(frame, groups)), want)
}
kw_groups <- list(
  list(c(2.9, 3.0, 2.5, 2.6, 3.2), c(3.8, 2.7, 4.0, 2.4), c(2.8, 3.4, 3.7, 2.2, 2.0)),
  list(c(68, 93, 123, 83, 108, 122), c(119, 116, 101, 103, 113, 84), c(70, 68, 54, 73, 81, 68), c(61, 54, 59, 67, 59, 70)),
  list(round(rnorm(12, 10, 2)), round(rnorm(15, 12, 2)), round(rnorm(9, 11, 2)), round(rnorm(14, 14, 2)), round(rnorm(11, 10, 2))),
  list(c(1, 1, 2, 2, 3), c(2, 3, 3, 4, 4, 5), c(1, 2, 2), c(5, 5, 6, 7)),
  list(c(1, 2, 3, NA, 5), c(6, 7, NA, 9), c(10, 11, 12)))
for (g in kw_groups) kruskal(g)
kruskal(list(c(3, 3, 3), c(3, 3), c(3, 3, 3, 3)), .more = list(refused = "TemplateOperationCancelledException: The Kruskal-Wallis test cannot be calculated when all of the observations are the same."))

kw_multiple <- function(groups, confidence = 0.95, sided = 2, .more = list()) {
  groups <- lapply(groups, function(g) g[!is.na(g)]); k <- length(groups); values <- unlist(groups); N <- length(values); n <- sapply(groups, length)
  r <- mid(values); group <- rep(1:k, n); rs <- tapply(r, group, sum)
  h <- 12 / (N * (N + 1)) * sum(rs^2 / n) - 3 * (N + 1); t <- ties_sum(values) / 12; hprime <- if (t != 0) h / (1 - 12 * t / (N^3 - N)) else h
  # a confidence of 0.05 is a 5% test, as 0.95 is; a level of 0% or 100% is taken as 95%
  conf <- level_of(confidence); alpha <- min(conf, 1 - conf); if (sided == 2) alpha <- alpha / 2
  want <- list(df = N - k, t = qt(alpha, N - k, lower.tail = FALSE), sidedoutput = if (sided == 1) "one" else "two")
  s2 <- (sum(r^2) - N * (N + 1)^2 / 4) / (N - 1); s2x <- s2 * (N - 1 - hprime) / (N - k)
  row <- 0
  # the range of k means with degrees of freedom without end, by an integration of the density of the range and its inverse; the
  # program has this from a series that is within 5e-7 in probability, so the room given is that
  if (sided == 2) { qval <- range_q(1 - min(conf, 1 - conf), k); want$q <- paste0(fig(qval), "~", 5e-7 / range_density(qval, k) / qval) }
  for (i in 1:(k - 1)) for (j in (i + 1):k) {
    row <- row + 1
    if (sided == 2) {
      xi <- groups[[i]]; xj <- groups[[j]]; rij <- mid(c(xi, xj)); ni <- length(xi); nj <- length(xj)
      if (ni < nj) { w <- sum(rij[1:ni]); nii <- ni; njj <- nj } else { w <- sum(rij[(ni + 1):(ni + nj)]); nii <- nj; njj <- ni }
      ct <- ties_sum(c(xi, xj)); v <- nii * njj / 24 * (nii + njj + 1 - ct / ((nii + njj) * (nii + njj - 1)))
      wx <- (w - nii * (nii + njj + 1) / 2) / sqrt(v)
      want[[sprintf("*dwass[%d].wx", row)]] <- wx; want[[sprintf("*dwass[%d].p", row)]] <- paste0(fig(1 - range_p(abs(wx), k)), "~5e-7!")
      want[[sprintf("*dwass[%d].diff", row)]] <- if (abs(wx) > qval) "significant" else "not significant"
    }
    stata <- abs(rs[i] / n[i] - rs[j] / n[j]); statq <- sqrt(s2x) * sqrt(1 / n[i] + 1 / n[j]); statb <- want$t * statq
    want[[sprintf("*conover[%d].stata", row)]] <- stata; want[[sprintf("*conover[%d].statb", row)]] <- statb
    want[[sprintf("*conover[%d].diff", row)]] <- if (stata > statb) "significant" else "not significant"
    p1 <- pt(-abs(stata / statq), N - k)
    want[[sprintf("*conover[%d].p", row)]] <- if (sided == 1) p1 else 2 * p1
    want[[sprintf("*conover[%d].compare", row)]] <- if (sided == 1) (if (rs[i] / n[i] > rs[j] / n[j]) ">" else "<") else "vs"
  }
  want[["*conover.rows"]] <- row
  want[names(.more)] <- .more
  case("RptKwMultiple", list(data = do.call(frame, groups), confidence = confidence, sided = as.character(sided)), want)
}
for (g in kw_groups[1:4]) for (sided in 1:2) kw_multiple(g, 0.95, sided)
kw_multiple(kw_groups[[2]], 0.99); kw_multiple(kw_groups[[2]], 0.05); kw_multiple(kw_groups[[3]], 0.9, 1)
kw_multiple(kw_groups[[2]], 0); kw_multiple(kw_groups[[2]], 1)   # a level of 0% or 100%: 95% is taken

squared_ranks <- function(groups, confidence = 0.95, .more = list()) {
  groups <- lapply(groups, function(g) g[!is.na(g)]); k <- length(groups); n <- sapply(groups, length); N <- sum(n)
  dev <- unlist(lapply(groups, function(g) abs(g - mean(g)))); r <- mid(dev); group <- rep(1:k, n)
  sj <- tapply(r^2, group, sum); sbar <- sum(r^2) / N; r4 <- sum(r^4)
  want <- list()
  if (k > 2) {
    d2 <- (r4 - N * sbar^2) / (N - 1); t2 <- (sum(sj^2 / n) - N * sbar^2) / d2
    want$x2 <- t2; want$df <- k - 1; want$p <- pchisq(t2, k - 1, lower.tail = FALSE)
    conf <- level_of(confidence); alpha <- min(conf, 1 - conf)
    if (want$p <= alpha) {
      df <- N - k; tval <- qt(alpha / 2, df, lower.tail = FALSE)
      want[["*pairwise.rows"]] <- 1; want[["*pairwise[1].df"]] <- df; want[["*pairwise[1].t"]] <- tval
      row <- 0
      for (i in 1:(k - 1)) for (j in (i + 1):k) {
        row <- row + 1
        stata <- abs(sj[i] / n[i] - sj[j] / n[j]); statq <- sqrt(d2 * (N - 1 - t2) / (N - k)) * sqrt(1 / n[i] + 1 / n[j])
        want[[sprintf("*pairwise[1].*pair[%d].stata", row)]] <- stata; want[[sprintf("*pairwise[1].*pair[%d].statb", row)]] <- tval * statq
        want[[sprintf("*pairwise[1].*pair[%d].p_pair", row)]] <- 2 * pt(-abs(stata / statq), df)
        want[[sprintf("*pairwise[1].*pair[%d].dif", row)]] <- if (stata > tval * statq) "VARIANCES SEEM DIFFERENT" else "variances not different"
      }
    } else want[["*pairwise.rows"]] <- 0
  } else {
    nm <- n[1] * n[2]; t1 <- (sj[1] - n[1] * sbar) / sqrt(nm / (N * (N - 1)) * r4 - nm / (N - 1) * sbar^2)
    want$z <- t1; want$p1 <- tail_of(t1); want$p2 <- 2 * tail_of(t1)
  }
  want[names(.more)] <- .more
  case("RptSqRank", list(data = do.call(frame, groups), confidence = confidence), want)
}
squared_ranks(list(c(10.8, 11.1, 10.4, 10.1, 11.3), c(10.8, 10.5, 11.0, 10.9, 10.8, 10.7, 10.8)))
squared_ranks(list(c(2.9, 3.0, 2.5, 2.6, 3.2), c(3.8, 2.7, 4.0, 2.4), c(2.8, 3.4, 3.7, 2.2, 2.0)))
squared_ranks(list(rnorm(12, 0, 1), rnorm(10, 0, 4), rnorm(15, 0, 1), rnorm(8, 0, 6)))
squared_ranks(list(rnorm(12, 0, 1), rnorm(10, 0, 4), rnorm(15, 0, 1), rnorm(8, 0, 6)), 0.99)
squared_ranks(list(rnorm(12, 0, 1), rnorm(10, 0, 1.1)), 0.9)
squared_ranks(list(rnorm(30, 0, 1), rnorm(30, 0, 100)))           # two groups far apart in spread: a deviate of 7, a P of 1e-12
squared_ranks(list(rnorm(12, 0, 1), rnorm(10, 0, 4), rnorm(15, 0, 1), rnorm(8, 0, 6)), 0)   # a level of 0% or 100%: 95% is taken
squared_ranks(list(rnorm(12, 0, 1), rnorm(10, 0, 4), rnorm(15, 0, 1), rnorm(8, 0, 6)), 1)

# ---- Friedman and Cochran's Q, with the multiple comparisons: the ranks within each block; A1 the sum of the squared ranks,
# B1 the sum of the squared rank sums of the treatments over the blocks, T1 the chi-square form and T2 the F form (Conover); with
# values of 0 and 1 only T1 is Cochran's Q
friedman <- function(cols, .more = list()) {
  if (!is.null(.more$refused)) return(case("RptFriedman", list(data = do.call(frame, cols)), list(refused = .more$refused)))
  m <- do.call(cbind, cols); m <- m[complete.cases(m), , drop = FALSE]; n <- nrow(m); k <- ncol(m)
  ranks <- t(apply(m, 1, rank)); rj <- colSums(ranks)
  a1 <- sum(ranks^2); b1 <- sum(rj^2) / n; c1 <- n * k * (k + 1)^2 / 4
  t1 <- (k - 1) * n * (b1 - c1) / (a1 - c1); t2 <- (n - 1) * (b1 - c1) / (a1 - b1)
  binary <- all(m == 0 | m == 1)
  want <- list(nb = n, df = k - 1, a2 = a1, t1 = t1, t2 = t2, rlist = paste(format(round(rj / n, 2), trim = TRUE, drop0trailing = TRUE), collapse = ", "),
               testName = if (binary) "Cochran" else "Friedman", p = if (binary) pchisq(t1, k - 1, lower.tail = FALSE) else pf(t2, k - 1, (n - 1) * (k - 1), lower.tail = FALSE))
  if (binary) { cj <- colSums(m); ri <- rowSums(m); q <- (k - 1) * (k * sum(cj^2) - sum(m)^2) / (k * sum(m) - sum(ri^2)); want$t1 <- q }
  # the numbers are small with fewer than 25 blocks, or, with values of 0 and 1 only, fewer than 25 blocks that have a 1
  want[["*warn.rows"]] <- if ((if (binary) sum(rowSums(m) > 0) else n) < 25) 1 else 0
  want[names(.more)] <- .more
  case("RptFriedman", list(data = do.call(frame, cols)), want)
}
fr <- list(
  list(c(5.40, 5.85, 5.20, 5.55, 5.90, 5.45, 5.40, 5.45, 5.25, 5.85, 5.25, 5.65, 5.60, 5.05, 5.50, 5.45, 5.55, 5.45, 5.50, 5.65, 5.70, 6.30),
       c(5.50, 5.70, 5.60, 5.50, 5.85, 5.55, 5.40, 5.50, 5.15, 5.80, 5.20, 5.55, 5.35, 5.00, 5.50, 5.55, 5.55, 5.50, 5.45, 5.60, 5.65, 6.30),
       c(5.55, 5.75, 5.50, 5.40, 5.70, 5.60, 5.35, 5.35, 5.00, 5.70, 5.10, 5.45, 5.45, 4.95, 5.40, 5.50, 5.35, 5.55, 5.25, 5.40, 5.55, 6.25)),
  list(c(1, 1, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1), c(0, 1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1), c(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1)),
  list(round(rnorm(10, 5)), round(rnorm(10, 6)), round(rnorm(10, 7)), round(rnorm(10, 5))),
  list(c(2, 3, 4, NA, 6), c(3, 4, 4, 5, 7), c(1, 2, 2, 4, NA)),
  list(c(1, 0, 1, 1, 0, 1), c(1, 1, 1, 1, 0, 1), c(0, 0, 1, 0, 0, 1), c(1, 0, 1, 1, 1, 1)))
for (f in fr) friedman(f)
friedman(list(c(2, 2, 2, 2), c(2, 2, 2, 2), c(2, 2, 2, 2)), .more = list(refused = "TemplateOperationCancelledException: The test cannot be calculated when the observations within every block are the same."))
friedman(list(c(1, 5), c(1, 5), c(1, 5)), .more = list(refused = "TemplateOperationCancelledException: The test cannot be calculated when the observations within every block are the same."))
friedman(list(c(1, NA), c(2, 3), c(3, 4)), .more = list(refused = "TemplateOperationCancelledException: Too few blocks: at least two rows with a value in every column are needed."))
friedman(lapply(1:3, function(j) round(rnorm(30, 5 + j))))     # 30 blocks: the numbers are not small
fr_multiple <- function(cols, confidence = 0.95, sided = 2, .more = list()) {
  m <- do.call(cbind, cols); m <- m[complete.cases(m), , drop = FALSE]; n <- nrow(m); k <- ncol(m)
  ranks <- t(apply(m, 1, rank)); rj <- colSums(ranks); a1 <- sum(ranks^2); b1 <- sum(rj^2) / n
  conf <- level_of(confidence); alpha <- min(conf, 1 - conf); if (sided == 2) alpha <- alpha / 2
  dfq <- (n - 1) * (k - 1); tval <- qt(alpha, dfq, lower.tail = FALSE); tcriq <- sqrt(abs(2 * n * (a1 - b1) / dfq))
  want <- list(df = dfq, t = tval, sidedoutput = if (sided == 1) "one" else "two")
  row <- 0
  for (g in 1:(k - 1)) for (j in (g + 1):k) {
    row <- row + 1; stata <- rj[g] - rj[j]; p1 <- pt(-abs(stata / tcriq), dfq)
    want[[sprintf("*pair[%d].stata", row)]] <- stata; want[[sprintf("*pair[%d].tcrit", row)]] <- tcriq * tval
    want[[sprintf("*pair[%d].diff", row)]] <- if (abs(stata) > tcriq * tval) "significant" else "not significant"
    want[[sprintf("*pair[%d].p", row)]] <- if (sided == 1) p1 else 2 * p1
    if (sided == 1) want[[sprintf("*pair[%d].compare", row)]] <- if (rj[g] > rj[j]) ">" else "<"
  }
  want[["*pair.rows"]] <- row
  want[names(.more)] <- .more
  case("RptFrMultiple", list(data = do.call(frame, cols), confidence = confidence, sided = as.character(sided)), want)
}
for (f in fr[c(1, 3, 5)]) for (sided in 1:2) fr_multiple(f, 0.95, sided)
fr_multiple(fr[[1]], 0.99); fr_multiple(fr[[1]], 0.01)
fr_multiple(fr[[1]], 0); fr_multiple(fr[[1]], 1)   # a level of 0% or 100%: 95% is taken

# ---- Gini coefficient: of the positive values in order, G = sum of (2 j - n - 1) x_(j) over n times the sum; weights repeat a
# value; the coefficient of variation with n - 1; the bootstrap figures are checked apart
gini <- function(x, w = NULL, gamma = 0.95, boots = 200, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  keep <- !is.na(x) & (if (is.null(w)) TRUE else !is.na(w)); xx <- x[keep]; ww <- if (is.null(w)) rep(1, sum(keep)) else floor(w[keep])
  pos <- xx > 0; xx <- xx[pos]; ww <- ww[pos]; used <- length(xx)
  r <- sort(rep(xx, ww)); n <- length(r); j <- seq_len(n)
  g <- sum((2 * j - n - 1) * r) / (n * sum(r))
  want <- list(`*data[1].n` = n, `*data[1].gini` = g, `*data[1].cv` = sd(r) / mean(r), `*data[1].pc` = 100 * gamma, `*data[1].boots` = boots, `*data[1].gini-unbiased` = g * n / (n - 1),
               `*data[1].msg` = if (used != length(x)) paste0("(note ", length(x) - used, " other observation(s) not used)") else "")
  want[names(.more)] <- .more
  inputs <- list(gamma = level, data = frame(x)); if (!is.null(w)) inputs$weights <- frame(w); inputs$boots <- boots
  case("RptGini", inputs, want)
}
incomes <- c(12, 18, 25, 31, 40, 47, 55, 68, 90, 150)
gini(incomes); gini(incomes, boots = 1000, gamma = 0.9); gini(c(incomes, 0, NA, -5)); gini(incomes, c(3, 1, 2, 2, 1, 4, 1, 1, 2, 1)); gini(round(rexp(50, 0.01)) + 1)
gini(incomes, gamma = 0); gini(incomes, gamma = 1)   # a level of 0% or 100%: 95% is taken

# ---- diversity of classes: Simpson's index 1 - sum n (n - 1) / (N (N - 1)) and Shannon's H = (N ln N - sum n ln n) / N, with
# their large sample standard errors, the numbers of classes seen and estimated (Chao); the bootstrap figures are checked apart
diversity <- function(counts, gamma = 0.95, boots = 200, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  n <- counts[!is.na(counts) & counts > 0 & counts == floor(counts)]; N <- sum(n); s <- length(n); p <- n / N
  simpson <- 1 - sum(n * (n - 1)) / (N * (N - 1)); shannon <- (N * log(N) - sum(n * log(n))) / N
  # each variance is a sum of squares about a mean, which is 0 with equal counts and can be a little below 0 by rounding
  simvar <- max(0, (sum(p^3) - sum(p^2)^2) / (0.25 * N))
  simvars <- (4 * N * (N - 1) * (N - 2) * sum(p^3) + 2 * N * (N - 1) * sum(p^2) - 2 * N * (N - 1) * (2 * N - 3) * sum(p^2)^2) / (N * (N - 1))^2
  shanvar <- max(0, (sum(n * log(n)^2) - sum(n * log(n))^2 / N) / N^2); shanvars <- shanvar + (s - 1) / (2 * N^2)
  cit <- qnorm(1 - (1 - gamma) / 2)
  f1 <- max(1, sum(n == 1)); f2 <- max(1, sum(n == 2)); unseen <- f1^2 / (2 * f2); stotalvar <- f2 * ((f1 / f2)^4 / 4 + (f1 / f2)^3 + (f1 / f2)^2 / 2)
  cc <- exp(cit * sqrt(log(1 + stotalvar / unseen^2)))
  want <- list(`*var[1].n` = N, `*var[1].s` = s, `*var[1].stotal` = s + round(unseen), `*var[1].se-largeSample` = sqrt(stotalvar), `*var[1].pc` = 100 * gamma,
               `*var[1].from-largeSample` = as.character(round(s + unseen / cc)), `*var[1].to-largeSample` = as.character(round(s + unseen * cc)),
               `*var[1].simpson` = simpson, `*var[1].dom` = 1 - simpson, `*var[1].ds` = 1 / (1 - simpson), `*var[1].se-simpson-largeSample` = sqrt(simvar), `*var[1].ses-simpson` = sqrt(simvars),
               `*var[1].from-simpson-largeSample` = simpson - cit * sqrt(simvar), `*var[1].to-simpson-largeSample` = simpson + cit * sqrt(simvar),
               `*var[1].shannon` = shannon, `*var[1].se-shannon-largeSample` = sqrt(shanvar), `*var[1].ses-shannon` = sqrt(shanvars),
               `*var[1].from-shannon-largeSample` = shannon - cit * sqrt(shanvar), `*var[1].to-shannon-largeSample` = shannon + cit * sqrt(shanvar), `*var[1].boots` = boots,
               `*var[1].msg` = if (s != length(counts)) paste0("(note ", length(counts) - s, " other observations not used)") else "")
  want[names(.more)] <- .more
  case("RptDiversity", list(gamma = level, data = frame(counts), boots = boots), want)
}
diversity(c(30, 12, 8, 5, 3, 2, 1, 1, 1)); diversity(c(30, 12, 8, 5, 3, 2, 1, 1, 1), 0.99, 500); diversity(c(10, 10, 10, 10)); diversity(c(100, 1, 1, 1, 2, 2, 5, NA, 0, 3.5))
diversity(rpois(40, 6) + 1)
diversity(c(30, 12, 8, 5, 3, 2, 1, 1, 1), 0); diversity(c(30, 12, 8, 5, 3, 2, 1, 1, 1), 1)   # a level of 0% or 100%: 95% is taken

writeLines(cases, args[1]); writeLines(expected, args[2])
cat(length(cases), "cases,", length(expected), "figures\n")
