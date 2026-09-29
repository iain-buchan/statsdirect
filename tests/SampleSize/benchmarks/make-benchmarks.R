# The Sample Size menu: the cases that the reports are given, and what is expected of them, worked out from the definitions of
# the methods as the help gives them.  A sample size is the smallest whole number above the value of the formula (the whole
# part and 1), or, for the correlation and the t tests, the smallest number at which the power reaches what is asked.  A
# number of controls is the smallest whole number that is not below the controls per subject times the subjects.
#
# A case (cases.txt): the report, a name, and the inputs as key=value, with semicolons between them.
# What is expected (expected.txt): "report|name|output", and the figure; for the rows of a list of the report the output is
# "*list.rows" for their number and "*list[i].output" for an output of row i; "refused" has words of the refusal.
#
# usage: Rscript --vanilla make-benchmarks.R     (in this folder; it takes about a minute)
args <- c("cases.txt", "expected.txt")
cases <- character(0); expected <- character(0); count <- 0
num <- function(x) if (is.character(x)) x else format(x, digits = 17, scientific = abs(x) < 1e-4 && x != 0 || abs(x) >= 1e15, trim = TRUE)
case <- function(report, inputs, ...) {
  count <<- count + 1
  name <- sprintf("c%04d", count)
  cases <<- c(cases, paste(report, name, paste0(names(inputs), "=", sapply(inputs, num), collapse = ";"), sep = "\t"))
  want <- list(...)
  for (k in names(want)) expected <<- c(expected, paste0(report, "|", name, "|", k, "\t", num(want[[k]])))
}
up <- function(x) floor(x) + 1
# the smallest whole number, not below the least, at which a power that goes up with the number reaches what is asked: a number
# that has the power is looked for by doubling, and the smallest between it and one that has not by halving the gap
smallest <- function(power, asked, least) {
  if (power(least) >= asked) return(least)
  lo <- least; hi <- 2 * least
  while (power(hi) < asked) { lo <- hi; hi <- 2 * hi }
  while (hi - lo > 1) { mid <- floor((lo + hi) / 2); if (power(mid) >= asked) hi <- mid else lo <- mid }
  hi
}
z <- function(p) qnorm(p, lower.tail = FALSE)
powers <- c(0.5, 0.8, 0.9, 0.99); alphas <- c(0.001, 0.01, 0.05, 0.2)
# whether the note on the power is given, and what it has
note <- function(test, power, alpha, n) if (test <= 3.1) list(rows = 1, less = power, greater = min(power + alpha / 2, 1), cases = n) else list(rows = 0)
with_note <- function(report, inputs, nt, ...) {
  if (nt$rows == 1) case(report, inputs, ..., `*assumptions.rows` = 1, `*assumptions[1].no_less` = nt$less, `*assumptions[1].no_greater` = nt$greater, `*assumptions[1].cases` = nt$cases)
  else case(report, inputs, ..., `*assumptions.rows` = 0)
}

