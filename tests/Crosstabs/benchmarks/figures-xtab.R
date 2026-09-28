# The figures of R for the tables of the Crosstabs report, under the keys of the figures of the program (Figures.cs), one
# "key<TAB>value" to a line.  The statistics are worked out from their definitions; the exact test is R's fisher.test, and for small
# tables also the sum over every table with the same totals.
# usage: Rscript figures-xtab.R <cases> <output> [the greatest number of subjects for which the exact test is made; 160 if not given]
suppressPackageStartupMessages(library(MASS))
args <- commandArgs(trailingOnly = TRUE)
exact_to <- if (length(args) >= 3) as.numeric(args[3]) else 160
lines <- readLines(args[1])
out <- file(args[2], "w")
put <- function(key, value) {
  for (i in seq_along(value)) {
    v <- value[i]
    text <- if (is.character(v)) paste0("\"", v, "\"") else if (is.nan(v)) "NaN" else if (is.na(v)) "NA" else if (is.infinite(v)) (if (v > 0) "Inf" else "-Inf") else format(v, digits = 15)
    cat(key[i], "\t", text, "\n", sep = "", file = out)
  }
}

# every table with the totals of t, and the sum of the probabilities of those no more probable than t
enumerate_p <- function(t) {
  r <- rowSums(t); cc <- colSums(t); n <- sum(t)
  logp <- function(x) sum(lfactorial(r)) + sum(lfactorial(cc)) - lfactorial(n) - sum(lfactorial(x))
  observed <- logp(t)
  total <- 0; below <- 0
  fill <- function(x, i, j, rleft, cleft) {
    # the cell (i, j) of the table x, filled column by column
    if (j == ncol(t)) {
      x[, j] <- rleft
      if (any(rleft < 0) || sum(rleft) != cleft[j]) return(invisible())
      p <- exp(logp(x)); total <<- total + p
      if (logp(x) <= observed + 1e-7) below <<- below + p
      return(invisible())
    }
    if (i == nrow(t)) {
      v <- cleft[j]
      if (v < 0 || v > rleft[i]) return(invisible())
      x[i, j] <- v; rleft[i] <- rleft[i] - v; cleft[j] <- 0
      return(fill(x, 1, j + 1, rleft, cleft))
    }
    for (v in 0:min(rleft[i], cleft[j])) {
      x2 <- x; x2[i, j] <- v
      rl <- rleft; rl[i] <- rl[i] - v
      cl <- cleft; cl[j] <- cl[j] - v
      fill(x2, i + 1, j, rl, cl)
    }
  }
  fill(matrix(0, nrow(t), ncol(t)), 1, 1, r, cc)
  stopifnot(abs(total - 1) < 1e-9)
  below
}

# The exact P values that the simulation estimates: for each of four statistics, the sum of the probabilities of the tables with the
# totals of t whose statistic is no less than that of t
statistics_of <- function(x, rs, cs) {
  n <- sum(x); rt <- rowSums(x); ct <- colSums(x)
  rows <- which(rt > 0); cols <- which(ct > 0)
  u <- x[rows, cols, drop = FALSE]; e <- outer(rt[rows], ct[cols]) / n
  x2 <- sum((u - e)^2 / e)
  g2 <- 2 * sum(ifelse(u > 0, u * log(u / e), 0))
  m <- sum(rt * rs) / n
  eq <- (n - 1) * sum(ct[cols] * (colSums(u * rs[rows]) / ct[cols] - m)^2) / sum(rt * (rs - m)^2)
  sxy <- sum(x * outer(rs, cs)) - sum(rt * rs) * sum(ct * cs) / n
  trend <- (n - 1) * sxy^2 / ((sum(rt * rs^2) - sum(rt * rs)^2 / n) * (sum(ct * cs^2) - sum(ct * cs)^2 / n))
  c(x2, g2, eq, trend)
}
enumerate_statistics <- function(t, rs, cs) {
  r <- rowSums(t); cc <- colSums(t); n <- sum(t)
  logp <- function(x) sum(lfactorial(r)) + sum(lfactorial(cc)) - lfactorial(n) - sum(lfactorial(x))
  observed <- statistics_of(t, rs, cs)
  total <- 0; above <- rep(0, 4)
  fill <- function(x, i, j, rleft, cleft) {
    if (j == ncol(t)) {
      x[, j] <- rleft
      if (any(rleft < 0) || sum(rleft) != cleft[j]) return(invisible())
      p <- exp(logp(x)); total <<- total + p
      s <- statistics_of(x, rs, cs)
      above <<- above + p * (s >= observed - 1e-9 * pmax(1, abs(observed)))
      return(invisible())
    }
    if (i == nrow(t)) {
      v <- cleft[j]
      if (v < 0 || v > rleft[i]) return(invisible())
      x[i, j] <- v; rleft[i] <- rleft[i] - v; cleft[j] <- 0
      return(fill(x, 1, j + 1, rleft, cleft))
    }
    for (v in 0:min(rleft[i], cleft[j])) {
      x2 <- x; x2[i, j] <- v
      rl <- rleft; rl[i] <- rl[i] - v
      cl <- cleft; cl[j] <- cl[j] - v
      fill(x2, i + 1, j, rl, cl)
    }
  }
  fill(matrix(0, nrow(t), ncol(t)), 1, 1, r, cc)
  stopifnot(abs(total - 1) < 1e-9)
  above
}

