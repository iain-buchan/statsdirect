# Benchmarks of the Chi-square Tests menu, worked out from the definitions.
# usage: Rscript --vanilla figures-chi.R cases-chi.txt r-chi.txt
args <- commandArgs(trailingOnly = TRUE)
lines <- readLines(args[1])
out <- character(0)
put <- function(key, value) out <<- c(out, paste0(key, "\t", if (is.character(value)) paste0("\"", value, "\"") else if (is.na(value)) "NA" else if (is.infinite(value)) (if (value > 0) "Inf" else "-Inf") else format(value, digits = 15, scientific = TRUE)))
upper <- function(x, df) pchisq(x, df, lower.tail = FALSE)

# ---- the exact tests of a 2 by 2 table (as in the benchmarks of the Exact Tests on Counts menu)
arranged <- function(t) { a <- t[1]; b <- t[2]; c <- t[3]; d <- t[4]; if (a > d) { x <- a; a <- d; d <- x }; if (b > c) { x <- b; b <- c; c <- x }; c(a, b, c, d) }
fisher <- function(key, t) {
  u <- arranged(round(t)); a <- u[1]; b <- u[2]; c <- u[3]; d <- u[4]
  p <- a + b; q <- c + d; r <- a + c; n <- p + q
  put(paste0(key, "|tab3_a1"), a); put(paste0(key, "|tab3_b1"), b); put(paste0(key, "|tab3_a2"), c); put(paste0(key, "|tab3_b2"), d)
  e <- p * r / n
  put(paste0(key, "|exp_a"), e)
  # the values of a that have a probability above nothing: those within 60 standard deviations of the expectation
  sd <- sqrt(p * q / n * r / n * (n - r) / max(1, n - 1))
  support <- max(0, r - q, floor(e - 60 * sd - 2)):min(p, r, ceiling(e + 60 * sd + 2))
  prob <- dhyper(support, p, q, r)
  observed <- dhyper(a, p, q, r)
  lower <- phyper(a, p, q, r); up <- phyper(a - 1, p, q, r, lower.tail = FALSE)
  one <- if (a > e) up else lower
  put(paste0(key, "|p_1"), one)
  put(paste0(key, "|p_1d"), min(1, 2 * one))
  put(paste0(key, "|p_2"), min(1, sum(prob[prob <= observed * (1 + 1e-7)])))
  put(paste0(key, "|mid_p"), one - observed / 2)
  put(paste0(key, "|mid_p_2"), min(1, 2 * (one - observed / 2)))
}
conditional <- function(key, t, level) {
  t <- round(t)
  a <- t[1]; b <- t[2]; c <- t[3]; d <- t[4]
  m1 <- a + b; n1 <- a + c; n0 <- b + d
  lo <- max(0, m1 - n0); hi <- min(m1, n1)
  if (hi - lo + 1 > 1000000) return(invisible())
  support <- lo:hi
  logd <- dhyper(support, n1, n0, m1, log = TRUE)
  dens <- function(logpsi) { v <- logd + logpsi * support; v <- exp(v - max(v)); v / sum(v) }
  up <- function(logpsi, half) { v <- dens(logpsi); sum(v[support > a]) + half * v[support == a] }
  down <- function(logpsi, half) { v <- dens(logpsi); sum(v[support < a]) + half * v[support == a] }
  root <- function(f) uniroot(f, c(-60, 60), tol = 1e-13, extendInt = "yes", maxiter = 10000)$root
  alpha <- 1 - level
  put(paste0(key, "|llf"), if (a == lo) 0 else exp(root(function(l) up(l, 1) - alpha / 2)))
  put(paste0(key, "|ulf"), if (a == hi) Inf else exp(root(function(l) down(l, 1) - alpha / 2)))
  put(paste0(key, "|llm"), if (a == lo) 0 else exp(root(function(l) up(l, 0.5) - alpha / 2)))
  put(paste0(key, "|ulm"), if (a == hi) Inf else exp(root(function(l) down(l, 0.5) - alpha / 2)))
  prob <- exp(logd); observed <- prob[support == a]
  upper <- sum(prob[support >= a]); lower <- sum(prob[support <= a])
  put(paste0(key, "|p1f"), min(upper, lower))
  put(paste0(key, "|p2f"), min(1, sum(prob[prob <= observed * (1 + 1e-7)])))
  mid <- min(upper - observed / 2, lower - observed / 2)
  put(paste0(key, "|p1m"), mid)
  put(paste0(key, "|p2m"), min(1, 2 * mid))
}