# ---- correlation: Fisher's z is normal with the variance 1 / (n - 3)
r_power <- function(d, n, zs) pnorm(d * sqrt(n - 3) - zs) + pnorm(-d * sqrt(n - 3) - zs)
for (p in powers) for (a in alphas) for (r in list(c(0, 0.3), c(0, 0.05), c(0.5, 0.6), c(0.2, 0.9), c(0.9, 0.2), c(0, 0.999), c(0.3, 0.3001), c(0, 0.0005))) {
  d <- abs(atanh(r[1]) - atanh(r[2])); zs <- z(a / 2)
  n <- smallest(function(n) r_power(d, n, zs), p, 4)
  case("RptSizeCorrelation", list(p = p, a = a, r0 = r[1], r1 = r[2]), size = n)
}
# ---- survival
for (p in powers) for (a in alphas) for (m in c(1, 2, 0.5, 1.3)) for (s in list(c(12, 18, 24, 12), c(12, 8, 24, 12), c(5, 5.5, 0, 10), c(5, 40, 10, 0), c(100, 50, 1, 1), c(1, 1.01, 3, 3))) {
  ct <- s[1]; et <- s[2]; at <- s[3]; fut <- s[4]; hr <- ct / et
  avt <- (ct + et) / 2
  pa <- if (at == 0) 1 else (1 - exp(-log(2) * at / avt)) / (log(2) * at / avt)
  pe <- 1 - pa * exp(-log(2) * fut / avt)
  n <- up((z(a / 2) + z(1 - p))^2 * ((1 + 1 / m) / pe) / log(hr)^2)
  with_note("RptSizeSurvival", list(p = p, a = a, ct = ct, at = at, fut = fut, m = m, `time-or-hr` = "time", et = et), note(2 * z(a / 2) + z(1 - p), p, a, n), size = n, controls = ceiling(round(n * m, 9)), hrFmt = hr)
  with_note("RptSizeSurvival", list(p = p, a = a, ct = ct, at = at, fut = fut, m = m, `time-or-hr` = "hr", hr = hr), note(2 * z(a / 2) + z(1 - p), p, a, n), size = n, controls = ceiling(round(n * m, 9)), etFmt = et)
}
# ---- independent case-control and cohort studies
two_props <- function(report, p, a, p0, p1, m, inputs) {
  pbar <- (p1 + m * p0) / (m + 1)
  s0 <- sqrt((1 + 1 / m) * pbar * (1 - pbar)); sa <- sqrt(p0 * (1 - p0) / m + p1 * (1 - p1))
  nx <- (z(a / 2) * s0 + z(1 - p) * sa)^2 / (p0 - p1)^2
  n <- up(nx); nc <- up(nx / 4 * (1 + sqrt(1 + 2 * (m + 1) / (nx * m * abs(p0 - p1))))^2)
  with_note(report, inputs, note(2 * (s0 / sa) * z(a / 2) + z(1 - p), p, a, n), case = n, controls = ceiling(round(m * n, 9)), case_corr = nc, controls_corr = ceiling(round(m * nc, 9)), ps = p1)
}
for (p in powers) for (a in alphas) for (m in c(1, 3, 0.5, 1.3)) for (s in list(c(0.1, 0.2), c(0.5, 0.45), c(0.01, 0.0001), c(0.9, 0.999), c(0.3, 0.31), c(0.001, 0.9))) {
  two_props("RptSizeIndCase", p, a, s[1], s[2], m, list(p = p, a = a, p0 = s[1], `prop-or-or` = "prop", p1 = s[2], m = m))
  two_props("RptSizeIndProp", p, a, s[1], s[2], m, list(p = p, a = a, p0 = s[1], `prop-or-or` = "prop", p1 = s[2], m = m))
  or <- (s[2] / (1 - s[2])) / (s[1] / (1 - s[1]))
  two_props("RptSizeIndCase", p, a, s[1], s[1] * or / (1 + s[1] * (or - 1)), m, list(p = p, a = a, p0 = s[1], `prop-or-or` = "or", r = or, m = m))
  rr <- s[2] / s[1]
  if (s[1] * rr <= 1) two_props("RptSizeIndProp", p, a, s[1], s[1] * rr, m, list(p = p, a = a, p0 = s[1], `prop-or-or` = "rr", r = rr, m = m))
}
# ---- matched case-control study: the probability of exposure of a case is the one with which the odds ratio of the pairs
# that differ is psi, with the correlation phi of the exposures of a case and its control
matched <- function(p, a, phi, p0, m, psi) {
  q0 <- 1 - p0
  f <- function(p1) { s <- sqrt(p1 * (1 - p1) * p0 * q0); (p1 * q0 - phi * s) - psi * (p0 * (1 - p1) - phi * s) }
  p1 <- uniroot(f, c(1e-12, 1 - 1e-12), tol = 1e-15)$root
  q1 <- 1 - p1; s <- sqrt(p1 * q1 * p0 * q0)
  p11 <- p1 * p0 + phi * s; p01 <- q1 * p0 - phi * s
  if (p11 < 0 || p01 < 0 || p1 - p11 < 0 || 1 - p1 - p01 < 0) return(NULL)
  plus <- p11 / p1; minus <- p01 / q1
  size <- function(m) {
    k <- 1:m
    t <- p1 * choose(m, k - 1) * plus^(k - 1) * (1 - plus)^(m - k + 1) + q1 * choose(m, k) * minus^k * (1 - minus)^(m - k)
    e1 <- sum(k * t / (m + 1)); v1 <- sum(k * t * (m - k + 1) / (m + 1)^2)
    ep <- sum(k * t * psi / (k * psi + m - k + 1)); vp <- sum(k * t * psi * (m - k + 1) / (k * psi + m - k + 1)^2)
    list(n = (z(1 - p) * sqrt(vp) + z(a / 2) * sqrt(v1))^2 / (ep - e1)^2, ratio = sqrt(v1) / sqrt(vp))
  }
  one <- size(1); many <- size(m)
  list(n = up(many$n), fm = up(many$n) / up(one$n), ratio = many$ratio)
}
for (p in powers) for (a in c(0.01, 0.05)) for (m in c(1, 2, 5, 40)) for (s in list(c(0.2, 0.3, 2), c(0, 0.3, 2), c(0.2, 0.1, 0.5), c(-0.1, 0.5, 3), c(0.5, 0.05, 1.1), c(0.1, 0.9, 10))) {
  w <- matched(p, a, s[1], s[2], m, s[3])
  if (is.null(w)) next
  inputs <- list(p = p, a = a, ph = s[1], p0 = s[2], ps = s[3], m = m)
  nt <- note(2 * w$ratio * z(a / 2) + z(1 - p), p, a, w$n)
  if (m > 1) { if (nt$rows == 1) case("RptSizeMatchCase", inputs, size = w$n, `*reduction.rows` = 1, `*reduction[1].reduction` = w$fm, `*assumptions.rows` = 1, `*assumptions[1].cases` = w$n) else case("RptSizeMatchCase", inputs, size = w$n, `*reduction.rows` = 1, `*reduction[1].reduction` = w$fm, `*assumptions.rows` = 0) }
  else with_note("RptSizeMatchCase", inputs, nt, size = w$n, `*reduction.rows` = 0)
}
# ---- paired cohort study
for (p in powers) for (a in alphas) for (s in list(c(0.1, 0.2, 0.2), c(0.1, 0.2, 0), c(0.5, 0.4, -0.3), c(0.3, 0.31, 0.5), c(0.05, 0.9, 0.1), c(0.6, 0.2, 0.2))) {
  p0 <- s[1]; p1 <- s[2]; ph <- s[3]
  g <- sqrt(p1 * (1 - p1) * p0 * (1 - p0))
  py <- p1 * (1 - p0) - ph * g; px <- p0 * (1 - p1) - ph * g
  pa <- py / (px + py)
  if (!(pa * (1 - pa) > 0)) next
  n <- up((z(a / 2) / 2 + z(1 - p) * sqrt(pa * (1 - pa)))^2 / ((pa - 0.5)^2 * (px + py)))
  nt <- note(2 * (0.5 / sqrt(pa * (1 - pa))) * z(a / 2) + z(1 - p), p, a, n)
  with_note("RptSizeMatchProp", list(p = p, a = a, p0 = p0, ph = ph, `er-or-rr` = "er", p1 = p1), nt, size = n)
  with_note("RptSizeMatchProp", list(p = p, a = a, p0 = p0, ph = ph, `er-or-rr` = "rr", rr = p1 / p0), nt, size = n)
}
# ---- population survey
for (cc in c(0.8, 0.95, 0.999)) for (ps in c(50, 20000, 1e9)) for (s in list(c(25, 3), c(50, 0.1), c(0.01, 0.005), c(99, 1), c(50, 50)))
  case("RptSizePopSurvey", list(ps = ps, p = s[1], xd = s[2], cco = cc), size = { sn <- qnorm(cc + (1 - cc) / 2)^2 * (s[1] / 100) * (1 - s[1] / 100) / (s[2] / 100)^2; up(sn / (1 + sn / ps)) })
