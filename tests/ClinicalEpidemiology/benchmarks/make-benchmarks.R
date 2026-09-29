# The Clinical Epidemiology menu: the cases that the reports are given, and what is expected of them, worked out from the
# definitions of the methods.
#  - The confidence limits of a proportion are those of Clopper and Pearson, from quantiles of the beta distribution.
#  - The limits of a ratio of two proportions (a relative risk, a likelihood ratio) are the score limits of Koopman: the ratios
#    at which the two proportions that are most likely with that ratio are as far from those observed as the normal deviate
#    of the confidence level allows.  They are found here as the roots of the score, between ends that are far apart.
#  - The limits of a difference of two proportions are the score limits of Miettinen and Nurminen, found in the same way.
#  - The conditional estimate of an odds ratio, its exact limits (Fisher and mid-P) and the exact P values are from the chances
#    of the first count of the table with its totals given, which are those of the hypergeometric distribution multiplied by
#    the odds ratio to the power of the count.
#  - The variance of a population attributable risk is that of the logarithm of 1 less the risk, from the counts of the sample.
#
# A case (cases.txt): the report, a name, and the inputs as key=value, with semicolons between them; "data" has the columns of
# a table, with bars between the columns and commas between the numbers.
# What is expected (expected.txt): "report|name|output", and the figure; for the rows of a list of the report the output is
# "*list.rows" for their number and "*list[i].output" for an output of row i; "refused" has words of the refusal; "missing" is
# a figure that there is none of; a number needed to treat is a figure and "_benefit" or "_harm".
#
# usage: Rscript --vanilla make-benchmarks.R     (in this folder; it takes a few seconds)
args <- c("cases.txt", "expected.txt")
cases <- character(0); expected <- character(0); count <- 0
num <- function(x) if (is.character(x)) x else if (is.na(x)) "missing" else if (is.infinite(x)) (if (x > 0) "Infinity" else "-Infinity") else format(x, digits = 17, scientific = abs(x) < 1e-4 && x != 0 || abs(x) >= 1e15, trim = TRUE)
case <- function(.report, .inputs, ...) {
  count <<- count + 1
  name <- sprintf("c%04d", count)
  cases <<- c(cases, paste(.report, name, paste0(names(.inputs), "=", sapply(.inputs, num), collapse = ";"), sep = "\t"))
  want <- list(...)
  for (k in names(want)) expected <<- c(expected, paste0(.report, "|", name, "|", k, "\t", num(want[[k]])))
}
# ---- the limits of a proportion of Clopper and Pearson: the proportions with which the count or more, and the count or fewer,
# have half of what the confidence level leaves
exact_limits <- function(r, n, level) { h <- (1 - level) / 2; c(if (r == 0) 0 else qbeta(h, r, n - r + 1), if (r == n) 1 else qbeta(h, r + 1, n - r, lower.tail = FALSE)) }
# ---- the score limits of the ratio of two proportions, (x1 / n1) / (x0 / n0): the ratios at which the two proportions that
# are most likely with that ratio are as far from those observed as the normal deviate of the confidence level allows
score_ratio <- function(theta, x1, n1, x0, n0) {
  # the most likely proportion of the second group if the first is theta times it: the root of a quadratic
  A <- (n1 + n0) * theta; B <- -(n1 * theta + x1 + n0 + x0 * theta); C <- x1 + x0
  p0 <- 2 * C / (-B + sqrt(B^2 - 4 * A * C)); p1 <- theta * p0        # the less of the two roots, in the form that keeps its figures
  (x1 / n1 - theta * x0 / n0) / sqrt(p1 * (1 - p1) / n1 + theta^2 * p0 * (1 - p0) / n0)
}
ratio_limits <- function(x1, n1, x0, n0, level) {
  z <- qnorm(1 - (1 - level) / 2)
  if (x1 == 0 && x0 == 0) return(c(0, Inf))
  est <- (x1 / n1) / (x0 / n0)
  f <- function(lt, side) score_ratio(exp(lt), x1, n1, x0, n0) - side * z
  lower <- if (x1 == 0) 0 else exp(uniroot(f, c(-40, if (is.finite(est)) log(est) else 40), side = 1, tol = 1e-14)$root)
  upper <- if (x0 == 0) Inf else exp(uniroot(f, c(if (est > 0) log(est) else -40, 40), side = -1, tol = 1e-14)$root)
  c(lower, upper)
}
# ---- the score limits of the difference of two proportions, x1 / n1 - x0 / n0 (Miettinen and Nurminen): the differences at
# which the most likely proportions with that difference are as far from those observed as the normal deviate allows, the
# variance being multiplied by N / (N - 1)
score_difference <- function(delta, x1, n1, x0, n0) {
  N <- n1 + n0
  # the most likely proportion of the second group if the first is delta more: where the slope of the logarithm of the
  # likelihood is 0, which is the root of a cubic; the slope goes down from one end of what the proportion can be to the other
  slope <- function(p0) { p1 <- p0 + delta; (if (x1 > 0) x1 / p1 else 0) - (if (n1 - x1 > 0) (n1 - x1) / (1 - p1) else 0) + (if (x0 > 0) x0 / p0 else 0) - (if (n0 - x0 > 0) (n0 - x0) / (1 - p0) else 0) }
  lo <- max(0, -delta); hi <- min(1, 1 - delta); tiny <- 1e-14 * (hi - lo)
  p0 <- if (slope(lo + tiny) <= 0) lo else if (slope(hi - tiny) >= 0) hi else uniroot(slope, c(lo + tiny, hi - tiny), tol = 1e-16)$root
  p1 <- p0 + delta
  v <- (p1 * (1 - p1) / n1 + p0 * (1 - p0) / n0) * N / (N - 1)
  (x1 / n1 - x0 / n0 - delta) / sqrt(v)
}
difference_limits <- function(x1, n1, x0, n0, level) {
  z <- qnorm(1 - (1 - level) / 2); est <- x1 / n1 - x0 / n0
  f <- function(d, side) score_difference(d, x1, n1, x0, n0) - side * z
  c(uniroot(f, c(-1 + 1e-10, est), side = 1, tol = 1e-13)$root, uniroot(f, c(est, 1 - 1e-10), side = -1, tol = 1e-13)$root)
}
# ---- the odds ratio of a 2 by 2 table with its totals given: the chances of the first count with an odds ratio psi
first_count <- function(a, b, c, d, psi) {
  k <- max(0, a + c - (c + d)):min(a + b, a + c)
  w <- dhyper(k, a + b, c + d, a + c, log = TRUE) + k * log(psi)
  w <- exp(w - max(w)); list(k = k, p = w / sum(w))
}
odds_exact <- function(a, b, c, d, level) {
  h <- (1 - level) / 2
  tail <- function(psi, upper, mid) { f <- first_count(a, b, c, d, psi); at <- f$p[f$k == a]; (if (upper) sum(f$p[f$k >= a]) else sum(f$p[f$k <= a])) - (if (mid) at / 2 else 0) }
  solve <- function(upper, mid) exp(uniroot(function(l) tail(exp(l), upper, mid) - h, c(-40, 40), tol = 1e-13)$root)
  least <- max(0, a - d); most <- min(a + b, a + c)
  mean_at <- function(l) { f <- first_count(a, b, c, d, exp(l)); sum(f$k * f$p) - a }
  est <- if (a == least) 0 else if (a == most) Inf else exp(uniroot(mean_at, c(-40, 40), tol = 1e-13)$root)
  null <- first_count(a, b, c, d, 1); at <- null$p[null$k == a]
  lower <- sum(null$p[null$k <= a]); upper <- sum(null$p[null$k >= a])
  list(est = est,
       llf = if (a == least) 0 else solve(TRUE, FALSE), ulf = if (a == most) Inf else solve(FALSE, FALSE),
       llm = if (a == least) 0 else solve(TRUE, TRUE), ulm = if (a == most) Inf else solve(FALSE, TRUE),
       # Fisher's two sided P value is the chance of the first counts that are no more likely than the one observed; the two
       # sided mid-P value is twice the one sided
       p1f = min(lower, upper), p2f = min(1, sum(null$p[null$p <= at * (1 + 1e-7)])), p1m = min(lower, upper) - at / 2, p2m = min(1, 2 * (min(lower, upper) - at / 2)))
}
tables <- list(c(10, 5, 3, 12), c(1, 9, 8, 2), c(20, 80, 10, 90), c(3, 0, 1, 4), c(0, 5, 6, 2), c(7, 3, 0, 9), c(4, 6, 5, 0), c(150, 120, 90, 210), c(2, 1, 1, 2), c(1200, 800, 900, 1100))
levels <- c(0.95, 0.99, 0.8)