# ---- the risk ratio and the risk difference of a cohort
koopman <- function(x1, n1, x0, n0, level) {
  chi2 <- function(theta) {
    A <- (n0 + n1) * theta
    B <- -((x0 + n1) * theta + x1 + n0)
    p0 <- 2 * (x0 + x1) / (-B + sqrt(B^2 - 4 * A * (x0 + x1)))
    p1 <- theta * p0
    (x1 - n1 * p1)^2 / (n1 * p1 * (1 - p1)) * (1 + n1 * (theta - p1) / (n0 * (1 - p1)))
  }
  est <- (x1 / n1) / (x0 / n0)
  f <- function(lt) chi2(exp(lt)) - qchisq(level, 1)
  lower <- if (x1 == 0) 0 else exp(uniroot(f, c(log(est) - 30, log(est)), tol = 1e-13)$root)
  upper <- exp(uniroot(f, c(if (x1 == 0) -30 else log(est), (if (x1 == 0) 0 else log(est)) + 30), tol = 1e-13)$root)
  c(lower, upper)
}
miettinen <- function(x1, n1, x0, n0, level) {
  N <- n1 + n0
  dhat <- x1 / n1 - x0 / n0
  score <- function(delta) {
    # the proportions that are most likely with the difference delta: a root of a cubic, by its formula
    theta <- n0 / n1
    A <- 1 + theta
    B <- -(1 + theta + x1 / n1 + theta * x0 / n0 + delta * (theta + 2))
    C <- delta^2 + delta * (2 * x1 / n1 + theta + 1) + x1 / n1 + theta * x0 / n0
    D <- -(x1 / n1) * delta * (1 + delta)
    v <- B^3 / (27 * A^3) - B * C / (6 * A^2) + D / (2 * A)
    u <- sign(v) * sqrt(max(0, B^2 / (9 * A^2) - C / (3 * A)))
    if (u == 0) { p1 <- -B / (3 * A) } else {
      w <- (pi + acos(max(-1, min(1, v / u^3)))) / 3
      p1 <- 2 * u * cos(w) - B / (3 * A)
    }
    p1 <- min(1, max(0, p1)); p0 <- min(1, max(0, p1 - delta))
    (dhat - delta)^2 / ((p1 * (1 - p1) / n1 + p0 * (1 - p0) / n0) * N / (N - 1))
  }
  f <- function(delta) score(delta) - qchisq(level, 1)
  c(uniroot(f, c(-1 + 1e-10, dhat), tol = 1e-13)$root, uniroot(f, c(dhat, 1 - 1e-10), tol = 1e-13)$root)
}

