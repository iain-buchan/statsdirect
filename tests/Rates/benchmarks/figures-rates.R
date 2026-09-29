# Benchmarks of the Rates menu, worked out from the definitions.
# usage: Rscript --vanilla figures-rates.R cases-rates.txt r-rates.txt
args <- commandArgs(trailingOnly = TRUE)
lines <- readLines(args[1])
out <- character(0)
put <- function(key, value) out <<- c(out, paste0(key, "\t", if (is.character(value)) paste0("\"", value, "\"") else if (is.na(value)) "NA" else if (is.infinite(value)) (if (value > 0) "Inf" else "-Inf") else format(value, digits = 15, scientific = TRUE)))
numbers <- function(text) suppressWarnings(as.numeric(strsplit(text, ",")[[1]]))

# The limits of the mean of a Poisson count: the mean with which as many events or more have the probability alpha / 2, and that
# with which as many or fewer have it; by the root of the probability
poisson_limits <- function(y, alpha) {
  lower <- if (y == 0) 0 else qgamma(alpha / 2, y)
  upper <- qgamma(1 - alpha / 2, y + 1)
  # for a whole number of events the limits are put back into the probabilities that define them
  if (y == floor(y) && y < 1e8) {
    if (y > 0 && abs(ppois(y - 1, lower, lower.tail = FALSE) / (alpha / 2) - 1) > 1e-9) stop("the lower limit of a Poisson mean is not the root of its probability")
    if (abs(ppois(y, upper) / (alpha / 2) - 1) > 1e-9) stop("the upper limit of a Poisson mean is not the root of its probability")
  }
  c(lower, upper)
}
# The limits of the ratio of two rates, with the events of both given: the first count is binomial, with the proportion
# ratio * pt1 / (ratio * pt1 + pt2), whose limits are those of Clopper and Pearson
ratio_limits <- function(a, b, pt1, pt2, alpha) {
  lower <- if (a == 0) 0 else { p <- qbeta(alpha / 2, a, b + 1); p / (1 - p) * pt2 / pt1 }
  upper <- if (b == 0) Inf else { p <- qbeta(1 - alpha / 2, a + 1, b); p / (1 - p) * pt2 / pt1 }
  c(lower, upper)
}
# the limits of the ratio of two proportions by the score method of Koopman: the ratios at which the score statistic has the
# value of chi-square for the confidence level
koopman <- function(x1, n1, x0, n0, level) {
  chi2 <- function(theta) {
    A <- (n0 + n1) * theta
    B <- -((x0 + n1) * theta + x1 + n0)
    p0 <- 2 * (x0 + x1) / (-B + sqrt(B^2 - 4 * A * (x0 + x1)))
    p1 <- theta * p0
    (x1 - n1 * p1)^2 / (n1 * p1 * (1 - p1)) * (1 + n1 * (theta - p1) / (n0 * (1 - p1)))
  }
  f <- function(lt) chi2(exp(lt)) - qchisq(level, 1)
  est <- if (x1 == 0 || x0 == 0) NA else (x1 / n1) / (x0 / n0)
  lower <- if (x1 == 0) 0 else exp(uniroot(f, c((if (x0 == 0) log(x1 / n1) else log(est)) - 40, if (x0 == 0) log((x1 / n1) / (0.5 / n0)) else log(est)), tol = 1e-13)$root)
  upper <- if (x0 == 0) Inf else exp(uniroot(f, c(if (x1 == 0) log((0.5 / n1) / (x0 / n0)) else log(est), (if (x1 == 0) log((0.5 / n1) / (x0 / n0)) else log(est)) + 40), tol = 1e-13)$root)
  c(lower, upper)
}
clopper <- function(r, n, level) c(if (r == 0) 0 else qbeta((1 - level) / 2, r, n - r + 1), if (r == n) 1 else qbeta(1 - (1 - level) / 2, r + 1, n - r))
one_sided <- function(level) paste0(" [", format(round(100 * (level + (1 - level) / 2), 1), nsmall = 0), "% one-sided CI]")

