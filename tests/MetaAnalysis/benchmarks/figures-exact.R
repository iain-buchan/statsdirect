# The exact and score figures of the Meta-analysis menu, from the distributions themselves to a tight tolerance, under the keys of the
# figures of the program: for each study the conditional (Fisher) limits of the odds ratio, the score limits of the relative risk
# (Koopman) and of the risk difference (Miettinen and Nurminen), the limits of g from the non-central t distribution, the exact limits of
# the rate ratio; and for the studies together the conditional maximum likelihood estimates with their exact and mid-P limits.
# usage: Rscript figures-exact.R <cases> <output>
args <- commandArgs(trailingOnly = TRUE)
lines <- readLines(args[1])
out <- file(args[2], "w")
put <- function(key, value) {
  for (i in seq_along(value)) {
    v <- value[i]
    text <- if (is.na(v)) "NA" else if (is.infinite(v)) (if (v > 0) "Inf" else "-Inf") else format(v, digits = 12)
    cat(key[i], "\t", text, "\n", sep = "", file = out)
  }
}
root <- function(f, lo, hi) uniroot(f, c(lo, hi), tol = 1e-14, maxiter = 10000)$root

# ---- the distribution of a total, given as the logarithms of its terms at a ratio of 1 over its support
hyper_terms <- function(a, b, c, d) {
  n1 <- a + c; n0 <- b + d; m1 <- a + b
  s <- max(0, m1 - n0):min(m1, n1)
  list(support = s, logp = lchoose(n1, s) + lchoose(n0, m1 - s))
}
binom_terms <- function(a, b, t1, t2) {
  s <- 0:(a + b)
  list(support = s, logp = lchoose(a + b, s) + s * log(t1) + (a + b - s) * log(t2))
}
convolve_terms <- function(list_of_terms) {
  low <- 0; p <- 1
  for (t in list_of_terms) {
    q <- exp(t$logp - max(t$logp))
    full <- numeric(length(p) + length(q) - 1)
    for (j in seq_along(q)) { at <- j - 1 + seq_along(p); full[at] <- full[at] + p * q[j] }
    p <- full / max(full)
    low <- low + t$support[1]
  }
  list(support = low + seq_along(p) - 1, logp = log(p))
}
exact <- function(terms, observed, level) {
  s <- terms$support; lp <- terms$logp
  at <- which(s == observed)
  prob <- function(logpsi) { v <- lp + (s - s[1]) * logpsi; v <- exp(v - max(v)); v / sum(v) }
  upper <- function(logpsi, half) { p <- prob(logpsi); sum(p[at:length(p)]) - half * p[at] }
  lower <- function(logpsi, half) { p <- prob(logpsi); sum(p[1:at]) - half * p[at] }
  alpha <- (1 - level) / 2
  r <- list()
  r$estimate <- if (at == 1) 0 else if (at == length(s)) Inf else exp(root(function(l) sum(prob(l) * s) - observed, -40, 40))
  r$llf <- if (at == 1) 0 else exp(root(function(l) upper(l, 0) - alpha, -40, 40))
  r$ulf <- if (at == length(s)) Inf else exp(root(function(l) lower(l, 0) - alpha, -40, 40))
  r$llm <- if (at == 1) 0 else exp(root(function(l) upper(l, 0.5) - alpha, -40, 40))
  r$ulm <- if (at == length(s)) Inf else exp(root(function(l) lower(l, 0.5) - alpha, -40, 40))
  p <- prob(0)
  up <- sum(p[at:length(p)]); lo <- sum(p[1:at])
  r$p1f <- min(up, lo)
  r$p2f <- min(1, sum(p[p <= p[at] * (1 + 1e-7)]))
  upm <- up - p[at] / 2
  r$p1m <- min(upm, 1 - upm)
  r$p2m <- min(1, 2 * r$p1m)
  r
}
# ---- score limits
koopman <- function(x1, n1, x2, n2, z) {
  # the ratio p1 / p2; the statistic with the estimates of p1 and p2 that are in the ratio theta
  stat <- function(theta) {
    N <- n1 + n2
    bq <- theta * (n1 + x2) + x1 + n2
    p1 <- (bq - sqrt(bq^2 - 4 * theta * N * (x1 + x2))) / (2 * N)
    p2 <- p1 / theta
    v <- p1 * (1 - p1) / n1 + theta^2 * p2 * (1 - p2) / n2
    (x1 / n1 - theta * x2 / n2)^2 / v
  }
  est <- (x1 / n1) / (x2 / n2)
  f <- function(l) stat(exp(l)) - z^2
  lo <- if (x1 == 0) 0 else exp(root(f, -60, if (is.finite(est) && est > 0) log(est) else 60))
  hi <- if (x2 == 0) Inf else exp(root(f, if (is.finite(est) && est > 0) log(est) else -60, 60))
  c(lo, hi)
}
miettinen <- function(x1, n1, x2, n2, z) {
  N <- n1 + n2
  est <- x1 / n1 - x2 / n2
  stat <- function(theta) {
    lo <- max(0, -theta); hi <- min(1, 1 - theta)
    ll <- function(p2) {
      p1 <- p2 + theta
      t <- function(x, p) if (x == 0) 0 else x * log(p)
      -(t(x1, p1) + t(n1 - x1, 1 - p1) + t(x2, p2) + t(n2 - x2, 1 - p2))
    }
    p2 <- optimize(ll, c(lo, hi), tol = 1e-13)$minimum
    p1 <- p2 + theta
    v <- (p1 * (1 - p1) / n1 + p2 * (1 - p2) / n2) * N / (N - 1)
    (est - theta)^2 / v - z^2
  }
  eps <- 1e-9
  c(root(stat, -1 + eps, est), root(stat, est, 1 - eps))
}