chi22 <- function(key, t, level, study, asked) {
  a <- t[1]; b <- t[2]; c <- t[3]; d <- t[4]
  p <- a + b; q <- c + d; r <- a + c; s <- b + d; n <- p + q
  if (p * q * r * s <= 0) { put(paste0(key, "|ok"), 0); return(invisible()) }
  put(paste0(key, "|ok"), 1)
  o <- matrix(c(a, c, b, d), 2)
  e <- outer(rowSums(o), colSums(o)) / n
  put(paste0(key, "|tab3_c1"), p); put(paste0(key, "|tab3_c2"), q); put(paste0(key, "|tab3_a3"), r); put(paste0(key, "|tab3_b3"), s); put(paste0(key, "|tab3_c3"), n)
  put(paste0(key, "|tab_a1"), e[1, 1]); put(paste0(key, "|tab_b1"), e[1, 2]); put(paste0(key, "|tab_a2"), e[2, 1]); put(paste0(key, "|tab_b2"), e[2, 2])
  x2 <- sum((o - e)^2 / e)
  put(paste0(key, "|chi"), x2); put(paste0(key, "|chi_p"), upper(x2, 1))
  y2 <- sum(pmax(0, abs(o - e) - 0.5)^2 / e)
  put(paste0(key, "|yates_chi"), y2); put(paste0(key, "|yates_chi_p"), upper(y2, 1))
  put(paste0(key, "|pearson"), sqrt(x2 / (x2 + n)))
  # the correlation of the row and the column of the subjects
  w <- c(a, b, c, d); rows <- c(1, 1, 0, 0); cols <- c(1, 0, 1, 0)
  mr <- sum(w * rows) / n; mc <- sum(w * cols) / n
  put(paste0(key, "|vs"), sum(w * (rows - mr) * (cols - mc)) / sqrt(sum(w * (rows - mr)^2) * sum(w * (cols - mc)^2)))
  small <- any(e < 5) || n < 20
  put(paste0(key, "|warn.rows"), as.numeric(small))
  if (small) put(paste0(key, "|warn.1|wrn"), if (n < 20) "Number of observations" else "Expected frequencies")
  z <- qnorm(1 - (1 - level) / 2)
  done <- FALSE
  put(paste0(key, "|odds.rows"), as.numeric(study == 0))
  # a cohort in which none of those without the characteristic has the outcome has no risk ratio, and the report says so
  put(paste0(key, "|relrisk.rows"), as.numeric(study == 1 && b > 0))
  put(paste0(key, "|note.rows"), as.numeric(study == 1 && b <= 0))
  if (study == 1 && b <= 0) put(paste0(key, "|note.1|note"), "The risk ratio is not given: none of those without the characteristic has the outcome.")
  if (study == 0) {
    k <- paste0(key, "|odds.1")
    put(paste0(k, "|odds"), if (a * d == 0 && b * c == 0) NA else if (a * d == 0) 0 else if (b * c == 0) Inf else a * d / (b * c))
    if (a * d > 0 && b * c > 0) {
      # the limits of Woolf are those of the logistic regression of the outcome on the group
      fit <- suppressWarnings(glm(cbind(c(a, b), c(c, d)) ~ c(1, 0), family = binomial, control = glm.control(epsilon = 1e-14, maxit = 100)))
      est <- coef(fit)[2]; se <- sqrt(vcov(fit)[2, 2])
      put(paste0(k, "|woolf_ci_1"), exp(est - z * se)); put(paste0(k, "|woolf_ci_2"), exp(est + z * se))
    }
    put(paste0(k, "|woolf_ci"), 100 * level)
    lo <- max(0, round(a) + round(b) - round(b) - round(d)); hi <- min(round(a) + round(b), round(a) + round(c))
    # the exact method is for no more than a million values of the first count: the report says when it is not used
    if (hi - lo + 1 <= 1000000) { conditional(k, t, level); done <- TRUE }
    put(paste0(k, "|note.rows"), as.numeric(!done))
    if (!done) put(paste0(k, "|note.1|note"), "The table is too large for the exact method of the odds ratio, which considers no more than a million values of the first count.")
  }
  if (study == 1 && b > 0) {
    k <- paste0(key, "|relrisk.1")
    put(paste0(k, "|ratio"), (a / r) / (b / s))
    put(paste0(k, "|dif"), a / r - b / s)
    # the limits are of the counts as entered, whole numbers or not, as the risk ratio and the difference are
    l <- if (c > 0 && d > 0) try(koopman(a, r, b, s, level), silent = TRUE) else NULL
    if (!is.null(l) && !inherits(l, "try-error")) { put(paste0(k, "|koopman_from"), l[1]); put(paste0(k, "|koopman_to"), l[2]) }
    l <- try(miettinen(a, r, b, s, level), silent = TRUE)
    if (!inherits(l, "try-error")) { put(paste0(k, "|miettinen_from"), l[1]); put(paste0(k, "|miettinen_to"), l[2]) }
    rr <- (a / r) / (b / s)
    put(paste0(k, "|exposure.rows"), as.numeric(rr > 1))
    if (rr > 1) {
      pe <- r / n
      put(paste0(k, "|exposure.1|pe"), 100 * pe)
      # the attributable risk of the cohort is the share of the outcomes that would not have been with the risk of the unexposed
      share <- function(w) 1 - (w[2] / (w[2] + w[4])) / ((w[1] + w[2]) / sum(w))
      par <- share(c(a, b, c, d))
      put(paste0(k, "|exposure.1|par"), 100 * par)
      # its variance from the variances and covariances of the four counts of one sample (the delta method), with the slopes
      # that R works out from the expression
      w <- c(a, b, c, d)
      of <- expression(1 - (w2 / (w2 + w4)) / ((w1 + w2) / (w1 + w2 + w3 + w4)))
      at <- list(w1 = a, w2 = b, w3 = c, w4 = d)
      slope <- sapply(c("w1", "w2", "w3", "w4"), function(name) eval(D(of[[1]], name), at))
      pr <- w / n
      v <- as.numeric(t(slope) %*% (n * (diag(pr) - pr %o% pr)) %*% slope)
      put(paste0(k, "|exposure.1|walter_from"), 100 * (par - z * sqrt(v))); put(paste0(k, "|exposure.1|walter_to"), 100 * (par + z * sqrt(v)))
    }
  }
  # Fisher's exact test: if the observations or the expected counts are few, if it is asked for, and if the exact method of the
  # odds ratio, which has its P values, was not used
  exact <- !done && (small || asked == 1 || study == 0)
  put(paste0(key, "|fisher.rows"), as.numeric(exact))
  if (exact) fisher(paste0(key, "|fisher.1"), t)
}