two <- function(key, level, a, b, pt1, pt2, cml) {
  if (a + b <= 0 || pt1 <= 0 || pt2 <= 0 || (cml == 1 && (a != floor(a) || b != floor(b)))) { put(paste0(key, "|ok"), 0); return() }
  put(paste0(key, "|ok"), 1)
  alpha <- 1 - level; z <- qnorm(1 - alpha / 2)
  m <- a + b; pt <- pt1 + pt2
  ir1 <- a / pt1; ir2 <- b / pt2; ird <- ir1 - ir2
  put(paste0(key, "|ir1"), ir1); put(paste0(key, "|ir2"), ir2); put(paste0(key, "|ird"), ird); put(paste0(key, "|pc"), 100 * level)
  put(paste0(key, "|m"), m); put(paste0(key, "|pt"), pt)
  # the chi-square of the events of the first group against what the person-time expects of them, and the limits that it gives
  xmh <- (a - m * pt1 / pt)^2 / (m * pt1 * pt2 / pt^2)
  put(paste0(key, "|xmh"), xmh); put(paste0(key, "|p"), pchisq(xmh, 1, lower.tail = FALSE))
  # the limits of the difference: each rate has the variance of a Poisson count over its person-time
  se <- sqrt(ir1 / pt1 + ir2 / pt2)
  put(paste0(key, "|ird_from"), ird - z * se); put(paste0(key, "|ird_to"), ird + z * se)
  put(paste0(key, "|irr"), if (b == 0) Inf else ir1 / ir2)
  l <- ratio_limits(a, b, pt1, pt2, alpha)
  put(paste0(key, "|irr_from"), l[1]); put(paste0(key, "|irr_to"), l[2])
  put(paste0(key, "|faults"), 0)
  if (cml == 1) {
    k <- paste0(key, "|exact.1")
    put(paste0(k, "|eor"), if (a == 0) 0 else if (b == 0) Inf else ir1 / ir2)
    put(paste0(k, "|llf"), l[1]); put(paste0(k, "|ulf"), l[2])
    # the mid-P limits: the proportion with which the tail, with half the probability of the count observed, is alpha / 2
    tail <- function(p, upper) if (upper) pbinom(a, m, p, lower.tail = FALSE) + 0.5 * dbinom(a, m, p) else pbinom(a - 1, m, p) + 0.5 * dbinom(a, m, p)
    root <- function(upper) { u <- uniroot(function(x) tail(plogis(x), upper) - alpha / 2, c(-60, 60), tol = 1e-14)$root; exp(u) * pt2 / pt1 }
    put(paste0(k, "|llm"), if (a == 0) 0 else root(TRUE)); put(paste0(k, "|ulm"), if (b == 0) Inf else root(FALSE))
    p0 <- pt1 / pt
    d <- dbinom(0:m, m, p0)
    up <- pbinom(a - 1, m, p0, lower.tail = FALSE); down <- pbinom(a, m, p0); own <- dbinom(a, m, p0)
    put(paste0(k, "|p1f"), min(up, down)); put(paste0(k, "|p2f"), min(1, sum(d[d <= own * (1 + 1e-7)])))
    mid <- min(up, down) - own / 2
    put(paste0(k, "|p1m"), mid); put(paste0(k, "|p2m"), min(1, 2 * mid))
  }
}

smr <- function(key, level, nunit, dead, rates, times) {
  keep <- !is.na(rates) & !is.na(times)
  r <- rates[keep] / nunit; t <- times[keep]
  e <- sum(r * t)
  if (e <= 0) { put(paste0(key, "|ok"), 0); return() }
  put(paste0(key, "|ok"), 1)
  alpha <- 1 - level
  put(paste0(key, "|groups.rows"), length(r))
  for (i in seq_along(r)) { k <- paste0(key, "|groups.", i); put(paste0(k, "|group"), r[i]); put(paste0(k, "|observed"), t[i]); put(paste0(k, "|expected"), r[i] * t[i]) }
  put(paste0(key, "|total"), e); put(paste0(key, "|ratio"), dead / e); put(paste0(key, "|smr"), floor(100 * dead / e + 0.5)); put(paste0(key, "|pc"), 100 * level)
  l <- poisson_limits(dead, alpha) / e
  put(paste0(key, "|from"), l[1]); put(paste0(key, "|to"), l[2])
  put(paste0(key, "|from100"), round(100 * l[1])); put(paste0(key, "|to100"), round(100 * l[2]))
  put(paste0(key, "|qty"), dead)
  put(paste0(key, "|p_hi"), ppois(dead - 1, e, lower.tail = FALSE)); put(paste0(key, "|p_lo"), ppois(dead, e))
}

