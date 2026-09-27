# The figures of R (packages meta and metafor) for the sets of studies of the other analyses, under the keys of the figures of the program
# (Figures.cs), one "key<TAB>value" to a line.  A figure that R gives in more than one way has a key for each way.
# usage: Rscript figures-other.R <cases> <output>
.libPaths(c(Sys.getenv("R_LIBS_USER"), .libPaths()))
suppressPackageStartupMessages({ library(meta); library(metafor) })
args <- commandArgs(trailingOnly = TRUE)
lines <- readLines(args[1])
out <- file(args[2], "w")
# with the exact method off nothing changes in these analyses but the limits of I-squared, which alone are written for that setting
only_limits <- FALSE
put <- function(key, value) {
  if (only_limits) { keep <- grepl("\\|(llisq|ulisq)$", key); key <- key[keep]; value <- value[keep] }
  for (i in seq_along(value)) {
    v <- value[i]
    text <- if (is.na(v)) "NA" else if (is.infinite(v)) (if (v > 0) "Inf" else "-Inf") else format(v, digits = 12)
    cat(key[i], "\t", text, "\n", sep = "", file = out)
  }
}
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
heterogeneity <- function(key, m, exact, level, prefix = "") {
  k <- m$k
  put(paste0(key, prefix, c("|qc", "|isq")), c(if (k > 1) m$Q else 0, if (k > 1) 100 * m$I2 else NA))
  if (is.finite(m$tau2)) put(paste0(key, prefix, "|tausq"), m$tau2)
  if (k > 1) put(paste0(key, prefix, "|xp"), m$pval.Q)
  if (exact) { if (k > 1) put(paste0(key, prefix, c("|llisq", "|ulisq")), isq_exact(m$Q, k - 1, level)) }
  else if (k > 2) put(paste0(key, prefix, c("|llisq", "|ulisq")), 100 * c(m$lower.I2, m$upper.I2))
}
bias <- function(key, m, block = "egger") {
  ok <- is.finite(m$TE) & is.finite(m$seTE) & m$seTE > 0
  if (sum(ok) < 4) return(invisible())
  e <- try(suppressWarnings(metabias(m, method.bias = "Egger", k.min = 4)), silent = TRUE)
  if (!inherits(e, "try-error") && is.finite(e$pval)) put(paste0(key, "|", block, c(".1|a", ".1|p")), c(e$estimate[1], e$pval))
  te <- m$TE[ok]; v <- m$seTE[ok]^2
  w <- 1 / v
  ts <- (te - sum(w * te) / sum(w)) / sqrt(v - 1 / sum(w))
  if (all(is.finite(ts))) {
    # values that differ by rounding alone are tied, as the program takes them to be
    ts <- signif(ts, 12); v <- signif(v, 12)
    ties <- anyDuplicated(ts) > 0 || anyDuplicated(v) > 0
    r <- suppressWarnings(cor.test(ts, v, method = "kendall", exact = if (ties) FALSE else NULL, continuity = ties))
    put(paste0(key, "|", block, c(".1|tau", ".1|p2")), c(unname(r$estimate), r$p.value))
  }
}
common <- function(key, m, names, back = identity) put(paste0(key, names), c(back(c(m$TE.common, m$lower.common, m$upper.common)), m$statistic.common))
random <- function(key, m, names, back = identity) put(paste0(key, names), c(back(c(m$TE.random, m$lower.random, m$upper.random)), m$statistic.random))

