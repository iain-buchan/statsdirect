# The figures of R (packages meta and stats) for the sets of studies with a binary outcome, under the keys of the figures of the program
# (Figures.cs), one "key<TAB>value" to a line.  A figure that R does not give, or gives in another sense, is left out.
# usage: Rscript figures-bin.R <cases> <output>
.libPaths(c(Sys.getenv("R_LIBS_USER"), .libPaths()))
suppressPackageStartupMessages(library(meta))
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
settings <- list(x1.tacc.d0 = list(exact = TRUE, incr = "TACC", delay = FALSE), x0.tacc.d0 = list(exact = FALSE, incr = "TACC", delay = FALSE),
                 x0.cc5.d0 = list(exact = FALSE, incr = 0.5, delay = FALSE), x0.cc5.d1 = list(exact = FALSE, incr = 0.5, delay = TRUE))
# The bias indicator of Harbord and Egger for the relative risk, from the score of each study and its variance: meta makes it with
# another variance, which is the same only if the two groups of a study are of one size
harbord_rr <- function(key, a, b, c, d) {
  n <- a + b + c + d
  z <- (a * n - (a + b) * (a + c)) / (c + d)
  v <- (b + d) * (a + c) * (a + b) / (n * (c + d))
  ok <- is.finite(v) & v > 0 & (a + c) > 0 & (b + d) > 0 & !((a == 0 & b == 0) | (c == 0 & d == 0))
  if (sum(ok) < 4) return(invisible())
  x <- sqrt(v[ok]); y <- z[ok] / x
  if (diff(range(x)) <= 1e-12 * max(x)) return(invisible())
  f <- summary(lm(y ~ x))$coefficients
  if (is.finite(f[1, 4])) put(paste0(key, c("|harbord.1|a", "|harbord.1|p")), c(f[1, 1], f[1, 4]))
}

# I-squared and its limits: by the test of Higgins and Thompson (as meta gives them), and from the non-central chi-square distribution
isq_exact <- function(q, df, level) {
  if (df < 1 || !is.finite(q)) return(c(NA, NA))
  limit <- function(prob) {
    if (pchisq(q, df) <= prob) return(0)
    f <- function(l) pchisq(q, df, ncp = l) - prob
    hi <- max(1, q) + 1000
    while (f(hi) > 0) hi <- hi * 2
    uniroot(f, c(0, hi), tol = 1e-12)$root
  }
  l <- c(limit(1 - (1 - level) / 2), limit((1 - level) / 2))
  100 * l / (df + l)
}
heterogeneity <- function(key, m, s, level) {
  k <- m$k
  put(paste0(key, c("|qc", "|isq")), c(if (k > 1) m$Q else 0, if (k > 1) 100 * m$I2 else NA))
  if (is.finite(m$tau2)) put(paste0(key, "|tausq"), m$tau2)
  if (k > 1) put(paste0(key, "|xp_cochran"), m$pval.Q)
  if (s$exact) { if (k > 1) put(paste0(key, c("|llisq", "|ulisq")), isq_exact(m$Q, k - 1, level)) }
  else if (k > 2) put(paste0(key, c("|llisq", "|ulisq")), 100 * c(m$lower.I2, m$upper.I2))
}
bias <- function(key, m, harbord) {
  if (sum(is.finite(m$TE) & is.finite(m$seTE)) < 4) return(invisible())
  e <- try(suppressWarnings(metabias(m, method.bias = "Egger", k.min = 4)), silent = TRUE)
  if (!inherits(e, "try-error") && is.finite(e$pval)) put(paste0(key, c("|egger.1|a", "|egger.1|p")), c(e$estimate[1], e$pval))
  ok <- is.finite(m$TE) & is.finite(m$seTE)
  te <- m$TE[ok]; v <- m$seTE[ok]^2
  w <- 1 / v
  ts <- (te - sum(w * te) / sum(w)) / sqrt(v - 1 / sum(w))
  if (all(is.finite(ts))) {
    # values that differ by rounding alone are tied, as the program takes them to be
    ts <- signif(ts, 12); v <- signif(v, 12)
    ties <- anyDuplicated(ts) > 0 || anyDuplicated(v) > 0
    r <- suppressWarnings(cor.test(ts, v, method = "kendall", exact = if (ties) FALSE else NULL, continuity = ties))
    put(paste0(key, c("|egger.1|tau", "|egger.1|p2")), c(unname(r$estimate), r$p.value))

  }
  if (harbord) {
    h <- try(suppressWarnings(metabias(m, method.bias = "Harbord", k.min = 4)), silent = TRUE)
    if (!inherits(h, "try-error") && is.finite(h$pval)) put(paste0(key, c("|harbord.1|a", "|harbord.1|p")), c(h$estimate[1], h$pval))
  }
}
breslow_day <- function(a, b, c, d, psi) {
  total <- 0
  for (i in seq_along(a)) {
    n1 <- a[i] + b[i]; n0 <- c[i] + d[i]; m1 <- a[i] + c[i]; m2 <- b[i] + d[i]
    if (n1 == 0 || n0 == 0 || m1 == 0 || m2 == 0) next
    f <- function(x) x * (n0 - m1 + x) - psi * (n1 - x) * (m1 - x)
    lo <- max(0, m1 - n0); hi <- min(n1, m1)
    ea <- uniroot(f, c(lo, hi), tol = 1e-13)$root
    total <- total + (a[i] - ea)^2 * (1 / ea + 1 / (n1 - ea) + 1 / (m1 - ea) + 1 / (n0 - m1 + ea))
  }
  total
}

