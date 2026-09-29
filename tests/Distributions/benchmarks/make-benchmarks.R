# The Distributions menu: the cases that the calculator is given, and what is expected of its boxes.
#
# A case (cases.txt): the distribution, a name, what is done (p: the probabilities of a value; lp, up, 2p: the value that has
# a lower tail, upper tail or two sided probability), and the texts of the three boxes that are filled ("-" for a box that is
# left empty): the value or the probability, the first parameter and the second.
# What is expected (expected.txt): "distribution|name|box", the figure, and the room that the figure is given if it is not
# the room of the box (see the README).  "error" is a refusal; "below 1e-15" is a probability that is shown as "< 1E-15".
#
# Where the figures are from:
#  - the normal, Student's t, F, chi-square, binomial and Poisson distributions: the functions of R;
#  - the F value of an upper tail probability: the root of R's probability, found in its logarithm;
#  - the non-central t and the studentized range: their definitions, by integration (below).  R's own functions are not the
#    reference for these two: in the far tails, and for the studentized range with few degrees of freedom, they are off by
#    more than the calculator shows (P(Q > 20) with 2 degrees of freedom and 2 samples is 0.0049628, which is twice the
#    upper tail of Student's t at 20 / root 2; the function of R gives 0.0046680);
#  - Kendall's S: the chances of the numbers of inversions of n things, from those of n - 1 things;
#  - the Hotelling-Pabst T of Spearman's rho: the counts of the orders of n ranks by the sum of the squares of the
#    differences, built up a place at a time over the sets of ranks that are used.
#
# usage: Rscript --vanilla make-benchmarks.R     (in this folder; it takes about three quarters of an hour, most of it for
#        the studentized range)
cases <- character(0); expected <- character(0); count <- 0
txt <- function(x) if (is.character(x)) x else format(x, digits = 17, scientific = abs(x) < 1e-4 && x != 0 || abs(x) >= 1e15, trim = TRUE)
fig <- function(x) if (is.character(x)) x else format(x, digits = 17, scientific = TRUE)
# a case; what is expected is given as box = figure, or box = list(figure, room)
case <- function(kind, action, x, a, b, ...) {
  count <<- count + 1
  name <- sprintf("%s%04d", action, count)
  cases <<- c(cases, paste(kind, name, action, txt(x), txt(a), txt(b), sep = "\t"))
  want <- list(...)
  for (box in names(want)) {
    w <- want[[box]]
    line <- paste0(kind, "|", name, "|", box, "\t", fig(w[[1]]))
    if (length(w) > 1) line <- paste0(line, "\t", fig(w[[2]]))
    expected <<- c(expected, line)
  }
}
refused <- "error"
small <- c(1e-300, 1e-100, 1e-30, 1e-16, 1e-12, 1e-9, 1e-6, 0.0005, 0.01, 0.025, 0.2, 0.5)