trend <- function(s, n, v) {
  # the chi-square for trend is the sum of squares of the regression of the proportions on the scores, with the weights of the
  # numbers of the rows over the variance of one observation
  p <- sum(s) / sum(n)
  w <- n / (p * (1 - p))
  fit <- lm(I(s / n) ~ v, weights = w)
  anova(fit)["v", "Sum Sq"]
}

chi2k <- function(key, type, k, rows) {
  s <- rows[seq(1, 3 * k, 3)]; f <- rows[seq(2, 3 * k, 3)]; v <- rows[seq(3, 3 * k, 3)]
  n <- s + f
  if (k < 2 || any(n <= 0)) { put(paste0(key, "|ok"), 0); return(invisible(NULL)) }
  put(paste0(key, "|ok"), 1)
  o <- cbind(s, f)
  e <- outer(rowSums(o), colSums(o)) / sum(o)
  for (i in 1:k) {
    r <- paste0(key, "|row.", i)
    put(paste0(r, "|obs_succ"), s[i]); put(paste0(r, "|obs_fail"), f[i]); put(paste0(r, "|obs_tot"), n[i]); put(paste0(r, "|obs_pc"), 100 * s[i] / n[i])
    put(paste0(r, "|exp_succ"), e[i, 1]); put(paste0(r, "|exp_fail"), e[i, 2])
    put(paste0(r, "|score.rows"), as.numeric(type != 0))
    if (type != 0) put(paste0(r, "|score.1|score"), v[i])
  }
  put(paste0(key, "|row.rows"), k)
  put(paste0(key, "|tot_succ"), sum(s)); put(paste0(key, "|tot_fail"), sum(f)); put(paste0(key, "|tot_tot"), sum(n)); put(paste0(key, "|tot_pc"), 100 * sum(s) / sum(n))
  few <- sum(e < 5)
  put(paste0(key, "|warn.rows"), as.numeric(few > 0))
  if (few > 0) { put(paste0(key, "|warn.1|num"), few); put(paste0(key, "|warn.1|den"), 2 * k) }
  # what the report says when it has no test: a column with nothing has no chi-square, and scores that are all the same no trend
  none <- sum(s) == 0 || sum(f) == 0
  same <- type != 0 && length(unique(v)) < 2
  put(paste0(key, "|note.rows"), as.numeric(none || same))
  if (none) put(paste0(key, "|note.1|note"), paste0("Chi-square can not be calculated: there are no ", if (sum(s) == 0) "successes" else "failures", "."))
  if (!none && same) put(paste0(key, "|note.1|note"), "The scores are all the same: there is no trend to test.")
  put(paste0(key, "|z.rows"), as.numeric(type != 0 && !none && !same))
  if (none) return(invisible(list(s = s, n = n, v = v, t2 = NA)))
  x2 <- sum((o - e)^2 / e)
  put(paste0(key, "|chi"), x2); put(paste0(key, "|chi_abs"), sqrt(x2)); put(paste0(key, "|totdf"), k - 1); put(paste0(key, "|chi_p"), upper(x2, k - 1))
  if (type == 0 || same) return(invisible(list(s = s, n = n, v = v, t2 = NA)))
  t2 <- if (k == 2) x2 else trend(s, n, v)
  z <- paste0(key, "|z.1")
  put(paste0(z, "|chi_lin"), t2); put(paste0(z, "|chi_1df"), sqrt(t2)); put(paste0(z, "|chi_lin_p"), upper(t2, 1))
  put(paste0(z, "|non.rows"), as.numeric(k > 2))
  if (k > 2) {
    rest <- x2 - t2
    if (abs(rest) < 1e-9 * x2) rest <- 0
    put(paste0(z, "|non.1|chi_non"), rest); put(paste0(z, "|non.1|df"), k - 2); put(paste0(z, "|non.1|chi_non_p"), upper(rest, k - 2))
  }
  invisible(list(s = s, n = n, v = v, t2 = t2))
}