# ---- t tests: the smallest number with which the power of the two sided test, from the non-central t distribution, reaches
# what is asked.  The power has both tails.
t_power <- function(n, d, a, m = NA) {
  if (is.na(m)) { df <- n - 1; ncp <- d * sqrt(n) } else { nc <- ceiling(round(m * n, 9)); df <- n + nc - 2; ncp <- d / sqrt(1 / n + 1 / nc) }
  tc <- qt(a / 2, df, lower.tail = FALSE)
  suppressWarnings(pt(tc, df, ncp, lower.tail = FALSE) + pt(-tc, df, ncp))
}
t_size <- function(p, d, a, m = NA) smallest(function(n) t_power(n, d, a, m), p, 2)
for (p in powers) for (a in alphas) for (s in list(c(5, 12), c(1, 1), c(3, 1), c(-2, 5), c(1, 40), c(0.5, 0.1))) {
  n <- t_size(p, abs(s[1] / s[2]), a)
  case("RptSizePaired", list(p = p, a = a, d = s[1], sd = s[2]), size = n, df = n - 1)
  for (m in c(1, 2, 0.5, 1.1)) {
    n <- t_size(p, abs(s[1] / s[2]), a, m)
    nc <- ceiling(round(m * n, 9))
    case("RptSizeUnPaired", list(p = p, a = a, d = s[1], sd = s[2], m = m), size = n, df = n + nc - 2, `*subjects[1].con_tot` = nc)
  }
}
# ---- the ends of what can be entered
case("RptSizeCorrelation", list(p = 0.8, a = 0.05, r0 = 0, r1 = 0.00001), size = smallest(function(n) r_power(atanh(0.00001), n, z(0.025)), 0.8, 4))
case("RptSizeCorrelation", list(p = 0.0000005, a = 0.05, r0 = 0, r1 = 0.3), size = 4)
two_props("RptSizeIndCase", 0.8, 0.05, 0.1, 0.1000000001, 1, list(p = 0.8, a = 0.05, p0 = 0.1, `prop-or-or` = "prop", p1 = 0.1000000001, m = 1))
two_props("RptSizeIndCase", 0.8, 0.05, 0, 1, 1, list(p = 0.8, a = 0.05, p0 = 0, `prop-or-or` = "prop", p1 = 1, m = 1))
for (m in c(0.0001, 100000)) {
  two_props("RptSizeIndCase", 0.8, 0.05, 0.1, 0.3, m, list(p = 0.8, a = 0.05, p0 = 0.1, `prop-or-or` = "prop", p1 = 0.3, m = m))
  two_props("RptSizeIndProp", 0.8, 0.05, 0.1, 0.3, m, list(p = 0.8, a = 0.05, p0 = 0.1, `prop-or-or` = "prop", p1 = 0.3, m = m))
}
two_props("RptSizeIndProp", 0.8, 0.05, 0.4, 1, 1, list(p = 0.8, a = 0.05, p0 = 0.4, `prop-or-or` = "rr", r = 2.5, m = 1))
for (s in list(c(0.8, 0.2, 0.3, 1000, 2), c(0.8, 0.2, 0.3, 3, 1e6), c(0.8, 0.99, 0.3, 1, 2), c(0.1, 0.2, 0.3, 1, 2))) {
  w <- matched(s[1], 0.05, s[2], s[3], s[4], s[5])
  case("RptSizeMatchCase", list(p = s[1], a = 0.05, ph = s[2], p0 = s[3], ps = s[5], m = s[4]), size = w$n, `*lower.rows` = if (1 - s[1] >= 0.8) 1 else 0)
}
for (k in c(0.001, 100)) case("RptSizePaired", list(p = 0.8, a = 0.05, d = k, sd = 1), size = t_size(0.8, k, 0.05))
case("RptSizePaired", list(p = 0.999999, a = 0.000001, d = 1, sd = 1), size = t_size(0.999999, 1, 0.000001))
for (m in c(0.001, 1000)) { n <- t_size(0.8, 5 / 12, 0.05, m); case("RptSizeUnPaired", list(p = 0.8, a = 0.05, d = 5, sd = 12, m = m), size = n, `*subjects[1].con_tot` = ceiling(round(m * n, 9))) }
case("RptSizeUnPaired", list(p = 0.8, a = 0.05, d = 0.002, sd = 1, m = 1), size = t_size(0.8, 0.002, 0.05, 1))
# a difference below a ten thousandth of the standard deviation is taken as a ten thousandth of it, with a warning, and the
# number is that of the equation in Student's t
t_equation <- function(p, a, k) { f <- function(n) (qt(a / 2, n - 1, lower.tail = FALSE) + qt(1 - p, n - 1, lower.tail = FALSE))^2 / k^2 - n; up(uniroot(f, c(0.5, 2) * (z(a / 2) + z(1 - p))^2 / k^2, tol = 1e-6)$root) }
for (d in c(0, 0.00001)) case("RptSizePaired", list(p = 0.8, a = 0.05, d = d, sd = 1), size = t_equation(0.8, 0.05, 0.0001), `*sample_size_warn.rows` = 1)
for (s in list(c(5, 25, 3, 0.95), c(1e300, 50, 0.0000001, 0.999999), c(20000, 25, 30, 0.95)))
  case("RptSizePopSurvey", list(ps = s[1], p = s[2], xd = s[3], cco = s[4]), size = { sn <- qnorm(s[4] + (1 - s[4]) / 2)^2 * (s[2] / 100) * (1 - s[2] / 100) / (s[3] / 100)^2; up(sn / (1 + sn / s[1])) })