# ---- Gauss-Legendre quadrature in panels
gauss_legendre <- function(n) {
  i <- 1:(n - 1); b <- i / sqrt(4 * i^2 - 1)
  J <- matrix(0, n, n); J[cbind(i, i + 1)] <- b; J[cbind(i + 1, i)] <- b
  e <- eigen(J, symmetric = TRUE)
  list(x = rev(e$values), w = rev(2 * e$vectors[1, ]^2))
}
GL <- gauss_legendre(16)
panels <- function(cuts) {
  a <- head(cuts, -1); b <- tail(cuts, -1)
  list(x = as.vector(outer(GL$x, (b - a) / 2) + rep((a + b) / 2, each = length(GL$x))), w = as.vector(outer(GL$w, (b - a) / 2)))
}
# ---- the studentized range of k means with df degrees of freedom: Q = (the range of k standard normal values) / u, where
# u = sqrt(V / df) and V is chi-square with df degrees of freedom.  The chance that the range of k normal values is w or less
# is k times the mean over the greatest value z of (Phi(z) - Phi(z - w))^(k-1); the chance that it is above w has
# Phi(z)^(k-1) less that power in its place.  The mean over u is taken in the logarithm of u.
q_tails <- function(q, k, df, u_panels = 80) {
  if (q <= 0) return(c(lower = 0, upper = 1))
  lo <- max(sqrt(qchisq(1e-25, df) / df), 1e-13); hi <- sqrt(qchisq(1e-25, df, lower.tail = FALSE) / df)
  pu <- panels(seq(log(lo), log(hi), length.out = u_panels + 1))
  u <- exp(pu$x)
  fu <- dchisq(u^2 * df, df) * 2 * u * df * u * pu$w
  w <- q * u
  lower <- upper <- numeric(length(u))
  for (j in seq_along(u)) {
    top <- 9 + min(w[j], 45)
    pz <- panels(seq(-9, top, length.out = ceiling(top + 9) * 2 + 1))
    inside <- pnorm(pz$x) - pnorm(pz$x - w[j])
    lower[j] <- k * sum(pz$w * dnorm(pz$x) * inside^(k - 1))
    upper[j] <- k * sum(pz$w * dnorm(pz$x) * (pnorm(pz$x)^(k - 1) - inside^(k - 1)))
  }
  c(lower = sum(fu * lower), upper = sum(fu * upper))
}
q_value <- function(p_upper, k, df) {
  f <- function(lq) log(q_tails(exp(lq), k, df)[["upper"]]) - log(p_upper)
  start <- log(sqrt(2) * qt(p_upper / 2, df, lower.tail = FALSE))          # the value for two means: the search widens from it
  exp(uniroot(f, start + c(0, 0.5), extendInt = "downX", tol = 1e-13)$root)
}
# ---- the non-central t: T = (Z + delta) / u; P(T <= t) is the mean over u of the normal probability below t u - delta.  The
# range of u is cut where its density is below 1e-300: a tail of 1e-74 comes from values of u far from 1.
nct <- function(t, df, delta, upper = FALSE, pieces = 400) {
  lo <- max(sqrt(qchisq(1e-300, df) / df), 1e-12); hi <- sqrt(qchisq(1e-300, df, lower.tail = FALSE) / df)
  cuts <- exp(seq(log(lo), log(hi), length.out = pieces + 1))
  f <- function(u) pnorm(t * u - delta, lower.tail = !upper) * dchisq(u^2 * df, df) * 2 * u * df
  sum(sapply(seq_len(pieces), function(i) integrate(f, cuts[i], cuts[i + 1], rel.tol = 1e-12, abs.tol = 0, stop.on.error = FALSE)$value))
}
nct_value <- function(p, df, delta, upper) {
  f <- function(t) log(nct(t, df, delta, upper)) - log(p)
  near <- delta + qt(p, df, lower.tail = !upper)                             # a start only: the search widens from it
  width <- max(1e-3, 0.05 * abs(near))
  uniroot(f, near + c(-width, width), extendInt = "yes", tol = 1e-13)$root
}
# ---- Kendall: the chances of the numbers of inversions of n things, from those of n - 1 things; S is the pairs less twice
# the inversions, and the upper tail is the chance of S or more
kendall <- function(n) {
  w <- 1
  for (m in 2:n) { longer <- numeric(length(w) + m - 1); for (j in 0:(m - 1)) longer[(1:length(w)) + j] <- longer[(1:length(w)) + j] + w / m; w <- longer }
  pairs <- n * (n - 1) / 2
  list(S = pairs - 2 * (0:pairs), chance = w, pairs = pairs)
}
kendall_upper <- function(k, s) sum(k$chance[k$S >= s])
# ---- Spearman: the counts of the orders of n ranks by T, the sum of the squares of the differences of the ranks from
# 1 to n, built up a place at a time: for every set of ranks that is used, the counts by the sum so far
spearman <- function(n) {
  most <- n * (n^2 - 1) / 3
  d <- vector("list", 2^n); d[[1]] <- c(1, rep(0, most))
  bits <- 2^(0:(n - 1))
  for (mask in 0:(2^n - 2)) {
    cur <- d[[mask + 1]]
    used <- bitwAnd(mask, bits) > 0
    place <- sum(used) + 1
    for (v in which(!used)) {
      shift <- (place - v)^2
      if (shift > most) next
      add <- c(rep(0, shift), cur[seq_len(most + 1 - shift)])
      to <- mask + bits[v] + 1
      d[[to]] <- if (is.null(d[[to]])) add else d[[to]] + add
    }
    d[mask + 1] <- list(NULL)
  }
  counts <- d[[2^n]]
  list(T = 0:most, chance = counts / sum(counts), most = most)
}
spearman_upper <- function(s, t) sum(s$chance[s$T <= t])