# the P value of the chi-square for trend from every table that has the totals of the one observed
exact2k <- function(key, got) {
  s <- got$s; n <- got$n; v <- got$v; k <- length(n)
  A <- sum(s); N <- sum(n)
  stat <- function(a) {
    p <- A / N
    t <- sum(v * a) - A * sum(n * v) / N
    t^2 / (p * (1 - p) * (sum(n * v^2) - sum(n * v)^2 / N))
  }
  total <- 0; above <- 0; same <- 0
  observed <- stat(s)
  walk <- function(i, a, left, logp) {
    if (i == k) {
      if (left > n[k]) return(invisible())
      a[k] <- left
      pr <- exp(logp + lchoose(n[k], left) - lchoose(N, A))
      total <<- total + pr
      x <- stat(a)
      if (x >= observed * (1 - 1e-9)) above <<- above + pr
      if (abs(x - observed) <= 1e-9 * observed && any(a != s)) same <<- same + pr
      return(invisible())
    }
    for (x in 0:min(n[i], left)) { a[i] <- x; walk(i + 1, a, left - x, logp + lchoose(n[i], x)) }
  }
  walk(1, numeric(k), A, 0)
  put(paste0(key, "|sim|p_exact"), above)
  put(paste0(key, "|sim|p_same"), same)
  put(paste0(key, "|sim|p_total"), total)
}

tables <- function(v, k) lapply(1:k, function(j) v[(4 * j - 3):(4 * j)])