direct <- function(key, level, nunit, events, times, ref) {
  keep <- !is.na(events) & !is.na(times) & !is.na(ref)
  y <- events[keep]; n <- times[keep]; N <- ref[keep]
  if (any(n <= 0) || sum(N) <= 0) { put(paste0(key, "|ok"), 0); return() }
  put(paste0(key, "|ok"), 1)
  alpha <- 1 - level; z <- qnorm(1 - alpha / 2)
  r <- y / n; w <- N / sum(N)
  put(paste0(key, "|units"), if (nunit == 1) "1 unit" else paste0(format(nunit, scientific = FALSE), " units"))
  put(paste0(key, "|inputs.rows"), length(y)); put(paste0(key, "|cis.rows"), length(y)); put(paste0(key, "|pc"), 100 * level)
  for (i in seq_along(y)) {
    k <- paste0(key, "|inputs.", i)
    put(paste0(k, "|idxy"), y[i]); put(paste0(k, "|idxn"), n[i]); put(paste0(k, "|idxr"), nunit * r[i]); put(paste0(k, "|refn"), N[i]); put(paste0(k, "|refw"), w[i])
    k <- paste0(key, "|cis.", i)
    l <- poisson_limits(y[i], alpha) / n[i]
    put(paste0(k, "|idxr"), nunit * r[i]); put(paste0(k, "|from"), nunit * l[1]); put(paste0(k, "|to"), nunit * l[2])
  }
  dsr <- sum(w * r)
  put(paste0(key, "|events"), sum(y)); put(paste0(key, "|stde"), dsr * sum(n)); put(paste0(key, "|crude"), nunit * sum(y) / sum(n)); put(paste0(key, "|stdr"), nunit * dsr)
  # the variance of a weighted mean of rates, each rate with the variance of a proportion, or of a Poisson count over its time
  small <- sqrt(sum(w^2 * r / n))
  # a stratum with more events than person-time has a rate above 1, which a proportion cannot have: the binomial model then has
  # no figures, and the report has a note
  if (all(y <= n)) {
    any <- sqrt(sum(w^2 * r * (1 - r) / n))
    put(paste0(key, "|ser_any"), nunit * any); put(paste0(key, "|from_any"), nunit * (dsr - z * any)); put(paste0(key, "|to_any"), nunit * (dsr + z * any))
    put(paste0(key, "|note.rows"), 0)
  } else {
    put(paste0(key, "|ser_any"), NA); put(paste0(key, "|from_any"), NA); put(paste0(key, "|to_any"), NA)
    put(paste0(key, "|note.rows"), 1)
  }
  put(paste0(key, "|ser_small"), nunit * small); put(paste0(key, "|from_small"), nunit * (dsr - z * small)); put(paste0(key, "|to_small"), nunit * (dsr + z * small))
  # Dobson: the limits of the count of all the events, put on the scale of the rate
  Y <- sum(y)
  if (Y > 0) {
    l <- poisson_limits(Y, alpha)
    put(paste0(key, "|from_dobson"), nunit * (dsr + sqrt(small^2 / Y) * (l[1] - Y))); put(paste0(key, "|to_dobson"), nunit * (dsr + sqrt(small^2 / Y) * (l[2] - Y)))
  } else { put(paste0(key, "|from_dobson"), NA); put(paste0(key, "|to_dobson"), NA) }
}