# ---- normal
for (z in c(-38, -20, -9, -6.5, -3, -1.96, -0.5, 0, 0.001, 1, 1.959964, 2.5758, 5, 8.3, 12, 25, 37))
  case("Z", "p", z, "-", "-", txtLp = pnorm(z), txtUp = pnorm(z, lower.tail = FALSE), txt2p = 2 * pnorm(-abs(z)))
for (p in c(small, 0.8, 0.999, 1 - 1e-9)) {
  case("Z", "lp", p, "-", "-", txtPdf = qnorm(p))
  case("Z", "up", p, "-", "-", txtPdf = qnorm(p, lower.tail = FALSE))
  case("Z", "2p", p, "-", "-", txtPdf = qnorm(p / 2, lower.tail = FALSE))
}
case("Z", "p", "1e400", "-", "-", txtLp = refused, txtUp = refused, txt2p = refused)
case("Z", "p", "abc", "-", "-", txtLp = refused, txtUp = refused, txt2p = refused)
# ---- Student's t
for (df in c(1, 2, 3.5, 10, 30, 200, 1e5))
  for (t in c(-300, -60, -12, -4.2, -1, 0, 0.3, 2.228, 7, 25, 300))
    case("T", "p", t, df, "-", txtLp = pt(t, df), txtUp = pt(t, df, lower.tail = FALSE), txt2p = 2 * pt(-abs(t), df))
for (df in c(1, 4, 25, 1000))
  for (p in c(small[-1], 0.9, 0.9999)) {
    case("T", "lp", p, df, "-", txtPdf = qt(p, df))
    case("T", "up", p, df, "-", txtPdf = qt(p, df, lower.tail = FALSE))
    case("T", "2p", p, df, "-", txtPdf = qt(p / 2, df, lower.tail = FALSE))
  }
case("T", "p", 2, 0, "-", txtLp = refused, txtUp = refused, txt2p = refused)
case("T", "p", 2, -3, "-", txtLp = refused, txtUp = refused, txt2p = refused)
case("T", "p", "abc", 5, "-", txtLp = refused, txtUp = refused, txt2p = refused)
# ---- F
for (d1 in c(1, 2, 7, 40, 3000))
  for (d2 in c(1, 3, 12, 500, 1e6))
    for (f in c(0, 0.01, 0.7, 1, 3.3, 20, 900)) case("F", "p", f, d1, d2, txtUp = pf(f, d1, d2, lower.tail = FALSE))
f_value <- function(p, d1, d2) {
  g <- if (p <= 0.5) function(lf) pf(exp(lf), d1, d2, lower.tail = FALSE, log.p = TRUE) - log(p) else function(lf) pf(exp(lf), d1, d2, log.p = TRUE) - log1p(-p)
  exp(uniroot(g, log(qf(p, d1, d2, lower.tail = FALSE)) + c(-0.01, 0.01), extendInt = "yes", tol = 1e-15)$root)
}
for (d1 in c(1, 5, 60)) for (d2 in c(2, 15, 800)) for (p in c(small[-1], 0.9, 0.9999)) case("F", "up", p, d1, d2, txtPdf = f_value(p, d1, d2))
case("F", "p", -1, 3, 4, txtUp = refused)
# ---- chi-square
for (df in c(1, 2, 5, 37, 400, 25000, 1e6))
  for (x in c(0, 0.004, 1, 3.841459, 30, 250, 5000, df, df + 12 * sqrt(2 * df))) case("ChiSq", "p", x, df, "-", txtUp = pchisq(x, df, lower.tail = FALSE))