# concordant and discordant pairs, each pair counted once
pairs_of <- function(t) {
  r <- nrow(t); cc <- ncol(t); C <- 0; D <- 0
  for (i in seq_len(r)) for (j in seq_len(cc)) {
    if (i < r && j < cc) C <- C + t[i, j] * sum(t[(i + 1):r, (j + 1):cc])
    if (i < r && j > 1) D <- D + t[i, j] * sum(t[(i + 1):r, 1:(j - 1)])
  }
  c(C, D)
}
gamma_of <- function(t) { p <- pairs_of(t); (p[1] - p[2]) / (p[1] + p[2]) }
taub_of <- function(t) { p <- pairs_of(t); n <- sum(t); 2 * (p[1] - p[2]) / sqrt((n^2 - sum(rowSums(t)^2)) * (n^2 - sum(colSums(t)^2))) }
excess_of <- function(t) { p <- pairs_of(t); 2 * (p[1] - p[2]) }
# the variance of a function of the counts of a multinomial sample, by the delta method with derivatives taken numerically
delta_variance <- function(f, t) {
  g <- matrix(0, nrow(t), ncol(t))
  for (i in seq_len(nrow(t))) for (j in seq_len(ncol(t))) {
    h <- 1e-4 * max(1, t[i, j])
    up <- t; up[i, j] <- up[i, j] + h; down <- t; down[i, j] <- down[i, j] - h
    up2 <- t; up2[i, j] <- up2[i, j] + 2 * h; down2 <- t; down2[i, j] <- down2[i, j] - 2 * h
    g[i, j] <- (8 * (f(up) - f(down)) - (f(up2) - f(down2))) / (12 * h)
  }
  sum(t * g^2) - sum(t * g)^2 / sum(t)
}