# ---- risk analysis of a retrospective (case-control) study: rows cases and controls, columns exposed and not exposed
for (t in tables) for (level in levels) for (pe in list(NULL, 0.1, 0.6)) {
  a <- t[1]; b <- t[2]; c <- t[3]; d <- t[4]; z <- qnorm(1 - (1 - level) / 2)
  or <- if (a * d == 0 && b * c == 0) NA else if (b * c == 0) Inf else a * d / (b * c)
  e <- odds_exact(a, b, c, d, level)
  inputs <- c(list(a = a, b = b, c = c, d = d, cco = level), if (is.null(pe)) list() else list(pe = pe))
  want <- list(odds = or, eor = e$est, llf = e$llf, ulf = e$ulf, llm = e$llm, ulm = e$ulm, p1f = e$p1f, p2f = e$p2f, p1m = e$p1m, p2m = e$p2m)
  if (a * d > 0 && b * c > 0) { s <- sqrt(1 / a + 1 / b + 1 / c + 1 / d); want <- c(want, list(`*power.rows` = 1, `*power[1].ci_1` = exp(log(or) - z * s), `*power[1].ci_2` = exp(log(or) + z * s))) } else want <- c(want, list(`*power.rows` = 0))
  if (!is.na(or) && or > 1 && is.finite(or)) {
    if (is.null(pe)) {
      # the exposure of the controls stands for that of the population: the fraction is 1 less the ratio of the parts of
      # cases and of controls that are not exposed, of which the logarithm has the variance a / (b m1) + c / (d m2)
      par <- 1 - (b / (a + b)) / (d / (c + d)); se <- (1 - par) * sqrt(a / (b * (a + b)) + c / (d * (c + d)))
      want <- c(want, list(`*risk.rows` = 1, `*risk[1].pe` = 100 * c / (c + d), `*risk[1].par` = 100 * par, `*risk[1].from` = 100 * (par - z * se), `*risk[1].to` = 100 * (par + z * se)))
    } else {
      f <- function(o) pe * (o - 1) / (1 + pe * (o - 1)); s <- sqrt(1 / a + 1 / b + 1 / c + 1 / d)
      want <- c(want, list(`*risk.rows` = 1, `*risk[1].pe` = 100 * pe, `*risk[1].par` = 100 * f(or), `*risk[1].from` = 100 * f(exp(log(or) - z * s)), `*risk[1].to` = 100 * f(exp(log(or) + z * s))))
    }
  } else want <- c(want, list(`*risk.rows` = 0))
  do.call(case, c(list("RptMiscRetroRisk", inputs), want))
}
# ---- risk analysis of a prospective (cohort) study: rows with and without the outcome, columns exposed and not exposed
for (t in tables) for (level in levels) for (pe in list(NULL, 0.1, 0.6)) {
  a <- t[1]; b <- t[2]; c <- t[3]; d <- t[4]; z <- qnorm(1 - (1 - level) / 2)
  n1 <- a + c; n2 <- b + d; m1 <- a + b; n <- n1 + n2
  if (a == 0 && b == 0) next
  rr <- if (b == 0) Inf else (a / n1) / (b / n2)
  rl <- ratio_limits(a, n1, b, n2, level); dl <- difference_limits(a, n1, b, n2, level)
  inputs <- c(list(a = a, b = b, c = c, d = d, cco = level), if (is.null(pe)) list() else list(pe = pe))
  want <- list(ratio = rr, koopman_from = rl[1], koopman_to = rl[2], dif = a / n1 - b / n2, miettinen_from = dl[1], miettinen_to = dl[2])
  if (rr > 1 && is.finite(rr)) {
    if (is.null(pe)) {
      # the exposure of the cohort stands for that of the population: the fraction is 1 less theta = b n / (n2 m1), of which
      # the logarithm has the variance that the four counts of a sample of n give it
      theta <- b * n / (n2 * m1)
      g <- c(-1 / m1, 1 / b - 1 / n2 - 1 / m1, 0, -1 / n2); x <- c(a, b, c, d)
      se <- theta * sqrt(sum(g^2 * x) - sum(g * x)^2 / n)
      want <- c(want, list(`*exposure.rows` = 1, `*exposure[1].pe` = 100 * n1 / n, `*exposure[1].par` = 100 * (1 - theta), `*exposure[1].walter_from` = 100 * (1 - theta - z * se), `*exposure[1].walter_to` = 100 * (1 - theta + z * se)))
    } else {
      f <- function(o) if (is.infinite(o)) 1 else pe * (o - 1) / (1 + pe * (o - 1))
      want <- c(want, list(`*exposure.rows` = 1, `*exposure[1].pe` = 100 * pe, `*exposure[1].par` = 100 * f(rr), `*exposure[1].walter_from` = 100 * f(rl[1]), `*exposure[1].walter_to` = 100 * f(rl[2])))
    }
  } else want <- c(want, list(`*exposure.rows` = 0))
  do.call(case, c(list("RptMiscRelRisk", inputs), want))
}
# ---- diagnostic test: rows test positive and negative, columns with and without the disease
for (t in tables) for (level in levels) {
  a <- t[1]; b <- t[2]; c <- t[3]; d <- t[4]; n <- a + b + c + d
  prev <- exact_limits(a + c, n, level); ppv <- exact_limits(a, a + b, level); npv <- exact_limits(d, c + d, level)
  sens <- exact_limits(a, a + c, level); spec <- exact_limits(d, b + d, level)
  lp <- ratio_limits(a, a + c, b, b + d, level); ln <- ratio_limits(c, a + c, d, b + d, level)
  e <- odds_exact(a, b, c, d, level)
  case("RptMiscDiagnostic", list(a = a, b = b, c = c, d = d, cco = level),
       prevalence = (a + c) / n, prevalence_from = prev[1], prevalence_to = prev[2],
       likely = a / (a + b), likely_from = ppv[1], likely_to = ppv[2], likely_change = 100 * (a / (a + b) - (a + c) / n),
       likely_negative = d / (c + d), likely_negative_from = npv[1], likely_negative_to = npv[2], likely_negative_change = 100 * (d / (c + d) - (b + d) / n),
       likely_despite = c / (c + d), likely_despite_from = 1 - npv[2], likely_despite_to = 1 - npv[1], likely_despite_change = 100 * (c / (c + d) - (a + c) / n),
       sensitive = a / (a + c), sensitive_from = sens[1], sensitive_to = sens[2],
       specific = d / (b + d), specific_from = spec[1], specific_to = spec[2],
       lr_pos = if (b == 0) Inf else (a / (a + c)) / (b / (b + d)), lr_pos_from = lp[1], lr_pos_to = lp[2],
       lr_neg = if (d == 0) Inf else (c / (a + c)) / (d / (b + d)), lr_neg_from = ln[1], lr_neg_to = ln[2],
       odr = if (b * c == 0) (if (a * d == 0) NA else Inf) else a * d / (b * c), cmle = e$est, cmle_from = e$llf, cmle_to = e$ulf)
}
# ---- screening test errors: the chance that a positive result is false, and that a negative result is false
for (pt in c(0.99, 0.8, 0.5, 1, 0)) for (pf in c(0.01, 0.2, 0.5, 0, 1)) for (one_in in c(1, 2, 100, 1e6)) {
  pd <- 1 / one_in
  pos <- pt * pd + pf * (1 - pd)                     # the chance of a positive result
  neg <- (1 - pt) * pd + (1 - pf) * (1 - pd)         # and of a negative result, as a sum of its own, which keeps its figures
  case("RptMiscFalseResult", list(pt = pt, pf = pf, pd = one_in), positive = if (pos == 0) NA else pf * (1 - pd) / pos, negative = if (neg == 0) NA else (1 - pt) * pd / neg)
}
# ---- likelihood ratios of a test with several results: the counts of each result with and without the feature
for (level in levels) for (t in list(list(c(10, 20, 30), c(40, 15, 5)), list(c(5, 0, 12, 3), c(1, 9, 0, 30)), list(c(200, 3), c(7, 500)), list(c(0, 4, 0), c(0, 6, 2)))) {
  plus <- t[[1]]; minus <- t[[2]]
  want <- list(`*row.rows` = length(plus))
  for (i in seq_along(plus)) {
    l <- ratio_limits(plus[i], sum(plus), minus[i], sum(minus), level)
    lr <- if (minus[i] == 0) (if (plus[i] == 0) NA else Inf) else (plus[i] / sum(plus)) / (minus[i] / sum(minus))
    want[[paste0("*row[", i, "].likely")]] <- lr; want[[paste0("*row[", i, "].from")]] <- l[1]; want[[paste0("*row[", i, "].to")]] <- l[2]
  }
  do.call(case, c(list("RptMiscLikely", list(data = paste0(paste(plus, collapse = ","), "|", paste(minus, collapse = ",")), z1 = level)), want))
}
# ---- number needed to treat: the treated and the controls, and those of each with the event
nn <- function(x, what) if (is.na(x)) "*" else paste0(if (is.infinite(x)) "Infinity" else format(abs(x), digits = 17), if (x < 0) "_harm" else "_benefit")
for (t in list(c(100, 10, 100, 20), c(50, 20, 60, 12), c(30, 0, 30, 6), c(30, 5, 30, 0), c(1000, 15, 800, 16), c(25, 25, 25, 20), c(40, 8, 40, 8))) for (level in levels) for (brr in list(NULL, 0.3, 5)) {
  nt <- t[1]; xt <- t[2]; nc <- t[3]; xc <- t[4]; pt <- xt / nt; pc <- xc / nc
  ce <- exact_limits(xc, nc, level); te <- exact_limits(xt, nt, level)
  rl <- ratio_limits(xt, nt, xc, nc, level); rn <- ratio_limits(nt - xt, nt, nc - xc, nc, level)
  dl <- difference_limits(xc, nc, xt, nt, level)
  e <- odds_exact(xt, nt - xt, xc, nc - xc, level)
  or <- if (xt * (nc - xc) == 0 && (nt - xt) * xc == 0) NA else if ((nt - xt) * xc == 0) Inf else xt * (nc - xc) / ((nt - xt) * xc)
  want <- list(ce_from = ce[1], te_from = te[1],
               rre = if (pc == 0) (if (pt == 0) NA else Inf) else pt / pc, rre_from = rl[1], rre_to = rl[2],
               rrne = if (pc == 1) (if (pt == 1) NA else Inf) else (1 - pt) / (1 - pc), rrne_from = rn[1], rrne_to = rn[2],
               oor = or, oor_from = e$llf, oor_to = e$ulf,
               rrr = if (pc == 0) (if (pt == 0) NA else -Inf) else (pc - pt) / pc, rrr_from = 1 - rl[2], rrr_to = 1 - rl[1],
               rd = pc - pt, rd_from = dl[1], rd_to = dl[2])
  # the number needed to treat is 1 over the difference of the risks, for benefit if the treated have the lower risk and for
  # harm if they have the higher; its limits are 1 over the limits of the difference, the nearer to 1 first if both are
  # of one kind, and harm before benefit if they are not
  pair <- function(l, u) { n <- c(1 / l, 1 / u); if (all(n < 0)) n[order(-n)] else n[order(n)] }
  limits <- pair(dl[1], dl[2])
  want <- c(want, list(treat = nn(1 / (pc - pt)), treat_from = nn(limits[1]), treat_to = nn(limits[2])))
  if (!is.null(brr)) {
    base <- if (brr >= 1) brr / 100 else brr
    from_ratio <- function(f, m) if (is.na(m) || is.infinite(m)) NA else if (f * m == 0) Inf else 1 / (f * m)
    from_odds <- function(o) if (is.na(o) || is.infinite(o)) NA else if ((1 - base) * base * (1 - o) == 0) Inf else (1 - base * (1 - o)) / ((1 - base) * base * (1 - o))
    rrr <- want$rrr; rrne <- want$rrne
    want <- c(want, list(`*adjusted.rows` = 1, `*adjusted[1].brr` = 100 * base,
                         `*adjusted[1].rd_treat` = nn(1 / (pc - pt)),
                         `*adjusted[1].rr_treat` = nn(from_ratio(base, rrr)),
                         `*adjusted[1].rrn_treat` = nn(from_ratio(1 - base, if (is.na(rrne)) NA else rrne - 1)),
                         `*adjusted[1].or_treat` = nn(from_odds(or))))
  } else want <- c(want, list(`*adjusted.rows` = 0))
  do.call(case, c(list("RptMiscNumberNeededToTreat", c(list(nt = nt, xt = xt, nc = nc, xc = xc, cco = level), if (is.null(brr)) list() else list(brr = brr))), want))
}
# ---- counts that are not whole numbers are rounded, a half to the even number, before anything is worked out: the figures are
# those of the table of the rounded counts
for (t in list(c(10.5, 5, 3, 12), c(11.5, 5.4, 2.6, 12), c(9.5, 0.4, 3, 12.49))) {
  w <- round(t); a <- w[1]; b <- w[2]; c <- w[3]; d <- w[4]; n <- sum(w); level <- 0.95
  e <- odds_exact(a, b, c, d, level)
  case("RptMiscRetroRisk", list(a = t[1], b = t[2], c = t[3], d = t[4], cco = level), aa = a, bb = b, cc = c, dd = d, odds = if (b * c == 0) Inf else a * d / (b * c), eor = e$est, llf = e$llf, ulf = e$ulf)
  rl <- ratio_limits(a, a + c, b, b + d, level)
  case("RptMiscRelRisk", list(a = t[1], b = t[2], c = t[3], d = t[4], cco = level), a_out = a, b_out = b, ratio = if (b == 0) Inf else (a / (a + c)) / (b / (b + d)), koopman_from = rl[1], koopman_to = rl[2])
  lp <- ratio_limits(a, a + c, b, b + d, level)
  case("RptMiscDiagnostic", list(a = t[1], b = t[2], c = t[3], d = t[4], cco = level), aa = a, tot = n, prevalence = (a + c) / n, sensitive = a / (a + c), lr_pos = if (b == 0) Inf else (a / (a + c)) / (b / (b + d)), lr_pos_from = lp[1], odr = if (b * c == 0) Inf else a * d / (b * c), cmle = e$est)
}
case("RptMiscNumberNeededToTreat", list(nt = 100, xt = 10.5, nc = 100.4, xc = 19.5, cco = 0.95), rd = 0.2 - 0.1, rre = 0.5, treat = nn(10))
case("RptMiscLikely", list(data = "10.5,20|40,14.5", z1 = 0.95), `*row[1].likely` = (10 / 30) / (40 / 54), `*row[2].likely` = (20 / 30) / (14 / 54), `*row[1].plusfeature` = 10, `*row[2].minusfeature` = 14)
# ---- the events and the subjects of a group given the other way about are taken the right way about
case("RptMiscNumberNeededToTreat", list(nt = 10, xt = 100, nc = 100, xc = 20, cco = 0.95), rd = 0.1, rre = 0.5, treat = nn(10))
# ---- an expected risk of 1 to 100 is a percentage; one that is not from 0 to 100 is put back to the risk of the controls
case("RptMiscNumberNeededToTreat", list(nt = 100, xt = 10, nc = 100, xc = 20, cco = 0.95, brr = 1), `*adjusted[1].brr` = 1, `*adjusted[1].rr_treat` = nn(1 / (0.01 * 0.5)))
for (brr in c(150, -0.2)) case("RptMiscNumberNeededToTreat", list(nt = 100, xt = 10, nc = 100, xc = 20, cco = 0.95, brr = brr), `*adjusted[1].brr` = 20, `*adjusted[1].rr_treat` = nn(1 / (0.2 * 0.5)))
# ---- a confidence level that is not above 0 and below 1 is taken as 0.95
for (level in c(0, 1, 95)) {
  rl <- ratio_limits(10, 13, 5, 17, 0.95)
  case("RptMiscRelRisk", list(a = 10, b = 5, c = 3, d = 12, cco = level), pc = 95, koopman_from = rl[1], koopman_to = rl[2])
  case("RptMiscDiagnostic", list(a = 10, b = 5, c = 3, d = 12, cco = level), pc = 95, prevalence_from = exact_limits(13, 30, 0.95)[1])
}