at <- 1
while (at <= length(lines)) {
  head <- strsplit(lines[at], "\t")[[1]]
  if (head[1] != "case") { at <- at + 1; next }
  name <- head[2]; kind <- head[3]; k <- as.integer(head[4]); level <- as.numeric(head[5])
  x <- matrix(as.numeric(unlist(strsplit(lines[at + seq_len(k)], "\t"))), k, byrow = TRUE)
  at <- at + k + 1
  for (sn in c("x1.tacc.d0", "x0.tacc.d0")) {
    exact <- sn == "x1.tacc.d0"
    only_limits <- !exact
    lev <- list(level = level, level.ma = level, level.hetstat = level)
    if (kind == "rate") {
      for (sm in c("IRR", "IRD")) {
        key <- paste(name, sn, tolower(sm), sep = "|")
        back <- if (sm == "IRR") exp else identity
        # as the program pools: by inverse variance.  A study without an event in either group is left out; one without an event in
        # one of its groups has meta's continuity correction, which is 0.5 added to the events of both its groups
        keep <- x[, 1] + x[, 3] > 0 & x[, 2] > 0 & x[, 4] > 0
        if (sum(keep) > 0) {
          m <- try(suppressWarnings(do.call(metainc, c(list(x[keep, 1], x[keep, 2], x[keep, 3], x[keep, 4], sm = sm, method = "Inverse", incr = 0.5, method.incr = "only0", method.tau = "DL", warn = FALSE), lev))), silent = TRUE)
          if (!inherits(m, "try-error") && is.finite(m$TE.common)) {
            common(key, m, c("|rmh", "|from", "|to", "|z"), back)
            random(key, m, c(if (sm == "IRR") "|dsirr" else "|dsird", "|dsll", "|dsul", "|dz"), back)
            heterogeneity(key, m, exact, level)
            i <- which(keep)
            put(paste0(key, "|ir.", i, "|", tolower(sm)), back(m$TE))
            if (sm == "IRD") put(c(paste0(key, "|ir.", i, "|lci"), paste0(key, "|ir.", i, "|uci")), c(m$lower, m$upper))
            put(paste0(key, "|ir.", i, "|vi"), m$seTE^2)
            put(paste0(key, "|ir.", i, "|wt"), 100 * m$w.common / sum(m$w.common))
            put(paste0(key, "|ir.", i, "|dwt"), 100 * m$w.random / sum(m$w.random))
            mm <- m; bias(key, mm)
          }
        }

      }
    }
    if (kind == "cont") {
      ok <- x[, 3] > 0 & x[, 6] > 0
      key <- paste(name, sn, "smd", sep = "|")

      if (all(ok)) {
        # the variance of Hedges and Olkin, n / (n1 n2) + d^2 / (2 n), as the program has it
        e <- escalc("SMD", n1i = x[, 1], m1i = x[, 2], sd1i = x[, 3], n2i = x[, 4], m2i = x[, 5], sd2i = x[, 6], vtype = "LS")
        g <- do.call(metagen, c(list(as.numeric(e$yi), sqrt(as.numeric(e$vi)), method.tau = "DL"), lev))
        put(paste0(key, "|approximate.", seq_len(k), "|d"), as.numeric(e$yi))
        put(c(paste0(key, "|approximate.", seq_len(k), "|lci"), paste0(key, "|approximate.", seq_len(k), "|uci")), c(g$lower, g$upper))
        if (k > 1) {
          common(key, g, c("|poolok.1|dplus", "|poolok.1|from", "|poolok.1|to", "|poolok.1|z"))
          random(key, g, c("|poolok.1|dsrd", "|poolok.1|dsll", "|poolok.1|dsul", "|poolok.1|dz"))
          heterogeneity(key, g, exact, level, "|poolok.1")
          put(paste0(key, "|weights.", seq_len(k), "|howt"), 100 * g$w.common / sum(g$w.common))
          put(paste0(key, "|weights.", seq_len(k), "|dswt"), 100 * g$w.random / sum(g$w.random))
        }
        bias(key, g)
      }
      key <- paste(name, sn, "wmd", sep = "|")
      if (all(ok)) {
        m <- do.call(metacont, c(list(x[, 1], x[, 2], x[, 3], x[, 4], x[, 5], x[, 6], sm = "MD", method.tau = "DL", warn = FALSE), lev))
        i <- seq_len(k)
        put(paste0(key, "|approximate.", i, "|d"), m$TE)
        put(c(paste0(key, "|approximate.", i, "|lci"), paste0(key, "|approximate.", i, "|uci")), c(m$lower, m$upper))

        if (k > 1) {
          common(key, m, c("|poolok.1|dplus", "|poolok.1|from", "|poolok.1|to", "|poolok.1|z"))
          random(key, m, c("|poolok.1|dsrd", "|poolok.1|dsll", "|poolok.1|dsul", "|poolok.1|dz"))
          heterogeneity(key, m, exact, level, "|poolok.1")
        }
        bias(key, m)
      }
    }
    if (kind == "prop") {
      for (method in c("doubleArcsine", "arcsine")) {
        key <- paste(name, sn, paste0("prop.", method), sep = "|")
        m <- try(suppressWarnings(do.call(metaprop, c(list(x[, 1], x[, 2], sm = "PFT", method = "Inverse", method.ci = "CP", method.tau = "DL", warn = FALSE), lev))), silent = TRUE)
        if (inherits(m, "try-error") || !is.finite(m$TE.common)) next
        i <- seq_len(k)
        put(paste0(key, "|proportions.", i, "|p"), x[, 1] / x[, 2])
        cp <- sapply(i, function(j) binom.test(x[j, 1], x[j, 2], conf.level = level)$conf.int)
        put(c(paste0(key, "|proportions.", i, "|from_y"), paste0(key, "|proportions.", i, "|to_y")), c(cp[1, ], cp[2, ]))
        put(paste0(key, "|proportions.", i, "|wt"), 100 * m$w.common / sum(m$w.common))
        put(paste0(key, "|proportions.", i, "|dwt"), 100 * m$w.random / sum(m$w.random))
        put(paste0(key, "|proportions.", i, "|yi"), 2 * m$TE)
        put(paste0(key, "|proportions.", i, "|vi"), 4 * m$seTE^2)
        hm <- 1 / mean(1 / x[, 2])
        back <- function(t) if (method == "doubleArcsine") meta:::asin2p(t, rep(hm, length(t))) else sin(t)^2
        fixed <- sapply(c(m$TE.common, m$lower.common, m$upper.common), back)
        # the program gives a pooled proportion of 0, with a lower limit of 0, if no study has an event, and of 1, with an upper limit
        # of 1, if every subject of every study has one
        if (all(x[, 1] == 0)) fixed[1:2] <- 0
        if (all(x[, 1] == x[, 2])) fixed[c(1, 3)] <- 1
        put(paste0(key, c("|rmh", "|from", "|to")), fixed)
        put(paste0(key, c("|dspr", "|from_ds", "|to_ds")), sapply(c(m$TE.random, m$lower.random, m$upper.random), back))
        put(paste0(key, c("|qc", "|isq")), c(if (k > 1) m$Q else 0, if (k > 1) 100 * m$I2 else NA))
        if (is.finite(m$tau2)) put(paste0(key, "|tausq"), 4 * m$tau2)
        if (exact) { if (k > 1) put(paste0(key, c("|llisq", "|ulisq")), isq_exact(m$Q, k - 1, level)) }
        else if (k > 2) put(paste0(key, c("|llisq", "|ulisq")), 100 * c(m$lower.I2, m$upper.I2))
      }
    }
    if (kind == "cor") {
      key <- paste(name, sn, "cor", sep = "|")
      if (all(abs(x[, 1]) < 1) && all(x[, 2] > 3)) {
        m <- do.call(metacor, c(list(x[, 1], x[, 2], sm = "ZCOR", method.tau = "DL"), lev))
        i <- seq_len(k)
        put(c(paste0(key, "|studies.", i, "|from"), paste0(key, "|studies.", i, "|to")), tanh(c(m$lower, m$upper)))
        put(paste0(key, "|studies.", i, "|wt"), 100 * m$w.common / sum(m$w.common))
        put(paste0(key, "|studies.", i, "|dwt"), 100 * m$w.random / sum(m$w.random))
        put(paste0(key, c("|rmh", "|from_fixed", "|to_fixed", "|z")), c(tanh(c(m$TE.common, m$lower.common, m$upper.common)), m$statistic.common))
        put(paste0(key, c("|dsrr", "|dsll", "|dsul", "|dz")), c(tanh(c(m$TE.random, m$lower.random, m$upper.random)), m$statistic.random))
        heterogeneity(key, m, exact, level)
        bias(key, m, "bias")
        hs <- try(suppressWarnings(rma(measure = "COR", ri = x[, 1], ni = x[, 2], method = "HS", weights = x[, 2], level = 100 * level)), silent = TRUE)
        if (!inherits(hs, "try-error")) put(paste0(key, "|wmr"), hs$b[1])
        n <- x[, 2]; r <- x[, 1]
        wmr <- sum(n * r) / sum(n)
        varr <- sum(n * (r - wmr)^2) / sum(n)
        vare <- (1 - wmr^2)^2 / (mean(n) - 1)
        z <- qnorm(1 - (1 - level) / 2)
        put(paste0(key, c("|var_r", "|var_e", "|var_p", "|wmr_lcl", "|wmr_ucl", "|het_x2")),
            c(varr, vare, max(0, varr - vare), wmr - z * sqrt(varr / k), wmr + z * sqrt(varr / k), k * varr / vare))
      }
    }
    if (kind == "gen") {
      for (ratio in c(FALSE, TRUE)) {
        if (ratio && any(x[, 1] <= 0)) next
        z <- qnorm(1 - (1 - level) / 2)
        for (limits in c(FALSE, TRUE)) {
          key <- paste(name, sn, paste0("gen.", if (ratio) "ratio" else "plain", if (limits) ".ci" else ".se"), sep = "|")
          if (limits && ratio && any(x[, 3] <= 0)) next
          te <- if (ratio) log(x[, 1]) else x[, 1]
          se <- if (!limits) x[, 2] else if (ratio) (log(x[, 4]) - log(x[, 3])) / 2 / z else (x[, 4] - x[, 3]) / 2 / z
          m <- do.call(metagen, c(list(te, se, method.tau = "DL"), lev))
          back <- if (ratio) exp else identity
          i <- seq_len(k)
          put(paste0(key, "|studies.", i, "|y"), x[, 1])
          if (!limits) put(c(paste0(key, "|studies.", i, "|from"), paste0(key, "|studies.", i, "|to")), back(c(m$lower, m$upper)))
          put(paste0(key, "|studies.", i, "|wt"), 100 * m$w.common / sum(m$w.common))
          put(paste0(key, "|studies.", i, "|dwt"), 100 * m$w.random / sum(m$w.random))
          common(key, m, c("|rmh", "|from_fixed", "|to_fixed", "|z"), back)
          random(key, m, c("|dsrr", "|dsll", "|dsul", "|dz"), back)
          heterogeneity(key, m, exact, level)
          bias(key, m, "bias")
        }
      }
    }
  }
}
close(out)
cat("figures written to", args[2], "\n")