analysis <- function(prefix, t, level, rs, cs, exact = TRUE, limit = 160) {
  r <- nrow(t); cc <- ncol(t)
  if (is.null(rs)) rs <- seq_len(r)
  if (is.null(cs)) cs <- seq_len(cc)
  n <- sum(t); rt <- rowSums(t); ct <- colSums(t)
  z <- qnorm(1 - (1 - level) / 2)
  e <- outer(rt, ct) / n
  for (i in seq_len(r)) {
    put(paste0(prefix, "|rows.", i, "|obs.", seq_len(cc), "|obs"), t[i, ])
    put(paste0(prefix, "|rows.", i, "|rtot.1|rtot"), rt[i])
    put(paste0(prefix, "|rows.", i, "|score.1|score"), rs[i])
    put(paste0(prefix, "|rows.", i, "|exps.1|exp.", seq_len(cc), "|exp"), e[i, ])
    put(paste0(prefix, "|rows.", i, "|chis.1|chi.", seq_len(cc), "|chi"), ifelse(e[i, ] > 0, (t[i, ] - e[i, ])^2 / e[i, ], NA))
    put(paste0(prefix, "|rows.", i, "|pcrs.1|pcr.", seq_len(cc), "|pcr"), if (rt[i] > 0) 100 * t[i, ] / rt[i] else rep(NA, cc))
    put(paste0(prefix, "|rows.", i, "|pcrs.1|pcc.", seq_len(cc + 1), "|pcc"), c(ifelse(ct > 0, 100 * t[i, ] / ct, NA), 100 * rt[i] / n))
  }
  put(paste0(prefix, "|tot.", seq_len(cc + 1), "|tot"), c(ct, n))
  put(paste0(prefix, "|pcgs.1|pcg.", seq_len(cc), "|pcg"), 100 * ct / n)
  put(paste0(prefix, "|scores.1|score.", seq_len(cc), "|score"), cs)
  # the rows and columns that have counts
  rows <- which(rt > 0); cols <- which(ct > 0)
  u <- t[rows, cols, drop = FALSE]; eu <- e[rows, cols, drop = FALSE]
  put(paste0(prefix, "|tot"), length(u))
  warn <- character()
  if (any(eu < 1)) warn <- c(warn, sprintf("Warning: %d out of %d cells have EXPECTATION < 1", sum(eu < 1), length(u)))
  if (any(eu < 5)) warn <- c(warn, sprintf("Warning: %d out of %d cells have EXPECTATION < 5", sum(eu < 5), length(u)))
  put(paste0(prefix, "|*warn|rows"), length(warn))
  if (length(warn)) put(paste0(prefix, "|warn.", seq_along(warn), "|warn"), warn)
  x2 <- sum((u - eu)^2 / eu)
  g2 <- 2 * sum(ifelse(u > 0, u * log(u / eu), 0))
  df <- (length(rows) - 1) * (length(cols) - 1)
  put(paste0(prefix, c("|chio", "|dfo", "|po", "|g2", "|pog2")), c(x2, df, if (df > 0) pchisq(x2, df, lower.tail = FALSE) else NA, g2, if (df > 0) pchisq(g2, df, lower.tail = FALSE) else NA))
  whole <- all(t == floor(t))
  # the exact test of a large table takes minutes: it is made where the table has no more than 160 subjects, as in the harness
  if (exact && whole && n <= limit && r > 1 && cc > 1) {
    if (length(rows) > 1 && length(cols) > 1) {
      # a table that is large and has many cells takes R a long time too
      if (length(u) <= 12 || n <= 60) {
        f <- try(fisher.test(u, workspace = 2e7)$p.value, silent = TRUE)
        if (!inherits(f, "try-error")) put(paste0(prefix, "|p2.fisher"), f)
      }
      if (length(u) <= 9 && n <= 60) put(paste0(prefix, "|p2.enumerated"), enumerate_p(u))
    }
  }
  # the mean score of the rows in each column: are the means of the columns the same?
  mean_all <- sum(rt * rs) / n
  sst <- sum(rt * (rs - mean_all)^2)
  means <- colSums(u * rs[rows]) / ct[cols]
  ssb <- sum(ct[cols] * (means - mean_all)^2)
  eq <- (n - 1) * ssb / sst
  put(paste0(prefix, c("|chie", "|dfe", "|pe")), c(eq, length(cols) - 1, if (length(cols) > 1) pchisq(eq, length(cols) - 1, lower.tail = FALSE) else NA))
  # the correlation of the scores of the rows and of the columns over the subjects
  w <- as.vector(t); x <- rs[as.vector(row(t))]; y <- cs[as.vector(col(t))]
  cv <- cov.wt(cbind(x, y), wt = w / sum(w), cor = TRUE, method = "ML")
  rho <- cv$cor[1, 2]
  put(paste0(prefix, c("|r", "|chit", "|pt")), c(rho, (n - 1) * rho^2, pchisq((n - 1) * rho^2, 1, lower.tail = FALSE)))
  put(paste0(prefix, c("|phi", "|pearson")), c(sqrt(x2 / n), sqrt(x2 / (x2 + n))))
  if (r == 2 && cc == 2) put(paste0(prefix, "|cramer"), (t[1, 1] * t[2, 2] - t[1, 2] * t[2, 1]) / sqrt(prod(rt) * prod(ct)))
  else put(paste0(prefix, "|cramer"), sqrt(x2 / n / min(length(rows) - 1, length(cols) - 1)))
  # the ordinal measures
  p <- pairs_of(t)
  measure <- function(name, f, scale, se_names, p_names, ll_names, ul_names) {
    est <- f(t)
    put(paste0(prefix, "|", name), est)
    se1 <- sqrt(delta_variance(f, t))
    se0 <- sqrt(delta_variance(excess_of, t)) / scale
    both <- list(se1, se0)
    for (m in 1:2) {
      se <- both[[m]]
      # a standard error of nothing (perfect association) comes out of the numerical derivatives as a very small number
      if (is.finite(se) && se < 1e-7) se <- 0
      ok <- is.finite(se) && se > 0
      put(paste0(prefix, "|", c(se_names[m], p_names[m], ll_names[m], ul_names[m])), c(se, if (ok) 2 * pnorm(-abs(est / se)) else NA, if (ok) est - z * se else NA, if (ok) est + z * se else NA))
    }
  }
  measure("gamma", gamma_of, 2 * (p[1] + p[2]), c("seg", "segi"), c("pg", "pgi"), c("llg", "llgi"), c("ulg", "ulgi"))
  measure("taub", taub_of, sqrt((n^2 - sum(rt^2)) * (n^2 - sum(ct^2))), c("setaub", "setaubi"), c("ptaub", "ptaubi"), c("lltaub", "lltaubi"), c("ultaub", "ultaubi"))
  put(paste0(prefix, "|pc"), 100 * level)
}