for (df in c(1, 3, 50, 2000)) for (p in c(small[-1], 0.9, 0.9999)) case("ChiSq", "up", p, df, "-", txtPdf = qchisq(p, df, lower.tail = FALSE))
case("ChiSq", "p", -1, 3, "-", txtUp = refused)
# ---- the studentized range: the second box is the number of samples.  With 8 to 20000 degrees of freedom that are twice the
# number of means or more the program has a series, which is within 0.0000005 of the probability with up to 30 means and
# within 0.000002 with more; with a million degrees of freedom it takes them as without end, which is within 0.000005; for
# the rest, and with two means, it has what the box
# shows, 7 decimal places.  A value has the room that the room of its probability gives it: that room divided by the slope
# of the probability at the value.
q_room <- function(k, df) if (k == 2) 6e-8 else if (df >= 1e6) 5e-6 else if (df >= 8 && df >= 2 * k && df <= 20000) (if (k > 30) 2e-6 else 5e-7) else 6e-8
for (df in c(2, 5, 20, 50, 120, 800, 5000, 25000, 25001, 1e5, 1e6))
  for (k in c(2, 3, 8, 30, 100))
    for (q in c(0.2, 1.5, 3.5, 5, 9, 14, 20, 40)) {
      t <- q_tails(q, k, df)
      case("Q", "p", q, df, k, txtLp = list(t[["lower"]], q_room(k, df)), txtUp = list(t[["upper"]], q_room(k, df)))
    }
for (df in c(3, 12, 60, 1000, 30000)) for (k in c(2, 4, 12, 50)) for (p in c(0.001, 0.01, 0.05, 0.5, 0.9)) {
  q <- q_value(p, k, df)
  slope <- (q_tails(q * (1 - 1e-4), k, df)[["upper"]] - q_tails(q * (1 + 1e-4), k, df)[["upper"]]) / (2e-4 * q)
  case("Q", "up", p, df, k, txtPdf = list(q, 6e-8 + (if (q_room(k, df) > 6e-8) q_room(k, df) else 1e-9) / slope))
}
# ---- binomial: the value box is the probability of a success, then the trials and the successes
for (n in c(1, 5, 40, 1000, 50000, 2000000))
  for (p in c(0, 1e-7, 0.03, 0.5, 0.97, 1))
    for (r in unique(round(c(0, 1, n * p, n * p + 3 * sqrt(n * p * (1 - p)), n / 2, n - 1, n))))
      if (r >= 0 && r <= n) case("Binomial", "p", p, n, r, txtLp = dbinom(r, n, p), txtUp = pbinom(r - 1, n, p, lower.tail = FALSE), txt2p = pbinom(r, n, p))
for (odd in list(c(0.5, 10, -1), c(0.5, 10, 2.5), c(0.5, 0, 0), c(0.5, 10, 11), c(1.5, 10, 3), c(-0.1, 10, 3)))
  case("Binomial", "p", odd[1], odd[2], odd[3], txtLp = refused, txtUp = refused, txt2p = refused)
# ---- Poisson: the first box is the number of events, the second the mean
for (m in c(0, 1e-8, 0.3, 4, 60, 2500, 1e6))
  for (n in unique(round(c(0, 1, m, m + 4 * sqrt(m), m - 3 * sqrt(m), 2 * m + 5))))
    if (n >= 0) case("Poisson", "p", "-", n, m, txtLp = dpois(n, m), txtUp = ppois(n - 1, m, lower.tail = FALSE), txt2p = ppois(n, m))