woolf <- function(key, level, show, k, v, worksheet) {
  ts <- if (worksheet) lapply(tables(v, k), function(t) c(t[2], t[1] - t[2], t[4], t[3] - t[4])) else tables(v, k)
  if (any(sapply(ts, function(t) t[1] + t[2] <= 0 || t[3] + t[4] <= 0))) { put(paste0(key, "|ok"), 0); return(invisible()) }
  put(paste0(key, "|ok"), 1)
  z <- qnorm(1 - (1 - level) / 2)
  put(paste0(key, "|pc"), 100 * level)
  one <- function(k1, names, y, var) {
    # names: what the figures are called
    put(paste0(k1, "|", names[1]), exp(y)); put(paste0(k1, "|", names[2]), y); put(paste0(k1, "|", names[3]), var); put(paste0(k1, "|", names[4]), sqrt(var))
  }
  plain <- list(y = numeric(0), w = numeric(0)); haldane <- list(y = numeric(0), w = numeric(0))
  put(paste0(key, "|table.rows"), if (show) k else 0)
  for (j in 1:k) {
    t <- ts[[j]]; a <- t[1]; b <- t[2]; c <- t[3]; d <- t[4]
    tk <- paste0(key, "|table.", j)
    p <- a + b; q <- c + d; r <- a + c; s <- b + d; n <- p + q
    whole <- all(t > 0)
    if (whole) { y <- log(a * d / (b * c)); var <- 1 / a + 1 / b + 1 / c + 1 / d; plain$y <- c(plain$y, y); plain$w <- c(plain$w, 1 / var) }
    yh <- log((a + 0.5) * (d + 0.5) / ((b + 0.5) * (c + 0.5))); varh <- 1 / (a + 1) + 1 / (b + 1) + 1 / (c + 1) + 1 / (d + 1)
    haldane$y <- c(haldane$y, yh); haldane$w <- c(haldane$w, 1 / varh)
    if (!show) next
    put(paste0(tk, "|table"), j)
    put(paste0(tk, "|pc_a"), 100 * a / p); put(paste0(tk, "|pc_c"), 100 * c / q); put(paste0(tk, "|pc_t"), 100 * r / n)
    o <- matrix(c(a, c, b, d), 2); e <- outer(rowSums(o), colSums(o)) / n
    put(paste0(tk, "|warn_small.rows"), as.numeric(any(e < 5)))
    put(paste0(tk, "|warn_zero_column.rows"), as.numeric(r <= 0 || s <= 0))
    if (r > 0 && s > 0) {
      x2 <- sum((o - e)^2 / e); sgn <- sign(a * d - b * c)
      put(paste0(tk, "|table_chi_2"), x2); put(paste0(tk, "|table_chi"), sgn * sqrt(x2))
      y2 <- sum(pmax(0, abs(o - e) - 0.5)^2 / e)
      put(paste0(tk, "|yates_chi_2"), y2); put(paste0(tk, "|yates_chi"), sgn * sqrt(y2)); put(paste0(tk, "|yates_chi_p"), upper(y2, 1))
    }
    put(paste0(tk, "|no_haldane.rows"), as.numeric(whole)); put(paste0(tk, "|warn_no_haldane.rows"), as.numeric(!whole))
    give <- function(k1, y, var) {
      put(paste0(k1, "|odds"), exp(y)); put(paste0(k1, "|log"), y); put(paste0(k1, "|var"), var); put(paste0(k1, "|se"), sqrt(var)); put(paste0(k1, "|weight"), 1 / var)
      put(paste0(k1, "|chi_2"), y^2 / var); put(paste0(k1, "|chi"), y / sqrt(var)); put(paste0(k1, "|chi_p"), upper(y^2 / var, 1))
      put(paste0(k1, "|pc"), 100 * level)
      put(paste0(k1, "|ci_from"), y - z * sqrt(var)); put(paste0(k1, "|ci_to"), y + z * sqrt(var))
      put(paste0(k1, "|odds_from"), exp(y - z * sqrt(var))); put(paste0(k1, "|odds_to"), exp(y + z * sqrt(var)))
    }
    if (whole) give(paste0(tk, "|no_haldane.1"), y, var)
    put(paste0(tk, "|haldane.rows"), 1)
    give(paste0(tk, "|haldane.1"), yh, varh)
  }
  # the tables together: the mean of the log odds ratios with the weights, which is the fit of a constant by weighted least squares;
  # the chi-square for heterogeneity is what that fit leaves
  pooled <- function(k1, set, x) {
    fit <- lm(set$y ~ 1, weights = set$w)
    m <- unname(coef(fit)[1]); var <- 1 / sum(set$w)
    put(paste0(k1, "|tables", x), length(set$y))
    put(paste0(k1, "|mean", x), m); put(paste0(k1, "|odds", x), exp(m)); put(paste0(k1, "|var", x), var); put(paste0(k1, "|se", x), sqrt(var))
    put(paste0(k1, "|ci_from", x), m - z * sqrt(var)); put(paste0(k1, "|ci_to", x), m + z * sqrt(var))
    put(paste0(k1, "|odds_from", x), exp(m - z * sqrt(var))); put(paste0(k1, "|odds_to", x), exp(m + z * sqrt(var)))
    put(paste0(k1, "|chi_2", x), m^2 / var); put(paste0(k1, "|chi", x), m / sqrt(var)); put(paste0(k1, "|chi_p", x), upper(m^2 / var, 1))
    het <- sum(set$w * residuals(fit)^2)
    put(paste0(k1, "|het_chi_2", x), het); put(paste0(k1, "|df", x), length(set$y) - 1); put(paste0(k1, "|het_chi_p", x), upper(het, length(set$y) - 1))
  }
  both <- k > 1 && length(plain$y) == k
  put(paste0(key, "|combined_no_haldane.rows"), as.numeric(both))
  if (both) pooled(paste0(key, "|combined_no_haldane.1"), plain, "")
  put(paste0(key, "|combined_with_haldane.rows"), as.numeric(k > 1))
  if (k > 1) pooled(paste0(key, "|combined_with_haldane.1"), haldane, "x")
}