# the generalised tests of Cochran, Mantel and Haenszel: u the scores of the rows, v of the columns
gcmh <- function(tab, u, v) {
  r <- dim(tab)[1]; cc <- dim(tab)[2]; K <- dim(tab)[3]
  Ag <- kronecker(cbind(diag(cc - 1), 0), cbind(diag(r - 1), 0))
  Am <- kronecker(matrix(v, 1), cbind(diag(r - 1), 0))
  Ac <- kronecker(matrix(v, 1), matrix(u, 1))
  acc <- function(A) {
    S <- 0; V <- 0
    for (k in 1:K) {
      n <- tab[, , k]; N <- sum(n); R <- rowSums(n); C <- colSums(n)
      if (N < 2) next
      m <- as.vector(outer(R, C) / N)
      Cov <- kronecker(N * diag(C, cc) - C %*% t(C), N * diag(R, r) - R %*% t(R)) / (N^2 * (N - 1))
      S <- S + A %*% (as.vector(n) - m)
      V <- V + A %*% Cov %*% t(A)
    }
    if (length(V) == 1 && all(V == 0)) return(c(NA, 0))
    rank <- qr(V, tol = 1e-9)$rank
    c(as.numeric(t(S) %*% ginv(V, tol = 1e-9) %*% S), rank)
  }
  rbind(acc(Ac), acc(Am), acc(Ag))
}