at <- 1
while (at <= length(lines)) {
  head <- strsplit(lines[at], "\t")[[1]]
  if (head[1] != "case") { at <- at + 1; next }
  name <- head[2]; k <- as.integer(head[4]); level <- as.numeric(head[5])
  x <- matrix(as.numeric(unlist(strsplit(lines[at + seq_len(k)], "\t"))), k, byrow = TRUE)
  at <- at + k + 1
  if (head[3] != "bin") next
  r1 <- x[, 1]; n1 <- x[, 2]; r2 <- x[, 3]; n2 <- x[, 4]
  a <- r1; b <- r2; c <- n1 - r1; d <- n2 - r2
  for (sn in names(settings)) {
    s <- settings[[sn]]
    fit <- function(sm, method) try(suppressWarnings(metabin(r1, n1, r2, n2, sm = sm, method = method, incr = s$incr, method.incr = "only0",
                 MH.exact = s$delay, method.tau = "DL", level = level, level.ma = level, level.hetstat = level,
                 Q.Cochrane = method == "MH", warn = FALSE)), silent = TRUE)
    # ---- odds ratio, Mantel-Haenszel
    key <- paste(name, sn, "or", sep = "|")
    m <- fit("OR", "MH")
    if (!inherits(m, "try-error") && is.finite(m$TE.common)) {
      put(paste0(key, c("|odds", "|from", "|to")), exp(c(m$TE.common, m$lower.common, m$upper.common)))

      put(paste0(key, c("|dsor", "|dsll", "|dsul", "|dsx2")), c(exp(c(m$TE.random, m$lower.random, m$upper.random)), m$statistic.random^2))
      heterogeneity(key, m, s, level)
      ok <- is.finite(m$TE)
      i <- which(ok)
      put(paste0(key, "|or.", i, "|or"), exp(m$TE[i]))
      if (!s$exact) put(c(paste0(key, "|or.", i, "|lci"), paste0(key, "|or.", i, "|uci")), exp(c(m$lower[i], m$upper[i])))
      put(paste0(key, "|or.", i, "|wt"), 100 * m$w.common[i] / sum(m$w.common[i]))
      put(paste0(key, "|or.", i, "|dwt"), 100 * m$w.random[i] / sum(m$w.random[i]))
      bias(key, m, TRUE)
      use <- !(a + b == 0 | c + d == 0)
      if (sum(use) > 0 && all((n1 > 0 & n2 > 0)[use])) {
        tab <- array(rbind(a[use], b[use], c[use], d[use]), c(2, 2, sum(use)))
        if (sum(use) > 1) {
          cmh <- try(suppressWarnings(mantelhaen.test(tab, correct = TRUE)), silent = TRUE)
          if (!inherits(cmh, "try-error")) put(paste0(key, "|chi_mantel"), unname(cmh$statistic))
          put(paste0(key, "|bd"), breslow_day(a[use], b[use], c[use], d[use], exp(m$TE.common)))

        }
      }
    }
    # ---- Peto odds ratio
    if (sn %in% c("x1.tacc.d0", "x0.tacc.d0")) {
      key <- paste(name, sn, "peto", sep = "|")
      m <- fit("OR", "Peto")
      if (!inherits(m, "try-error") && is.finite(m$TE.common)) {
        put(paste0(key, c("|por", "|from", "|to", "|z")), c(exp(c(m$TE.common, m$lower.common, m$upper.common)), m$statistic.common))
        k1 <- m$k
        put(paste0(key, c("|qc", "|isq")), c(if (k1 > 1) m$Q else 0, if (k1 > 1) 100 * m$I2 else NA))
        if (s$exact) { if (k1 > 1) put(paste0(key, c("|llisq", "|ulisq")), isq_exact(m$Q, k1 - 1, level)) }
        else if (k1 > 2) put(paste0(key, c("|llisq", "|ulisq")), 100 * c(m$lower.I2, m$upper.I2))
        i <- which(is.finite(m$TE))
        put(paste0(key, "|odds.", i, "|or"), exp(m$TE[i]))
        put(c(paste0(key, "|odds.", i, "|lci"), paste0(key, "|odds.", i, "|uci")), exp(c(m$lower[i], m$upper[i])))
        put(paste0(key, "|odds.", i, "|wt"), 100 * m$w.common[i] / sum(m$w.common[i]))
        # the bias indicator of Egger is not taken from meta, which makes it from the odds ratios pooled by inverse variance and not
        # from the Peto odds ratios; that of Harbord and Egger is
        h <- if (sum(is.finite(m$TE)) >= 4) try(suppressWarnings(metabias(m, method.bias = "Harbord", k.min = 4)), silent = TRUE) else NULL
        if (!is.null(h) && !inherits(h, "try-error") && is.finite(h$pval)) put(paste0(key, c("|harbord.1|a", "|harbord.1|p")), c(h$estimate[1], h$pval))
      }
    }
    # ---- relative risk
    key <- paste(name, sn, "rr", sep = "|")
    for (cochrane in c(TRUE, FALSE)) {
      m <- try(suppressWarnings(metabin(r1, n1, r2, n2, sm = "RR", method = "MH", incr = s$incr, method.incr = "only0", MH.exact = s$delay,
               RR.Cochrane = cochrane, method.tau = "DL", level = level, level.ma = level, level.hetstat = level, Q.Cochrane = TRUE, warn = FALSE)), silent = TRUE)
      if (inherits(m, "try-error") || !is.finite(m$TE.common)) next
      if (!cochrane) next
      put(paste0(key, c("|rr", "|from", "|to", "|x2")), c(exp(c(m$TE.common, m$lower.common, m$upper.common)), m$statistic.common^2))
      put(paste0(key, c("|dsrr", "|dsll", "|dsul", "|dsx2")), c(exp(c(m$TE.random, m$lower.random, m$upper.random)), m$statistic.random^2))
      heterogeneity(key, m, s, level)
      i <- which(is.finite(m$TE))
      put(paste0(key, "|risks.", i, "|rr"), exp(m$TE[i]))
      if (!s$exact) put(c(paste0(key, "|risks.", i, "|lci"), paste0(key, "|risks.", i, "|uci")), exp(c(m$lower[i], m$upper[i])))
      put(paste0(key, "|risks.", i, "|wt"), 100 * m$w.common[i] / sum(m$w.common[i]))
      put(paste0(key, "|risks.", i, "|dwt"), 100 * m$w.random[i] / sum(m$w.random[i]))
      bias(key, m, FALSE)
      harbord_rr(key, a, b, c, d)
    }
    # ---- risk difference
    key <- paste(name, sn, "rd", sep = "|")
    m <- fit("RD", "MH")
    if (!inherits(m, "try-error") && is.finite(m$TE.common)) {
      put(paste0(key, c("|rmh", "|from", "|to", "|x2")), c(m$TE.common, m$lower.common, m$upper.common, m$statistic.common^2))
      put(paste0(key, c("|dsrd", "|dsll", "|dsul", "|dsx2")), c(m$TE.random, m$lower.random, m$upper.random, m$statistic.random^2))
      heterogeneity(key, m, s, level)
      i <- which(is.finite(m$TE))
      put(paste0(key, "|differences.", i, "|rd"), m$TE[i])
      if (!s$exact) put(c(paste0(key, "|differences.", i, "|lci"), paste0(key, "|differences.", i, "|uci")), c(m$lower[i], m$upper[i]))
      put(paste0(key, "|differences.", i, "|wt"), 100 * m$w.common[i] / sum(m$w.common[i]))
      put(paste0(key, "|differences.", i, "|dwt"), 100 * m$w.random[i] / sum(m$w.random[i]))
      bias(key, m, FALSE)
    }
  }
}
close(out)
cat("figures written to", args[2], "\n")