for (odd in list(c(-1, 2), c(2.5, 2), c(3, -1)))
  case("Poisson", "p", "-", odd[1], odd[2], txtLp = refused, txtUp = refused, txt2p = refused)
# the mean with which n events or more, or n or fewer, have a probability
for (n in c(0, 1, 7, 150, 20000))
  for (p in c(1e-12, 1e-6, 0.025, 0.5, 0.975)) {
    if (n > 0) case("Poisson", "up", p, n, "-", txtDf2 = qgamma(p, n))
    case("Poisson", "2p", p, n, "-", txtDf2 = qgamma(p, n + 1, lower.tail = FALSE))
  }
# ---- Kendall: the sample size, then S.  Up to 50 observations the calculator sums the orders; above 50 it has a series,
# which is within 0.0000005 of the probability
for (n in c(2, 3, 5, 8, 12, 25, 50, 51, 60, 100, 200)) {
  k <- kendall(n); pairs <- k$pairs
  tail <- cumsum(k$chance)                                                # the chance of S or more, S from the most down
  at <- sapply(c(0.4, 0.1, 0.025, 0.001, 1e-6, 1e-9), function(p) { i <- which(tail <= p); if (length(i)) k$S[max(i)] else pairs })
  for (s in unique(c(pairs, pairs - 2, at, if (pairs %% 2 == 0) 0 else 1, -at[2], -pairs))) {
    up <- kendall_upper(k, s)
    case("Kendall", "p", "-", n, s, txtUp = if (n > 50) list(up, 5e-7) else if (up < 1e-15) "below 1e-15" else up, txtPdf = s / pairs)
  }
}
# tau entered, from which S is worked out
case("Kendall", "p", 23 / 45, 10, "-", txtDf2 = 23, txtUp = kendall_upper(kendall(10), 23))
case("Kendall", "p", -0.2, 12, "-", txtDf2 = round(-0.2 * 66), txtUp = kendall_upper(kendall(12), round(-0.2 * 66)))
# samples of tens of thousands: the normal distribution with S less 1, which the series is within 1e-4 of
sd_s <- function(n) sqrt(n * (n - 1) * (2 * n + 5) / 18)
case("Kendall", "p", "-", 50000, 3000000, txtUp = list(pnorm((3000000 - 1) / sd_s(50000), lower.tail = FALSE), 1e-4), txtPdf = 3000000 / (50000 * 49999 / 2))
case("Kendall", "p", 0.001, 60000, "-", txtDf2 = round(0.001 * 60000 * 59999 / 2), txtUp = list(pnorm((round(0.001 * 60000 * 59999 / 2) - 1) / sd_s(60000), lower.tail = FALSE), 1e-4))
case("Kendall", "p", 0.5, 50000, "-", txtDf2 = 0.5 * 50000 * 49999 / 2, txtUp = "below 1e-15")
for (odd in list(c("-", 1, 0), c("-", 10, 46), c("-", 10, -46), c("-", 10, 2.5))) case("Kendall", "p", odd[1], odd[2], odd[3], txtUp = refused)
for (odd in list(c(1.2, 10, "-"), c(-1.01, 10, "-"))) case("Kendall", "p", odd[1], odd[2], odd[3], txtUp = refused, txtDf2 = refused)
# the value of an upper tail P: the greatest S that has the chance P or more of being reached or passed
for (n in c(5, 10, 30, 50)) {
  k <- kendall(n); tail <- cumsum(k$chance)
  for (p in c(0.1, 0.05, 0.025, 0.001)) {
    ok <- which(tail >= p - 1e-14 & k$S >= 0)
    if (!length(ok)) next
    s <- max(k$S[ok])
    case("Kendall", "up", p, n, "-", txtDf2 = s, txtPdf = s / k$pairs, txtUp = kendall_upper(k, s))
  }
}
# ---- Spearman: the sample size, then the Hotelling-Pabst T.  Up to 10 pairs the calculator goes through the orders; above
# 10 it has a series, which the help gives as within 0.0004 with 11 pairs and within 0.00005 from 15
for (n in c(4:12, 15)) {
  s <- spearman(n); most <- s$most
  room <- if (n <= 10) NULL else if (n < 15) 0.0004 else 0.00005
  for (t in unique(c(0, 1, 2, 3, round(most / 8) * 2, round(most / 8) * 2 + 1, round(most / 4) * 2, most / 2 - (most / 2) %% 2, most - 2, most - 1, most))) {
    up <- spearman_upper(s, t)
    case("Rho", "p", "-", n, t, txtUp = if (is.null(room)) up else list(up, room), txtPdf = 1 - 6 * t / (n * (n^2 - 1)))
  }
}
# rho entered, from which T is worked out
case("Rho", "p", 0.85, 5, "-", txtDf2 = 3, txtUp = spearman_upper(spearman(5), 3))
case("Rho", "p", -0.5, 5, "-", txtDf2 = 30, txtUp = spearman_upper(spearman(5), 30))
case("Rho", "p", -1, 6, "-", txtDf2 = 70, txtUp = 1)
case("Rho", "p", 0.5, 1500, "-", txtDf2 = 0.5 * 1500 * (1500^2 - 1) / 6, txtUp = "below 1e-15")
for (odd in list(c("-", 5, 41), c("-", 5, -2), c("-", 3, 2), c("-", 5, 2.5), c("-", 2000, 10))) case("Rho", "p", odd[1], odd[2], odd[3], txtUp = refused)
for (odd in list(c(-1.5, 5, "-"), c(1.1, 5, "-"), c(0.5, 2000, "-"))) case("Rho", "p", odd[1], odd[2], odd[3], txtUp = refused, txtDf2 = refused)
# the value of an upper tail P: the greatest T that has no more than the chance P of not being passed
for (n in c(5, 8, 10)) {
  s <- spearman(n); lower <- cumsum(s$chance)
  for (p in c(0.1, 0.05, 0.025, 0.001)) {
    ok <- which(lower <= p + 1e-14 & s$T %% 2 == 0)
    if (!length(ok)) { case("Rho", "up", p, n, "-", txtUp = refused); next }
    t <- max(s$T[ok])
    case("Rho", "up", p, n, "-", txtDf2 = t, txtPdf = 1 - 6 * t / (n * (n^2 - 1)), txtUp = spearman_upper(s, t))
  }
}
# ---- non-central t: the degrees of freedom, then the non-centrality
for (df in c(2, 9, 40, 600))
  for (delta in c(-3, 0, 0.5, 4, 25))
    for (t in c(-20, -2, 0, 1.3, delta, delta + 6, 60))
      case("NonCentralT", "p", t, df, delta, txtLp = nct(t, df, delta), txtUp = nct(t, df, delta, upper = TRUE))
for (df in c(3, 20, 300)) for (delta in c(-2, 0, 1.5, 12)) for (p in c(1e-16, 1e-9, 0.001, 0.025, 0.5, 0.95)) {
  case("NonCentralT", "lp", p, df, delta, txtPdf = if (p == 0.5 && delta == 0) 0 else nct_value(p, df, delta, FALSE))
  case("NonCentralT", "up", p, df, delta, txtPdf = if (p == 0.5 && delta == 0) 0 else nct_value(p, df, delta, TRUE))
}
for (odd in list(c(2, 2.5, 1), c(2, 0, 1))) case("NonCentralT", "p", odd[1], odd[2], odd[3], txtLp = refused, txtUp = refused)

writeLines(cases, "cases.txt"); writeLines(expected, "expected.txt")
cat(length(cases), "cases,", length(expected), "figures\n")