stdrr <- function(key, level, nunit, model, a, pt1, b, pt2, ref) {
  keep <- !is.na(a) & !is.na(pt1) & !is.na(b) & !is.na(pt2) & !is.na(ref)
  a <- a[keep]; pt1 <- pt1[keep]; b <- b[keep]; pt2 <- pt2[keep]; ref <- ref[keep]
  put(paste0(key, "|ok"), 1)
  alpha <- 1 - level; z <- qnorm(1 - alpha / 2)
  poisson <- model == "poisson"
  k <- length(a)
  put(paste0(key, "|strata.rows"), k); put(paste0(key, "|rates.rows"), k + 1); put(paste0(key, "|pc"), 100 * level)
  put(paste0(key, "|method"), if (poisson) "exact Poisson" else "Koopman")
  put(paste0(key, "|model_out"), if (poisson) "Poisson (small rates)" else "Binomial")
  put(paste0(key, "|units"), if (nunit == 1) "1 unit" else paste0(format(nunit, scientific = FALSE), " units"))
  ratio <- function(x, t1, y, t2) {
    # the ratio of the rates and its limits; nothing if neither group has an event
    if (x + y <= 0) return(c(NA, NA, NA))
    l <- if (poisson) ratio_limits(x, y, t1, t2, alpha) else koopman(x, t1, y, t2, level)
    c(if (y == 0) Inf else (x / t1) / (y / t2), l)
  }
  for (i in 1:k) {
    s <- paste0(key, "|rates.", i)
    v <- ratio(a[i], pt1[i], b[i], pt2[i])
    put(paste0(s, "|rr"), v[1]); put(paste0(s, "|lci"), v[2]); put(paste0(s, "|uci"), v[3])
    put(paste0(s, "|wt"), ref[i] / sum(ref))
  }
  A <- sum(a); B <- sum(b); T1 <- sum(pt1); T2 <- sum(pt2)
  v <- ratio(A, T1, B, T2)
  s <- paste0(key, "|rates.", k + 1)
  put(paste0(s, "|rr"), v[1]); put(paste0(s, "|lci"), v[2]); put(paste0(s, "|uci"), v[3])
  # the crude rates
  crude <- function(name, x, t) {
    put(paste0(key, "|", name), nunit * x / t)
    if (poisson) { l <- poisson_limits(x, alpha) / t; put(paste0(key, "|", name, "_warn"), "") }
    else { l <- clopper(x, t, level); put(paste0(key, "|", name, "_warn"), if (x == 0 || x == t) one_sided(level) else "") }
    put(paste0(key, "|", name, "_from"), nunit * l[1]); put(paste0(key, "|", name, "_to"), nunit * l[2])
  }
  crude("cre", A, T1); crude("crne", B, T2)
  # the standardized rates: the strata of the reference population that have a size
  w <- ref / sum(ref)
  standard <- function(name, x, t) {
    r <- x / t
    dsr <- sum(w * r)
    v <- if (poisson) sum(w^2 * x / t^2) else sum(w^2 * r * (1 - r) / t)
    put(paste0(key, "|", name), nunit * dsr); put(paste0(key, "|", name, "_from"), nunit * (dsr - z * sqrt(v))); put(paste0(key, "|", name, "_to"), nunit * (dsr + z * sqrt(v)))
    c(dsr, v)
  }
  e <- standard("sre", a, pt1); n <- standard("srne", b, pt2)
  if (n[1] > 0 && e[1] > 0) {
    srr <- e[1] / n[1]
    v <- e[2] / e[1]^2 + n[2] / n[1]^2
    put(paste0(key, "|srr"), srr); put(paste0(key, "|srr_from"), exp(log(srr) - z * sqrt(v))); put(paste0(key, "|srr_to"), exp(log(srr) + z * sqrt(v)))
  } else if (n[1] > 0) {
    # no events in the first population: the ratio is 0, and the method of the logarithm has no limits for it
    put(paste0(key, "|srr"), 0); put(paste0(key, "|srr_from"), NA); put(paste0(key, "|srr_to"), NA)
  } else { put(paste0(key, "|srr"), if (e[1] > 0) Inf else NA); put(paste0(key, "|srr_from"), NA); put(paste0(key, "|srr_to"), NA) }
}

rate <- function(key, level, events, time) {
  put(paste0(key, "|ok"), 1)
  l <- poisson_limits(events, 1 - level) / time
  put(paste0(key, "|events"), events); put(paste0(key, "|time"), time); put(paste0(key, "|rate"), events / time); put(paste0(key, "|pc"), 100 * level)
  put(paste0(key, "|from"), l[1]); put(paste0(key, "|to"), l[2])
}

done <- 0
for (line in lines) {
  f <- strsplit(line, "\t")[[1]]
  key <- paste0(f[1], "|", f[2])
  level <- as.numeric(f[3])
  result <- try({
    if (f[1] == "two") two(key, level, as.numeric(f[4]), as.numeric(f[5]), as.numeric(f[6]), as.numeric(f[7]), as.numeric(f[8]))
    if (f[1] == "smr") smr(key, level, as.numeric(f[4]), as.numeric(f[5]), numbers(f[6]), numbers(f[7]))
    if (f[1] == "direct") direct(key, level, as.numeric(f[4]), numbers(f[5]), numbers(f[6]), numbers(f[7]))
    if (f[1] == "stdrr") stdrr(key, level, as.numeric(f[4]), f[5], numbers(f[6]), numbers(f[7]), numbers(f[8]), numbers(f[9]), numbers(f[10]))
    if (f[1] == "rate") rate(key, level, as.numeric(f[4]), as.numeric(f[5]))
  }, silent = TRUE)
  if (inherits(result, "try-error")) cat("FAILED", key, ":", as.character(result))
  done <- done + 1
}
writeLines(out, args[2])
cat(length(out), "figures of", done, "cases\n")