# ---- what is refused, and the words of the refusal
refused <- function(report, inputs, message) case(report, inputs, refused = message)
no_power <- "The power must be greater than 0% and less than 100%."
no_alpha <- "Alpha must be greater than 0% and less than 100%."
every <- list(
  RptSizeCorrelation = list(r0 = 0, r1 = 0.3),
  RptSizeSurvival = list(ct = 12, at = 24, fut = 12, m = 1, `time-or-hr` = "hr", hr = 0.67),
  RptSizeIndCase = list(p0 = 0.1, `prop-or-or` = "prop", p1 = 0.3, m = 1),
  RptSizeIndProp = list(p0 = 0.1, `prop-or-or` = "prop", p1 = 0.3, m = 1),
  RptSizeMatchCase = list(ph = 0.2, p0 = 0.3, ps = 2, m = 1),
  RptSizeMatchProp = list(p0 = 0.1, ph = 0.2, `er-or-rr` = "rr", rr = 2),
  RptSizePaired = list(d = 5, sd = 12),
  RptSizeUnPaired = list(d = 5, sd = 12, m = 1))
# (the first argument has a name that no input begins, so that an input is not taken for it)
with_inputs <- function(.which, ...) { l <- c(list(p = 0.8, a = 0.05), every[[.which]]); new <- list(...); for (k in names(new)) l[[k]] <- new[[k]]; l }
for (report in names(every)) {
  for (p in c(0, 1, 1.5, -0.2)) refused(report, with_inputs(report, p = p), no_power)
  for (a in c(0, 1, 2)) refused(report, with_inputs(report, a = a), no_alpha)
}
refused("RptSizeCorrelation", with_inputs("RptSizeCorrelation", r0 = -0.2), "must be at least 0 and less than 1")
refused("RptSizeCorrelation", with_inputs("RptSizeCorrelation", r1 = 1), "greater than 0 and less than 1")
refused("RptSizeCorrelation", with_inputs("RptSizeCorrelation", r0 = 0.3), "must differ")
refused("RptSizeSurvival", with_inputs("RptSizeSurvival", hr = 1), "a hazard ratio of 1 gives no effect to detect")
refused("RptSizeSurvival", with_inputs("RptSizeSurvival", hr = 0), "or the hazard ratio, must be greater than 0")
refused("RptSizeSurvival", with_inputs("RptSizeSurvival", `time-or-hr` = "time", et = 12), "a hazard ratio of 1 gives no effect to detect")
refused("RptSizeSurvival", with_inputs("RptSizeSurvival", `time-or-hr` = "time", et = -3), "The median survival time of the experimental group")
refused("RptSizeSurvival", with_inputs("RptSizeSurvival", ct = 0), "The median survival time of the control group must be greater than 0")
refused("RptSizeSurvival", with_inputs("RptSizeSurvival", at = 0, fut = 0), "one of them must be greater than 0")
refused("RptSizeSurvival", with_inputs("RptSizeSurvival", at = -1), "must not be below 0")
for (m in c(0, -2)) for (report in c("RptSizeSurvival", "RptSizeUnPaired")) refused(report, with_inputs(report, m = m), "The number of controls per experimental subject must be greater than 0")
for (m in c(0, -2)) refused("RptSizeIndCase", with_inputs("RptSizeIndCase", m = m), "The number of controls per case must be greater than 0")
for (m in c(0, -2)) refused("RptSizeIndProp", with_inputs("RptSizeIndProp", m = m), "The number of controls per experimental subject must be greater than 0")
for (r in c(0, -2)) refused("RptSizeIndCase", with_inputs("RptSizeIndCase", `prop-or-or` = "or", r = r), "The odds ratio must be greater than 0")
refused("RptSizeIndCase", with_inputs("RptSizeIndCase", `prop-or-or` = "or", r = 1), "must differ")
refused("RptSizeIndCase", with_inputs("RptSizeIndCase", p1 = 1.5), "The probability of exposure in cases must be from 0 to 1")
refused("RptSizeIndCase", with_inputs("RptSizeIndCase", p0 = -0.1), "The probability of exposure in controls must be from 0 to 1")
refused("RptSizeIndCase", with_inputs("RptSizeIndCase", p1 = 0.1), "must differ")
for (r in c(0, -1)) refused("RptSizeIndProp", with_inputs("RptSizeIndProp", `prop-or-or` = "rr", r = r), "The relative risk must be greater than 0")
refused("RptSizeIndProp", with_inputs("RptSizeIndProp", p0 = 0.4, `prop-or-or` = "rr", r = 3), "must not be above 1")
refused("RptSizeIndProp", with_inputs("RptSizeIndProp", p1 = -0.3), "The probability of the event in experimental subjects must be from 0 to 1")
refused("RptSizeIndProp", with_inputs("RptSizeIndProp", p0 = 1.2), "The probability of the event in controls must be from 0 to 1")
for (m in c(1.5, 2.999)) refused("RptSizeMatchCase", with_inputs("RptSizeMatchCase", m = m), "The number of controls per case must be a whole number")
refused("RptSizeMatchCase", with_inputs("RptSizeMatchCase", m = 0), "There must be at least one control per case")
refused("RptSizeMatchCase", with_inputs("RptSizeMatchCase", m = 1001), "must not exceed 1000")
refused("RptSizeMatchCase", with_inputs("RptSizeMatchCase", ps = 0), "The odds ratio must be greater than 0")
refused("RptSizeMatchCase", with_inputs("RptSizeMatchCase", ps = 1), "the odds ratio must differ from 1")
refused("RptSizeMatchCase", with_inputs("RptSizeMatchCase", p0 = 0), "must be greater than 0 and less than 1")
for (ph in c(-0.9, 1.5)) refused("RptSizeMatchCase", with_inputs("RptSizeMatchCase", ph = ph), "is not possible with this probability of exposure and odds ratio")
refused("RptSizeMatchProp", with_inputs("RptSizeMatchProp", rr = 12), "must be greater than 0 and less than 1")
refused("RptSizeMatchProp", with_inputs("RptSizeMatchProp", rr = 1), "The event rates in the two groups must differ")
refused("RptSizeMatchProp", with_inputs("RptSizeMatchProp", ph = 0.95), "try a smaller value for correlation")
refused("RptSizeMatchProp", with_inputs("RptSizeMatchProp", ph = 1), "The correlation coefficient must be greater than -1 and less than 1")
for (sd in c(0, -2)) for (report in c("RptSizePaired", "RptSizeUnPaired")) refused(report, with_inputs(report, sd = sd), "The standard deviation must be greater than 0")
survey <- function(...) { l <- list(ps = 20000, p = 25, xd = 3, cco = 0.95); new <- list(...); for (k in names(new)) l[[k]] <- new[[k]]; l }
for (cc in c(0, 1, 95)) refused("RptSizePopSurvey", survey(cco = cc), "The confidence level must be greater than 0% and less than 100%")
for (xd in c(0, -3)) refused("RptSizePopSurvey", survey(xd = xd), "The acceptable deviation must be greater than 0%")
refused("RptSizePopSurvey", survey(ps = 0), "The size of the population must be greater than 0")
for (p in c(0, 100, 120)) refused("RptSizePopSurvey", survey(p = p), "must be greater than 0% and less than 100%")

writeLines(cases, args[1]); writeLines(expected, args[2])
cat(length(cases), "cases,", length(expected), "figures\n")