at <- 1
while (at <= length(lines)) {
  head <- strsplit(lines[at], "\t")[[1]]
  if (head[1] != "case") { at <- at + 1; next }
  name <- head[2]; kind <- head[3]; r <- as.integer(head[4]); cc <- as.integer(head[5]); k <- as.integer(head[6]); level <- as.numeric(head[7])
  sc <- function(s) if (s == "-") NULL else as.numeric(strsplit(s, ",")[[1]])
  rs <- sc(head[8]); cs <- sc(head[9])
  x <- matrix(as.numeric(unlist(strsplit(lines[at + seq_len(r * k)], "\t"))), r * k, byrow = TRUE)
  at <- at + r * k + 1
  t <- array(0, c(r, cc, k))
  for (s in seq_len(k)) t[, , s] <- x[(s - 1) * r + seq_len(r), , drop = FALSE]
  if (kind == "rc") {
    m <- matrix(t[, , 1], r, cc)
    analysis(paste0(name, "|schi"), m, level, rs, cs, limit = exact_to)
    if (all(m == floor(m)) && sum(m) <= 60 && length(m) <= 9 && r > 1 && cc > 1 && sum(rowSums(m) > 0) > 1 && sum(colSums(m) > 0) > 1) {
      rows <- which(rowSums(m) > 0); cols <- which(colSums(m) > 0)
      urs <- if (is.null(rs)) seq_len(r) else rs; ucs <- if (is.null(cs)) seq_len(cc) else cs
      p <- enumerate_statistics(m[rows, cols, drop = FALSE], urs[rows], ucs[cols])
      put(paste0(name, "|mc|", c("pmcx2", "pmcg2", "pmcx2eq", "pmcx2trend"), ".1|p.exact"), p)
    }
    if (all(m == floor(m))) {
      rows <- which(rowSums(m) > 0); cols <- which(colSums(m) > 0)
      for (sym in 0:1) {
        labels_r <- rows; labels_c <- cols
        if (sym == 1 && length(rows) != length(cols)) labels_r <- labels_c <- sort(union(rows, cols))
        u <- matrix(0, length(labels_r), length(labels_c))
        u[match(rows, labels_r), match(cols, labels_c)] <- m[rows, cols]
        prefix <- paste0(name, "|xtab.sym", sym, "|columns.1")
        put(paste0(prefix, "|xtab.1|x.", seq_along(labels_c), "|x"), as.character(labels_c))
        for (i in seq_along(labels_r)) {
          put(paste0(prefix, "|xtab.1|y.", i, "|y"), as.character(labels_r[i]))
          put(paste0(prefix, "|xtab.1|y.", i, "|tot.", seq_along(labels_c), "|tot"), u[i, ])
        }
        # the scores that are given go to the categories in their order
        urs <- if (is.null(rs)) NULL else if (length(rs) == nrow(u)) rs else NA
        ucs <- if (is.null(cs)) NULL else if (length(cs) == ncol(u)) cs else NA
        if (identical(urs, NA) || identical(ucs, NA)) next
        analysis(paste0(prefix, "|chirxc.1"), u, level, urs, ucs, limit = min(60, exact_to))
      }
    }
  } else if (r == 2 && cc == 2) {
    a <- t[2, 2, ]; b <- t[1, 2, ]; c0 <- t[2, 1, ]; d <- t[1, 1, ]
    nn <- a + b + c0 + d
    for (study in c("casecontrol", "cohort")) {
      block <- if (study == "casecontrol") "mantel" else "rrmeta"
      prefix <- paste0(name, "|", study, "|columns.1|", block, ".1")
      put(paste0(prefix, "|inputs.", seq_len(k), "|a"), a); put(paste0(prefix, "|inputs.", seq_len(k), "|b"), b)
      put(paste0(prefix, "|inputs.", seq_len(k), "|c"), c0); put(paste0(prefix, "|inputs.", seq_len(k), "|d"), d)
    }
    # the pooled odds ratio of Mantel and Haenszel and its chi-square, where no table has an empty cell or is left out
    if (all(t > 0)) {
      arr <- array(rbind(a, c0, b, d), c(2, 2, k))
      mh <- mantelhaen.test(arr, conf.level = level)
      put(paste0(name, "|casecontrol|columns.1|mantel.1", c("|odds", "|from", "|to", "|chi_mantel", "|chi_p")), c(unname(mh$estimate), mh$conf.int, unname(mh$statistic), mh$p.value))
      rr <- sum(a * (b + d) / nn) / sum(b * (a + c0) / nn)
      put(paste0(name, "|cohort|columns.1|rrmeta.1|rr"), rr)
    }
  } else {
    prefix <- paste0(name, "|cmh|columns.1")
    for (s in seq_len(k)) for (i in seq_len(r)) put(paste0(prefix, "|xtabz.1|z.", s, "|y.", i, "|tot.", seq_len(cc), "|tot"), t[i, , s])
    urs <- if (is.null(rs)) seq_len(r) else rs; ucs <- if (is.null(cs)) seq_len(cc) else cs
    q <- gcmh(t, urs, ucs)
    put(paste0(prefix, "|gencmh.1|", c("x21", "x22", "x23")), q[, 1])
    put(paste0(prefix, "|gencmh.1|", c("df1", "df2", "df3")), q[, 2])
    put(paste0(prefix, "|gencmh.1|", c("p1", "p2", "p3")), ifelse(q[, 2] > 0, pchisq(q[, 1], q[, 2], lower.tail = FALSE), NA))
    put(paste0(prefix, "|gencmh.1|nt"), sum(t))
    if (r > 1 && cc > 1 && all(apply(t, 3, sum) > 1)) {
      mh <- try(mantelhaen.test(t), silent = TRUE)
      if (!inherits(mh, "try-error")) put(paste0(prefix, "|gencmh.1|x23.mantelhaen"), unname(mh$statistic))
    }
  }
}
close(out)
cat("figures written to", args[2], "\n")