# ---- what is refused, and the words of the refusal
refused <- function(.report, .inputs, .message) case(.report, .inputs, refused = .message)
below <- "The counts must be numbers that are not below 0"
many <- "The counts come to more than 2,147,483,647"
for (report in c("RptMiscRetroRisk", "RptMiscRelRisk", "RptMiscDiagnostic")) {
  refused(report, list(a = -1, b = 5, c = 3, d = 12, cco = 0.95), below)
  refused(report, list(a = 10, b = 5, c = 3, d = -0.2, cco = 0.95), below)
  refused(report, list(a = 3e9, b = 1000, c = 1000, d = 3e9, cco = 0.95), many)
  refused(report, list(a = 1e9, b = 1e9, c = 1e8, d = 1e8, cco = 0.95), many)
}
refused("RptMiscNumberNeededToTreat", list(nt = 100, xt = -5, nc = 100, xc = 20, cco = 0.95), below)
refused("RptMiscNumberNeededToTreat", list(nt = 3e9, xt = 10, nc = 3e9, xc = 20, cco = 0.95), many)
refused("RptMiscNumberNeededToTreat", list(nt = 0, xt = 0, nc = 100, xc = 20, cco = 0.95), "There must be at least one treated subject and one control")
refused("RptMiscNumberNeededToTreat", list(nt = 100, xt = 10, nc = 0.4, xc = 0, cco = 0.95), "There must be at least one treated subject and one control")
refused("RptMiscRelRisk", list(a = 0, b = 0, c = 3, d = 12, cco = 0.95), "no subject has the outcome")
refused("RptMiscRelRisk", list(a = 0, b = 5, c = 0, d = 12, cco = 0.95), "there must be subjects who were exposed and subjects who were not")
refused("RptMiscRelRisk", list(a = 4, b = 0, c = 7, d = 0, cco = 0.95), "there must be subjects who were exposed and subjects who were not")
refused("RptMiscDiagnostic", list(a = 0, b = 0, c = 0, d = 0, cco = 0.95), "The table has no subjects")
refused("RptMiscFalseResult", list(pt = 1.2, pf = 0.1, pd = 100), "Sensitivity must be between 0 and 1")
refused("RptMiscFalseResult", list(pt = 0.9, pf = -0.1, pd = 100), "1 - specificity must be between 0 and 1")
for (one_in in c(0.5, 0, -3)) refused("RptMiscFalseResult", list(pt = 0.9, pf = 0.1, pd = one_in), "n must be at least 1")
refused("RptMiscLikely", list(data = "10,20,30|40,15,*", z1 = 0.95), "All values must be >= 0")
refused("RptMiscLikely", list(data = "10,-2,30|40,15,5", z1 = 0.95), "All values must be >= 0")
refused("RptMiscLikely", list(data = "0,0|4,5", z1 = 0.95), "must both be > 0")
refused("RptMiscLikely", list(data = "3,4|0,0", z1 = 0.95), "must both be > 0")

writeLines(cases, args[1]); writeLines(expected, args[2])
cat(length(cases), "cases,", length(expected), "figures\n")