at <- 1
while (at <= length(lines)) {
  head <- strsplit(lines[at], "\t")[[1]]
  if (head[1] != "case") { at <- at + 1; next }
  name <- head[2]; kind <- head[3]; k <- as.integer(head[4]); level <- as.numeric(head[5])
  x <- matrix(as.numeric(unlist(strsplit(lines[at + seq_len(k)], "\t"))), k, byrow = TRUE)
  at <- at + k + 1
  z <- qnorm(1 - (1 - level) / 2)
  sn <- "x1.tacc.d0"
  if (kind == "bin") {
    a <- x[, 1]; b <- x[, 3]; c <- x[, 2] - x[, 1]; d <- x[, 4] - x[, 3]
    if (max(x[, 2] + x[, 4]) > 3000) next
    for (i in seq_len(k)) {
      if (x[i, 2] == 0 || x[i, 4] == 0) next
      if (!((a[i] == 0 && b[i] == 0) || (c[i] == 0 && d[i] == 0))) {
        e <- exact(hyper_terms(a[i], b[i], c[i], d[i]), a[i], level)
        put(paste0(name, "|", sn, "|or|or.", i, c("|lci", "|uci")), c(e$llf, e$ulf))
        r <- try(koopman(a[i], x[i, 2], b[i], x[i, 4], z), silent = TRUE)
        if (!inherits(r, "try-error")) put(paste0(name, "|", sn, "|rr|risks.", i, c("|lci", "|uci")), r)
      }
      r <- try(miettinen(a[i], x[i, 2], b[i], x[i, 4], z), silent = TRUE)
      if (!inherits(r, "try-error")) put(paste0(name, "|", sn, "|rd|differences.", i, c("|lci", "|uci")), r)
    }
    use <- which(!((a == 0 & b == 0) | (c == 0 & d == 0)) & x[, 2] > 0 & x[, 4] > 0)
    inf <- use[(a[use] * d[use] != 0) | (b[use] * c[use] != 0)]
    if (length(inf) > 0) {
      e <- exact(convolve_terms(lapply(inf, function(i) hyper_terms(a[i], b[i], c[i], d[i]))), sum(a[inf]), level)
      put(paste0(name, "|", sn, "|or|cml.1|", c("eor", "llf", "ulf", "llm", "ulm", "p1f", "p2f", "p1m", "p2m")), c(e$estimate, e$llf, e$ulf, e$llm, e$ulm, e$p1f, e$p2f, e$p1m, e$p2m))
    }
  }
  if (kind == "rate") {
    a <- x[, 1]; t1 <- x[, 2]; b <- x[, 3]; t2 <- x[, 4]
    for (i in seq_len(k)) {
      # the limits are of the events as they are: without an event in one of the groups one of them is 0 or infinity
      if (a[i] + b[i] <= 0 || t1[i] <= 0 || t2[i] <= 0) next
      ci <- binom.test(a[i], a[i] + b[i], conf.level = level)$conf.int
      put(paste0(name, "|", sn, "|irr|ir.", i, c("|lci", "|uci")), ci / (1 - ci) * t2[i] / t1[i])
    }
    inf <- which(a + b > 0 & t1 > 0 & t2 > 0)
    if (length(inf) > 0 && sum(a[inf] + b[inf]) < 20000) {
      e <- exact(convolve_terms(lapply(inf, function(i) binom_terms(a[i], b[i], t1[i], t2[i]))), sum(a[inf]), level)
      put(paste0(name, "|", sn, "|irr|poolok.1|", c("eor", "llf", "ulf", "llm", "ulm", "p1f", "p2f", "p1m", "p2m")), c(e$estimate, e$llf, e$ulf, e$llm, e$ulm, e$p1f, e$p2f, e$p1m, e$p2m))
    }
  }
  if (kind == "cont") {
    for (i in seq_len(k)) {
      ne <- x[i, 1]; nc <- x[i, 4]
      if (x[i, 3] <= 0 || x[i, 6] <= 0) next
      s <- sqrt(((ne - 1) * x[i, 3]^2 + (nc - 1) * x[i, 6]^2) / (ne + nc - 2))
      g <- (x[i, 2] - x[i, 5]) / s
      df <- ne + nc - 2
      scale <- sqrt(ne * nc / (ne + nc))
      t <- g * scale
      f <- function(ncp, p) suppressWarnings(pt(t, df, ncp)) - p
      lo <- try(root(function(v) f(v, 1 - (1 - level) / 2), t - 40 - 10 * abs(t), t), silent = TRUE)
      hi <- try(root(function(v) f(v, (1 - level) / 2), t, t + 40 + 10 * abs(t)), silent = TRUE)
      m <- df
      j <- exp(lgamma(m / 2) - lgamma((m - 1) / 2)) / sqrt(m / 2)
      put(paste0(name, "|", sn, "|smd|exact.", i, c("|g", "|gj")), c(g, j))
      if (!inherits(lo, "try-error") && !inherits(hi, "try-error") && abs(t) < 35) put(paste0(name, "|", sn, "|smd|exact.", i, c("|lci", "|uci")), c(lo, hi) / scale)
    }
  }
}
close(out)
cat("figures written to", args[2], "\n")