# the test of Mantel and Haenszel and the pooled odds ratio, as R gives them for the tables that can be pooled
mantel <- function(key, level, exact, k, v) {
  ts <- tables(v, k)
  # the program has the first row of a table as the first group: a, c of R's table are its first row
  use <- Filter(function(t) (t[1] + t[2]) > 0 && (t[3] + t[4]) > 0 && (t[1] + t[3]) > 0 && (t[2] + t[4]) > 0, ts)
  if (length(use) == 0) return(invisible())
  arr <- array(unlist(lapply(use, function(t) c(t[1], t[3], t[2], t[4]))), c(2, 2, length(use)))
  if (length(use) > 1) {
    m <- suppressWarnings(mantelhaen.test(arr, correct = TRUE, conf.level = level))
    # the chi-square is of the counts as they are
    put(paste0(key, "|chi_mantel"), unname(m$statistic)); put(paste0(key, "|chi_p"), m$p.value)
    # R pools the counts as they are: the program does so if no table has an empty cell
    if (all(v > 0)) { put(paste0(key, "|odds"), unname(m$estimate)); put(paste0(key, "|from"), m$conf.int[1]); put(paste0(key, "|to"), m$conf.int[2]) }
  }
}

done <- 0
for (line in lines) {
  f <- strsplit(line, "\t")[[1]]
  v <- as.numeric(f[-(1:2)])
  key <- paste0(f[1], "|", f[2])
  started <- proc.time()[3]
  result <- try({
    if (f[1] == "chi22") chi22(key, v[1:4], v[5], v[6], v[7])
    if (f[1] == "chi2k") chi2k(key, v[1], v[2], v[-(1:2)])
    if (f[1] == "sim2k") { got <- chi2k(key, v[1], v[2], v[-(1:5)]); if (!is.null(got)) exact2k(key, got) }
    if (f[1] == "woolf") woolf(key, v[1], v[2] == 1, v[3], v[-(1:3)], FALSE)
    if (f[1] == "woolfws") woolf(key, v[1], v[2] == 1, v[3], v[-(1:3)], TRUE)
    if (f[1] == "mantel") mantel(key, v[1], v[2], v[3], v[-(1:3)])
  }, silent = TRUE)
  if (inherits(result, "try-error")) cat("FAILED", key, ":", as.character(result))
  done <- done + 1
  if (proc.time()[3] - started > 5) cat(key, round(proc.time()[3] - started), "s\n")
  if (done %% 100 == 0) cat(done, "cases\n")
}
writeLines(out, args[2])
cat(length(out), "figures\n")
