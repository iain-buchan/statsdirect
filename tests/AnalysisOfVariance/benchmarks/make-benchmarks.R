# The benchmarks of tests/AnalysisOfVariance: the cases of the Analysis of Variance menu and what is expected of them, worked out
# in R from the definitions of the methods.  A case: the report, a name, and the inputs.  What is expected:
# "report|name|output<TAB>figure"; a figure may carry its own room ("value~part", or "value~room!" for a room that is not a
# part of the value).  R 4.6.1, no packages.
# usage: Rscript --vanilla make-benchmarks.R cases.txt expected.txt
args <- commandArgs(trailingOnly = TRUE)
cases <- character(0); expected <- character(0); count <- 0
fig <- function(v) format(v, digits = 17, scientific = abs(v) < 1e-4 && v != 0 || abs(v) >= 1e15, trim = TRUE)
num <- function(x) if (is.character(x)) x else if (is.na(x)) "missing" else if (is.logical(x)) (if (x) "true" else "false") else if (is.infinite(x)) (if (x > 0) "Infinity" else "-Infinity") else fig(x)
column <- function(x) paste(sapply(x, function(v) if (is.na(v)) "*" else fig(v)), collapse = ",")
frame <- function(...) paste(sapply(list(...), column), collapse = "|")
frame_of <- function(cols) do.call(frame, cols)
frame2d <- function(frames) paste(sapply(frames, frame_of), collapse = "~")
withroom <- function(v, room) paste0(fig(v), "~", format(room, digits = 3), "!")   # a figure with an absolute room
case <- function(.report, .inputs, .want) {
  count <<- count + 1
  name <- sprintf("c%04d", count)
  if (!is.null(.want$refused)) .want <- .want["refused"]
  cases <<- c(cases, paste(.report, name, paste0(names(.inputs), "=", sapply(.inputs, num), collapse = ";"), sep = "\t"))
  for (k in names(.want)) expected <<- c(expected, paste0(.report, "|", name, "|", k, "\t", num(.want[[k]])))
  invisible(name)
}
set.seed(20260930)
ss <- function(x) sum((x - mean(x))^2)
tail2 <- function(t, df) 2 * pt(-abs(t), df)
level_of <- function(gamma) if (gamma <= 0 || gamma >= 1) 0.95 else gamma

# ---- the studentized range: P(range of k means over the root mean square with df degrees of freedom <= q), by an integration of
# the density of the range of normal variates (about its peak) over the distribution of the residual standard deviation, and its
# inverse; the program has the same from a series that is within 5e-7 in probability, which is the room given to a P, and to a q
# through the density
range_p <- function(w, k) {
  if (w <= 0) return(0)
  logf <- function(z) dnorm(z, log = TRUE) + (k - 1) * log(pmax(pnorm(z + w) - pnorm(z), 1e-300))
  peak <- optimize(function(z) -logf(z), c(-w, 0))$minimum
  top <- logf(peak); f <- function(z) exp(logf(z) - top)
  h <- 1e-3; curve <- (logf(peak + h) - 2 * logf(peak) + logf(peak - h)) / h^2; sd <- 1 / sqrt(-curve)
  k * exp(top) * integrate(f, peak - 12 * sd, peak + 12 * sd, rel.tol = 1e-12, abs.tol = 0, subdivisions = 5000)$value
}
srange_p <- function(q, k, df) {
  if (q <= 0) return(0)
  logc <- 0.5 * df * log(df) - (0.5 * df - 1) * log(2) - lgamma(0.5 * df)
  g <- function(y) { s <- exp(y); exp(logc + df * y - 0.5 * df * s^2) * sapply(s, function(si) range_p(q * si, k)) }
  ylo <- 0.5 * log(qchisq(1e-30, df) / df); yhi <- 0.5 * log(qchisq(1e-30, df, lower.tail = FALSE) / df)
  min(1, integrate(g, ylo, yhi, rel.tol = 1e-11, abs.tol = 0, subdivisions = 5000)$value)
}
srange_q <- function(p, k, df) uniroot(function(q) srange_p(q, k, df) - p, c(0.05, 40), tol = 1e-12)$root
srange_dens <- function(q, k, df) (srange_p(q + 1e-5, k, df) - srange_p(q - 1e-5, k, df)) / 2e-5
p_room <- 5e-7

# ---- one way analysis of variance: the columns are the groups
one_way <- function(groups, .more = list()) {
  g <- lapply(groups, function(x) x[!is.na(x)]); n <- sapply(g, length); m <- sapply(g, mean); N <- sum(n); gm <- sum(unlist(g)) / N
  ssb <- sum(n * (m - gm)^2); ssw <- sum(sapply(g, ss)); sst <- sum((unlist(g) - gm)^2)
  k <- length(g); f <- (ssb / (k - 1)) / (ssw / (N - k))
  want <- list(b_sum = ssb, b_df = k - 1, b_mean = ssb / (k - 1), w_sum = ssw, w_df = N - k, w_mean = ssw / (N - k), t_sum = sst, t_df = N - 1,
               f = f, p = pf(f, k - 1, N - k, lower.tail = FALSE))
  want[names(.more)] <- .more
  case("RptOneWay", list(data = frame_of(groups)), want)
}
expt <- list(c(279, 338, 334, 198, 303), c(378, 275, 412, 265, 286), c(172, 335, 335, 282, 250), c(381, 346, 340, 471, 318))
expt_unequal <- list(c(279, 338, 334, 198, 303), c(378, 275, 412, 265, 286), c(172, 335, 335, 282, 250), c(381, 346, 340))
substances <- list(c(29, 28, 23, 26, 26, 19, 25, 29, 26, 28), c(17, 25, 24, 19, 28, 21, 20, 25, 19, 24), c(17, 16, 21, 22, 23, 18, 20, 17, 25, 21), c(18, 20, 25, 24, 16, 20, 20, 17, 19, 17))
one_way(expt); one_way(expt_unequal); one_way(substances)
one_way(list(c(1.2, 3.4, NA, 2.2), c(5.1, NA, 4.4), c(0.5, 2.9, 6.0, 1.1)))
one_way(list(c(1e6 + 1.2, 1e6 + 3.4, 1e6 + 2.2), c(1e6 + 5.1, 1e6 + 4.4, 1e6 + 6.0), c(1e6 + 0.5, 1e6 + 2.9, 1e6 + 1.1)))   # values far from 0
one_way(list(c(-3.5, -2.1, -4.4, -1.9), c(2.2, 1.8, 3.1, 2.7)))                       # two groups: F is t squared
one_way(list(rnorm(30, 10, 2), rnorm(25, 11, 2), rnorm(40, 10.5, 3), rnorm(12, 9, 1), rnorm(18, 12, 2)))

# ---- two way (randomized blocks): the columns are the treatments and the rows the blocks; a row with a missing value is left out
two_way <- function(cols, .more = list()) {
  m <- do.call(cbind, cols); keep <- complete.cases(m); m <- m[keep, , drop = FALSE]; nr <- nrow(m); nc <- ncol(m)
  r <- rowMeans(m); cm <- colMeans(m); gm <- mean(m)
  ssrow <- nc * sum((r - gm)^2); sscol <- nr * sum((cm - gm)^2); sstot <- sum((m - gm)^2)
  ssres <- sum((m - outer(r, rep(1, nc)) - outer(rep(1, nr), cm) + gm)^2)
  dfrow <- nr - 1; dfcol <- nc - 1; dfres <- dfrow * dfcol
  want <- list(sub_sum = ssrow, sub_df = dfrow, sub_mean = ssrow / dfrow, grp_sum = sscol, grp_df = dfcol, grp_mean = sscol / dfcol,
               res_sum = ssres, res_df = dfres, res_mean = ssres / dfres, tot_sum = sstot, tot_df = nr * nc - 1,
               sub_vr = (ssrow / dfrow) / (ssres / dfres), sub_p = pf((ssrow / dfrow) / (ssres / dfres), dfrow, dfres, lower.tail = FALSE),
               grp_vr = (sscol / dfcol) / (ssres / dfres), grp_p = pf((sscol / dfcol) / (ssres / dfres), dfcol, dfres, lower.tail = FALSE))
  want[names(.more)] <- .more
  case("RptTwoWay", list(data = frame_of(cols)), want)
}
grass <- list(c(8.4, 12.8, 9.6, 9.8, 8.4, 8.6, 8.9, 7.9), c(9.4, 15.2, 9.1, 8.8, 8.2, 9.9, 9, 8.1), c(9.8, 12.9, 11.2, 9.9, 8.5, 9.8, 9.2, 8.2), c(12.2, 14.4, 9.8, 12, 8.5, 10.9, 10.4, 10))
two_way(grass)
two_way(list(c(8.4, 12.8, NA, 9.8, 8.4), c(9.4, 15.2, 9.1, 8.8, NA), c(9.8, 12.9, 11.2, 9.9, 8.5)))   # rows with a missing value are left out
two_way(lapply(1:3, function(j) 1e6 + rnorm(6, j, 0.5)))                                          # values far from 0
two_way(list(c(1, 2, 3), c(2, 4, 4)))                                                              # two treatments, three blocks

# ---- replicated two way: the frames are the blocks, the columns of each the treatments, the values the repeats; a missing
# repeat is replaced by the mean of its cell and takes a residual and a total degree of freedom away
two_multi <- function(blocks, .more = list()) {
  nr <- length(blocks); nc <- length(blocks[[1]]); nm <- length(blocks[[1]][[1]]); missing <- 0
  y <- array(NA, c(nm, nr, nc)); sumrecip <- numeric(nc)
  for (i in 1:nr) for (j in 1:nc) { v <- blocks[[i]][[j]]; present <- v[!is.na(v)]; missing <- missing + sum(is.na(v)); v[is.na(v)] <- mean(present); y[, i, j] <- v }
  cell <- apply(y, c(2, 3), mean); r <- rowMeans(cell); cm <- colMeans(cell); gm <- mean(y)
  ssrow <- nc * nm * sum((r - gm)^2); sscol <- nr * nm * sum((cm - gm)^2); sstot <- sum((y - gm)^2)
  ssint <- nm * sum((cell - outer(r, rep(1, nc)) - outer(rep(1, nr), cm) + gm)^2)
  ssres <- sum(sapply(1:nr, function(i) sapply(1:nc, function(j) sum((y[, i, j] - cell[i, j])^2))))
  dfrow <- nr - 1; dfcol <- nc - 1; dfint <- dfrow * dfcol; dfres <- (nm - 1) * nr * nc - missing; dftot <- nm * nr * nc - 1 - missing
  msres <- ssres / dfres
  want <- list(sub_sum = ssrow, sub_df = dfrow, sub_mean = ssrow / dfrow, grp_sum = sscol, grp_df = dfcol, grp_mean = sscol / dfcol,
               int_sum = ssint, int_df = dfint, int_mean = ssint / dfint, res_sum = ssres, res_df = dfres, res_mean = msres, tot_sum = sstot, tot_df = dftot,
               sub_vr = ssrow / dfrow / msres, sub_p = pf(ssrow / dfrow / msres, dfrow, dfres, lower.tail = FALSE),
               grp_vr = sscol / dfcol / msres, grp_p = pf(sscol / dfcol / msres, dfcol, dfres, lower.tail = FALSE),
               int_vr = ssint / dfint / msres, int_p = pf(ssint / dfint / msres, dfint, dfres, lower.tail = FALSE), `*warn.rows` = if (missing > 0) 1 else 0)
  want[names(.more)] <- .more
  case("RptTwoMulti", list(data2d = frame2d(blocks)), want)
}
rep_blocks <- list(list(c(9.8, 10.1, 9.8), c(9.9, 9.5, 10), c(11.3, 10.7, 10.7)), list(c(9.2, 8.6, 9.2), c(9.1, 9.1, 9.4), c(10.3, 10.7, 10.2)), list(c(8.4, 7.9, 8), c(8.6, 8, 8), c(9.8, 10.1, 10.1)))
two_multi(rep_blocks)
two_multi(list(list(c(9.8, 10.1, NA), c(9.9, 9.5, 10), c(11.3, 10.7, 10.7)), list(c(9.2, 8.6, 9.2), c(NA, 9.1, 9.4), c(10.3, 10.7, 10.2)), list(c(8.4, 7.9, 8), c(8.6, 8, 8), c(9.8, 10.1, 10.1))))
two_multi(list(list(c(1, 2), c(3, 5)), list(c(2, 2), c(6, 7))))                                    # two by two with two repeats
two_multi(lapply(1:4, function(i) lapply(1:3, function(j) 1e6 + rnorm(4, i + j, 0.3))))          # values far from 0

# ---- fully nested: the frames are the groups, the columns of each the subgroups
two_nest <- function(groups, .more = list()) {
  g <- lapply(groups, function(sub) lapply(sub, function(x) x[!is.na(x)]))
  y <- unlist(g); N <- length(y); gm <- mean(y); L <- sum(sapply(g, length)); k <- length(g)
  gbar <- sapply(g, function(sub) mean(unlist(sub))); ngp <- sapply(g, function(sub) length(unlist(sub)))
  ss1 <- sum(ngp * (gbar - gm)^2)
  ss2 <- sum(unlist(lapply(seq_along(g), function(i) sapply(g[[i]], function(x) length(x) * (mean(x) - gbar[i])^2))))
  ss3 <- sum(unlist(lapply(g, function(sub) sapply(sub, ss)))); ss4 <- sum((y - gm)^2)
  df1 <- k - 1; df2 <- L - k; df3 <- N - L
  f1 <- ss1 / df1 / (ss3 / df3); f2 <- ss1 / df1 / (ss2 / df2); f3 <- ss2 / df2 / (ss3 / df3)
  want <- list(grp_sum = ss1, grp_df = df1, grp_mean = ss1 / df1, sub_sum = ss2, sub_df = df2, sub_mean = ss2 / df2, res_sum = ss3, res_df = df3, res_mean = ss3 / df3,
               tot_sum = ss4, tot_df = N - 1, f_1 = f1, p_1 = pf(f1, df1, df3, lower.tail = FALSE), f_2 = f2, p_2 = pf(f2, df1, df2, lower.tail = FALSE),
               f_3 = f3, p_3 = pf(f3, df2, df3, lower.tail = FALSE))
  want[names(.more)] <- .more
  case("RptTwoNest", list(data2d = frame2d(groups)), want)
  # the means, given by the report that follows
  want <- list(g_mean = gm, g_n = N, `*group.rows` = k)
  for (i in 1:k) { want[[sprintf("*group[%d].gmean", i)]] <- gbar[i]; want[[sprintf("*group[%d].gn", i)]] <- ngp[i] }
  ii <- 0
  for (i in 1:k) for (j in seq_along(g[[i]])) { ii <- ii + 1; want[[sprintf("*subgroup[%d].sgmean", ii)]] <- mean(g[[i]][[j]]) }
  want[["*subgroup.rows"]] <- ii
  case("RptNestMeans", list(data2d = frame2d(groups), after = "RptTwoNest"), want)
}
nested <- list(list(c(3.28, 3.09), c(3.52, 3.48), c(2.88, 2.8)), list(c(2.46, 2.44), c(1.87, 1.92), c(2.19, 2.19)), list(c(2.77, 2.66), c(3.74, 3.44), c(2.55, 2.55)), list(c(3.78, 3.87), c(4.07, 4.12), c(3.31, 3.31)))
two_nest(nested)
two_nest(list(list(c(1, 2, 3), c(2, 4)), list(c(5, 6, NA, 7), c(4, 4, 5), c(8, 9))))              # subgroups of different sizes, a missing value
two_nest(lapply(1:3, function(i) lapply(1:2, function(j) 1e6 + rnorm(5, i, 0.2))))               # values far from 0

# ---- Latin square: the observations with the codes of their row, column and treatment
latin <- function(obs, row, col, trt, .more = list()) {
  n <- as.integer(round(sqrt(length(obs)))); ri <- as.integer(factor(row)); ci <- as.integer(factor(col)); ti <- as.integer(factor(trt))
  x <- matrix(0, n, n); x[cbind(ri, ci)] <- obs
  tx <- tapply(obs, ti, sum); sr <- rowSums(x); sc <- colSums(x); grand <- sum(x); es <- grand / n; ex <- grand / n^2
  ssqr <- sum((sr - es)^2) / n; ssqc <- sum((sc - es)^2) / n; ssqtr <- sum((tx - es)^2) / n; ssqtot <- sum((x - ex)^2); ssqres <- ssqtot - ssqr - ssqc - ssqtr
  df <- n - 1; dfres <- (n - 1) * (n - 2); vres <- ssqres / dfres
  want <- list(row_sum = ssqr, row_f = df, row_mean = ssqr / df, col_sum = ssqc, col_f = df, col_mean = ssqc / df, treat_sum = ssqtr, treat_f = df, treat_mean = ssqtr / df,
               res_sum = ssqres, res_f = dfres, res_mean = vres, tot_sum = ssqtot, tot_f = n^2 - 1,
               f_row = ssqr / df / vres, p_row = pf(ssqr / df / vres, df, dfres, lower.tail = FALSE), f_col = ssqc / df / vres, p_col = pf(ssqc / df / vres, df, dfres, lower.tail = FALSE),
               f_treat = ssqtr / df / vres, p_treat = pf(ssqtr / df / vres, df, dfres, lower.tail = FALSE))
  want[names(.more)] <- .more
  case("RptLatin", list(observations = frame(obs), column = frame(col), row = frame(row), treatment = frame(trt)), want)
}
rabbits <- c(7.9, 6.1, 7.5, 6.9, 6.7, 7.3, 8.7, 8.2, 8.1, 8.5, 9.9, 8.3, 7.4, 7.7, 6, 6.8, 7.3, 7.3, 7.4, 7.1, 6.4, 7.7, 6.4, 5.8, 7.1, 8.1, 6.2, 8.5, 6.4, 6.4, 8.2, 5.9, 7.5, 8.5, 7.3, 7.7)
rabbit <- rep(1:6, each = 6); position <- rep(1:6, 6)
order6 <- c(3, 4, 1, 6, 2, 5, 5, 2, 3, 1, 4, 6, 4, 6, 5, 3, 1, 2, 1, 5, 6, 2, 3, 4, 6, 3, 2, 4, 5, 1, 2, 1, 4, 5, 6, 3)
latin(rabbits, position, rabbit, order6)
latin(rabbits, position * 10, rabbit + 100, order6 * 3)                                            # any codes, and in another order of the rows
shuffle <- sample(36); latin(rabbits[shuffle], position[shuffle], rabbit[shuffle], order6[shuffle])
sq3 <- expand.grid(r = 1:3, c = 1:3); sq3$t <- (sq3$r + sq3$c - 2) %% 3 + 1
latin(c(10.2, 11.5, 9.8, 12.1, 10.9, 11.7, 9.5, 10.4, 12.3), sq3$r, sq3$c, sq3$t)                  # three by three
latin(1e6 + rabbits, position, rabbit, order6)                                                     # values far from 0

# ---- crossover trial: two groups, each with a value on the drug and on the placebo, and a baseline that may be given; group 2
# had the placebo first (the program takes group 2's drug column as its second period)
crossover <- function(g1d, g1p, g2d, g2p, g1b = NULL, g2b = NULL, gamma = 0.95, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  keep1 <- !is.na(g1d) & !is.na(g1p) & (if (is.null(g1b)) TRUE else !is.na(g1b)); b1 <- if (is.null(g1b)) 0 else g1b[keep1]
  x1d <- g1d[keep1] - b1; x1p <- g1p[keep1] - b1
  keep2 <- !is.na(g2d) & !is.na(g2p) & (if (is.null(g2b)) TRUE else !is.na(g2b)); b2 <- if (is.null(g2b)) 0 else g2b[keep2]
  x2p <- g2d[keep2] - b2; x2d <- g2p[keep2] - b2
  n1 <- length(x1d); n2 <- length(x2d); dif1 <- x1d - x1p; dif2 <- x2d - x2p; sum1 <- x1d + x1p; sum2 <- x2d + x2p
  all <- c(dif1, -dif2); relse <- sqrt(var(all) / (n1 + n2)); relt <- mean(all) / relse
  var <- (ss(dif1) + ss(dif2)) / (n1 + n2 - 2); se <- sqrt(var * (1 / n1 + 1 / n2)); df <- n1 + n2 - 2
  tt <- (mean(dif1) - mean(dif2)) / se; mag <- (mean(dif1) - mean(dif2)) / 2; crit <- qt(1 - (1 - gamma) / 2, df)
  tp <- (mean(dif1) + mean(dif2)) / se
  vars <- (ss(sum1) + ss(sum2)) / (n1 + n2 - 2); ses <- sqrt(vars * (1 / n1 + 1 / n2)); ts <- (mean(sum1) - mean(sum2)) / ses
  want <- list(grp1_p1 = mean(x1d), grp1_p2 = mean(x1p), grp1_diff = mean(dif1), grp2_p1 = mean(x2d), grp2_p2 = mean(x2p), grp2_diff = mean(dif2),
               relative_diff = mean(all), relative_se = relse, relative_t = relt, relative_df = n1 + n2 - 1, relative_p = tail2(relt, n1 + n2 - 1),
               treatment_diff = mean(dif1) - mean(dif2), treatment_se = se, treatment_mag = mag, treatment_pc = 100 * gamma, treatment_from = mag - crit * se / 2, treatment_to = mag + crit * se / 2,
               treatment_t = tt, treatment_df = df, treatment_p = tail2(tt, df), period_diff = mean(dif1) + mean(dif2), period_se = se, period_t = tp, period_df = df, period_p = tail2(tp, df),
               tpi_sum = mean(sum1) - mean(sum2), tpi_se = ses, tpi_t = ts, tpi_df = df, tpi_p = tail2(ts, df),
               `*warn.rows` = if (sum(!keep1) + sum(!keep2) > 0) 1 else 0)
  want[names(.more)] <- .more
  inputs <- list(gamma = level, group1drug = frame(g1d), group1placebo = frame(g1p)); if (!is.null(g1b)) inputs$group1baseline <- frame(g1b)
  inputs$group2drug <- frame(g2d); inputs$group2placebo <- frame(g2p); if (!is.null(g2b)) inputs$group2baseline <- frame(g2b)
  case("RptCrossover", inputs, want)
}
d1 <- c(8, 14, 8, 9, 11, 3, 6, 0, 13, 10, 7, 13, 8, 7, 9, 10, 2); p1 <- c(5, 10, 0, 7, 6, 5, 0, 0, 12, 2, 5, 13, 10, 7, 0, 6, 2)
d2 <- c(11, 8, 9, 8, 9, 8, 14, 4, 13, 7, 10, 6); p2 <- c(12, 6, 13, 8, 8, 4, 8, 2, 8, 9, 7, 7)
crossover(d1, p1, d2, p2)
crossover(d1, p1, d2, p2, gamma = 0.99); crossover(d1, p1, d2, p2, gamma = 0); crossover(d1, p1, d2, p2, gamma = 1)
crossover(d1, p1, d2, p2, g1b = round(runif(17, 3, 9)), g2b = round(runif(12, 3, 9)))
crossover(c(d1[1:8], NA, d1[10:17]), p1, d2, c(p2[1:5], NA, p2[7:12]))                             # subjects with a missing value are left out
crossover(1e6 + d1, 1e6 + p1, 1e6 + d2, 1e6 + p2)                                                  # values far from 0

# ---- the comparisons after a one way analysis (or a two way one): what the analysis caches, from the definitions
carrier <- function(groups) {
  g <- lapply(groups, function(x) x[!is.na(x)]); n <- sapply(g, length); m <- sapply(g, mean); N <- sum(n)
  list(n = n, mean = m, mse = sum(sapply(g, ss)) / (N - length(g)), df = N - length(g))
}
carrier_two_way <- function(cols) {   # after a randomized blocks analysis: the treatment means, the residual mean square and its degrees of freedom
  m <- do.call(cbind, cols); m <- m[complete.cases(m), , drop = FALSE]; nr <- nrow(m); nc <- ncol(m)
  r <- rowMeans(m); cm <- colMeans(m); gm <- mean(m)
  ssres <- sum((m - outer(r, rep(1, nc)) - outer(rep(1, nr), cm) + gm)^2); dfres <- (nr - 1) * (nc - 1)
  list(n = rep(nr, nc), mean = cm, mse = ssres / dfres, df = dfres)
}
# the rows of contrasts in descending order of the statistic, with the summary of significant differences
contrast_rows <- function(want, rows, labels, alpha, stopword = " {stop}", statname = "t", marker_when_not_significant = TRUE) {
  ord <- order(-rows$stat); rows <- rows[ord, ]; halted <- FALSE
  for (i in seq_len(nrow(rows))) {
    want[[sprintf("*differences[%d].cf1", i)]] <- rows$lab1[i]; want[[sprintf("*differences[%d].cf2", i)]] <- rows$lab2[i]
    want[[sprintf("*differences[%d].delta", i)]] <- rows$delta[i]; want[[sprintf("*differences[%d].%s", i, statname)]] <- rows$stat[i]
    if (!is.null(rows$lci)) { want[[sprintf("*differences[%d].lci", i)]] <- rows$lci[i]; want[[sprintf("*differences[%d].uci", i)]] <- rows$uci[i] }
    if (!is.null(rows$gps)) want[[sprintf("*differences[%d].gps", i)]] <- rows$gps[i]
    want[[sprintf("*differences[%d].p", i)]] <- withroom(rows$p[i], p_room)
    if (marker_when_not_significant && !halted && rows$p[i] >= alpha) { halted <- TRUE; want[[sprintf("*differences[%d].stop_marker", i)]] <- stopword }
    if (!is.null(rows$nottested) && rows$nottested[i]) want[[sprintf("*differences[%d].stop_marker", i)]] <- stopword
  }
  want[["*differences.rows"]] <- nrow(rows)
  # the summary: each label in the order met, with the labels it differs from significantly
  seen <- character(0); sigs <- list(); means <- list()
  for (i in seq_len(nrow(rows))) for (side in 1:2) {
    lab <- if (side == 1) rows$lab1[i] else rows$lab2[i]; other <- if (side == 1) rows$lab2[i] else rows$lab1[i]
    if (!(lab %in% seen)) { seen <- c(seen, lab); sigs[[lab]] <- character(0); means[[lab]] <- if (side == 1) rows$mean1[i] else rows$mean2[i] }
    if (rows$significant[i] && !(other %in% sigs[[lab]])) sigs[[lab]] <- c(sigs[[lab]], other)
  }
  for (i in seq_along(seen)) { want[[sprintf("*summary[%d].cf1", i)]] <- seen[i]; want[[sprintf("*summary[%d].mean", i)]] <- means[[seen[i]]]
    want[[sprintf("*summary[%d].sigs", i)]] <- if (length(sigs[[seen[i]]]) == 0) "none" else paste(sigs[[seen[i]]], collapse = ", ") }
  want[["*summary.rows"]] <- length(seen)
  want
}
pairs_of <- function(k) { p <- NULL; for (i in 1:(k - 1)) for (j in (i + 1):k) p <- rbind(p, c(i, j)); p }

bonferroni <- function(groups, va, vb, comparisons, gamma = 0.95, .more = list()) {
  level <- gamma; gamma <- level_of(gamma)
  cr <- carrier(groups); k <- length(groups); a <- va + 1; b <- vb + 1
  d <- cr$mean[a] - cr$mean[b]; se <- sqrt(cr$mse * (1 / cr$n[b] + 1 / cr$n[a])); t <- d / se
  cit <- qt(1 - (1 - gamma) / 2, cr$df); comp <- max(1, comparisons); gadj <- 1 - (1 - gamma) / comp; citadj <- qt(1 - (1 - gadj) / 2, cr$df)
  want <- list(`a-b` = d, std_err = se, groups = k, pc = 100 * gamma, from = d - cit * se, to = d + cit * se, adj_pc = 100 * gadj, adj_from = d - citadj * se, adj_to = d + citadj * se,
               t = t, df = cr$df, p = tail2(t, cr$df), comp = paste0(comp, " comparison", if (comp == 1) "" else "s"), bonf = (1 - gamma) / comp)
  want[names(.more)] <- .more
  case("RptBonferroni", list(gamma = level, data = frame_of(groups), variables = paste(va, vb, sep = ","), comparisons = comparisons), want)
}
bonferroni(substances, 0, 3, 6); bonferroni(substances, 1, 2, 6, 0.99); bonferroni(expt_unequal, 3, 0, 1); bonferroni(expt, 0, 1, 0, 0.9)
bonferroni(substances, 0, 3, 6, 1); bonferroni(substances, 0, 3, 6, 0)                          # a level of 0% or 100%: 95% is taken

tukey <- function(groups, gamma = 0.95, labels = NULL, after = NULL, .more = list()) {
  cr <- if (is.null(after)) carrier(groups) else carrier_two_way(groups); k <- length(groups); n <- cr$n; m <- cr$mean
  if (is.null(labels)) labels <- paste0("c", 1:k)
  level <- gamma; gamma <- level_of(gamma); alpha <- 1 - gamma; cc <- gamma
  same <- all(abs(n - n[1]) <= 1e-9 * n[1]); q <- srange_q(cc, k, cr$df); qroom <- p_room / srange_dens(q, k, cr$df)
  want <- list(method = if (same) "Tukey" else "Tukey-Kramer", q = withroom(q, qroom), d = withroom(q / sqrt(2), qroom / sqrt(2)), psd = sqrt(cr$mse), cn = n[1], pc = 100 * cc)
  pr <- pairs_of(k); rows <- NULL
  for (r in seq_len(nrow(pr))) {
    i <- pr[r, 1]; j <- pr[r, 2]; delta <- m[i] - m[j]
    t <- if (same) sqrt(cr$mse / n[1]) else sqrt(cr$mse / 2 * (1 / n[j] + 1 / n[i]))
    stat <- abs(delta / t); p <- 1 - srange_p(stat, k, cr$df)
    rows <- rbind(rows, data.frame(lab1 = labels[i], lab2 = labels[j], mean1 = m[i], mean2 = m[j], delta = delta, stat = stat, lci = delta - t * q, uci = delta + t * q, p = p, significant = p < alpha, stringsAsFactors = FALSE))
  }
  # the limits carry the room of q
  want <- contrast_rows(want, rows, labels, alpha)
  ord <- order(-rows$stat); tr <- rows[ord, ]
  for (i in seq_len(nrow(tr))) { t <- (tr$uci[i] - tr$delta[i]) / q; want[[sprintf("*differences[%d].lci", i)]] <- withroom(tr$lci[i], t * qroom + 1e-12); want[[sprintf("*differences[%d].uci", i)]] <- withroom(tr$uci[i], t * qroom + 1e-12) }
  want[names(.more)] <- .more
  inputs <- list(gamma = level, data = frame_of(groups)); if (!is.null(after)) inputs$after <- after
  case("RptTukey", inputs, want)
}
tukey(substances); tukey(expt_unequal); tukey(expt, 0.99); tukey(substances, 0); tukey(substances, 1)
tukey(grass, after = "RptTwoWay")                                                                  # after the randomized blocks analysis

scheffe <- function(groups, gamma = 0.95, after = NULL, .more = list()) {
  cr <- if (is.null(after)) carrier(groups) else carrier_two_way(groups); k <- length(groups); n <- cr$n; m <- cr$mean; labels <- paste0("c", 1:k)
  level <- gamma; gamma <- level_of(gamma); alpha <- 1 - gamma
  dfn <- k - 1; dfd <- cr$df; crit <- sqrt(dfn * qf(alpha, dfn, dfd, lower.tail = FALSE))
  want <- list(critical = crit, pc = 100 * (1 - alpha))
  pr <- pairs_of(k); rows <- NULL
  for (r in seq_len(nrow(pr))) {
    i <- pr[r, 1]; j <- pr[r, 2]; delta <- m[i] - m[j]; se <- sqrt(cr$mse * (1 / n[i] + 1 / n[j])); L <- delta / se
    p <- pf(L^2 / dfn, dfn, dfd, lower.tail = FALSE)
    rows <- rbind(rows, data.frame(lab1 = labels[i], lab2 = labels[j], mean1 = m[i], mean2 = m[j], delta = delta, stat = abs(L), lci = delta - crit * se, uci = delta + crit * se, p = p, significant = p < alpha, stringsAsFactors = FALSE))
  }
  want <- contrast_rows(want, rows, labels, alpha)
  ord <- order(-rows$stat); tr <- rows[ord, ]
  for (i in seq_len(nrow(tr))) want[[sprintf("*differences[%d].p", i)]] <- tr$p[i]   # from F: no room needed
  want[names(.more)] <- .more
  inputs <- list(gamma = level, data = frame_of(groups)); if (!is.null(after)) inputs$after <- after
  case("RptScheffe", inputs, want)
}
scheffe(substances); scheffe(expt_unequal, 0.99); scheffe(substances, 0); scheffe(grass, after = "RptTwoWay")

newman_keuls <- function(groups, gamma = 0.95, after = NULL, .more = list()) {
  cr <- if (is.null(after)) carrier(groups) else carrier_two_way(groups); k <- length(groups); n <- cr$n; m <- cr$mean; labels <- paste0("c", 1:k)
  level <- gamma; gamma <- level_of(gamma); alpha <- 1 - gamma
  se <- sqrt(cr$mse / n[1]); pr <- pairs_of(k); rows <- NULL
  for (r in seq_len(nrow(pr))) {
    i <- pr[r, 1]; j <- pr[r, 2]; delta <- m[i] - m[j]; q <- abs(delta) / se
    a <- min(m[i], m[j]); b <- max(m[i], m[j]); smaller <- sum(m < a); bigger <- sum(m > b); gps <- k - smaller - bigger
    p <- 1 - srange_p(q, gps, cr$df)
    rows <- rbind(rows, data.frame(lab1 = labels[i], lab2 = labels[j], mean1 = m[i], mean2 = m[j], delta = delta, stat = q, absdelta = abs(delta), gps = gps, lo = smaller + 1, hi = k - bigger, p = p, significant = p < alpha, nottested = FALSE, stringsAsFactors = FALSE))
  }
  for (span in k:2) for (i in which(rows$gps == span)) for (j in seq_len(nrow(rows)))
    if (rows$gps[j] > span && rows$lo[j] <= rows$lo[i] && rows$hi[i] <= rows$hi[j] && !rows$significant[j]) { rows$nottested[i] <- TRUE; rows$significant[i] <- FALSE; break }
  # sorted by the absolute difference of the means, the statistic being that over one standard error
  rows$stat <- rows$absdelta
  want <- contrast_rows(list(), rows, labels, alpha, stopword = " {not tested}", statname = "t", marker_when_not_significant = FALSE)
  ord <- order(-rows$absdelta); tr <- rows[ord, ]
  for (i in seq_len(nrow(tr))) want[[sprintf("*differences[%d].t", i)]] <- tr$absdelta[i] / se
  want[names(.more)] <- .more
  inputs <- list(gamma = level, data = frame_of(groups)); if (!is.null(after)) inputs$after <- after
  case("RptNewmanKeuls", inputs, want)
}
newman_keuls(substances); newman_keuls(expt, 0.99); newman_keuls(grass, after = "RptTwoWay"); newman_keuls(substances, 0)
newman_keuls(expt_unequal, .more = list(refused = "TemplateOperationCancelledException: All group sizes must be equal for the Newman-Keuls method."))

# ---- Dunnett: each treatment against the control, with the critical value of the greatest |t| of the k contrasts, whose
# correlations are lambda_i lambda_j, lambda_i = sqrt(n_i / (n_i + n_c)); the probability that every |T_i| is within d is the
# integral over the common part z and over the residual standard deviation s of the product of the normal probabilities
dunnett_p <- function(d, lam, nu) {
  inner <- function(s) sapply(s, function(si) integrate(function(z) dnorm(z) * sapply(z, function(zi) prod(pnorm((d * si - lam * zi) / sqrt(1 - lam^2)) - pnorm((-d * si - lam * zi) / sqrt(1 - lam^2)))), -12, 12, rel.tol = 1e-11, abs.tol = 0, subdivisions = 2000)$value)
  logc <- 0.5 * nu * log(nu) - (0.5 * nu - 1) * log(2) - lgamma(0.5 * nu)
  g <- function(y) { s <- exp(y); exp(logc + nu * y - 0.5 * nu * s^2) * inner(s) }
  ylo <- 0.5 * log(qchisq(1e-30, nu) / nu); yhi <- 0.5 * log(qchisq(1e-30, nu, lower.tail = FALSE) / nu)
  min(1, integrate(g, ylo, yhi, rel.tol = 1e-10, abs.tol = 0, subdivisions = 2000)$value)
}
dunnett_q <- function(cc, lam, nu) uniroot(function(d) dunnett_p(d, lam, nu) - cc, c(0.5, 12), tol = 1e-10)$root
dunnett <- function(groups, ic, gamma = 0.95, .more = list()) {
  cr <- carrier(groups); k <- length(groups); n <- cr$n; m <- cr$mean; labels <- paste0("c", 1:k); c1 <- ic + 1
  level <- gamma; gamma <- level_of(gamma); alpha <- 1 - gamma; cc <- gamma
  others <- setdiff(1:k, c1); lam <- sqrt(n[others] / (n[others] + n[c1])); nu <- cr$df; psd <- sqrt(cr$mse)
  d <- dunnett_q(cc, lam, nu); droom <- 1e-8 * d   # the program's critical value agrees with the integration to a part in a hundred million
  want <- list(d = withroom(d, droom), psd = psd, cn = n[c1], pc = 100 * cc, control = labels[c1])
  rows <- NULL
  for (i in others) { delta <- m[i] - m[c1]; w <- psd * sqrt(1 / n[c1] + 1 / n[i]); t <- delta / w
    rows <- rbind(rows, data.frame(lab = labels[i], n = n[i], delta = delta, lci = delta - d * w, uci = delta + d * w, w = w, p = 1 - dunnett_p(abs(t), lam, nu), stringsAsFactors = FALSE)) }
  rows <- rows[order(-abs(rows$delta)), ]
  for (i in seq_len(nrow(rows))) { want[[sprintf("*differences[%d].level", i)]] <- rows$lab[i]; want[[sprintf("*differences[%d].cn", i)]] <- rows$n[i]; want[[sprintf("*differences[%d].delta", i)]] <- rows$delta[i]
    want[[sprintf("*differences[%d].lci", i)]] <- withroom(rows$lci[i], rows$w[i] * droom + 1e-12); want[[sprintf("*differences[%d].uci", i)]] <- withroom(rows$uci[i], rows$w[i] * droom + 1e-12)
    want[[sprintf("*differences[%d].p", i)]] <- withroom(rows$p[i], 1e-8) }
  want[["*differences.rows"]] <- nrow(rows)
  want[names(.more)] <- .more
  case("RptDunnett", list(gamma = level, data = frame_of(groups), indexvariable = as.character(ic)), want)
}
dunnett(substances, 0); dunnett(expt_unequal, 3); dunnett(substances, 2, 0.99); dunnett(substances, 0, 0)

# ---- equality of variance: Levene's test on the absolute differences from the medians, Bartlett's test, and Welch's analysis of
# variance for unequal variances
equality <- function(groups, .more = list()) {
  g <- lapply(groups, function(x) x[!is.na(x)]); n <- sapply(g, length); k <- length(g); N <- sum(n)
  z <- lapply(g, function(x) abs(x - median(x))); zm <- sapply(z, mean); zbar <- mean(unlist(z))
  ssg <- sum(n * (zm - zbar)^2); sse <- sum(sapply(z, ss)); f <- (ssg / (k - 1)) / (sse / (N - k))
  v <- sapply(g, var); df <- n - 1; sbar <- sum(v * df) / sum(df); M <- sum(df) * log(sbar) - sum(df * log(v)); C <- 1 + (sum(1 / df) - 1 / sum(df)) / (3 * (k - 1)); x2 <- M / C
  w <- n / v; W <- sum(w); xbar <- sum(w * sapply(g, mean)) / W; fa <- sum(w * (sapply(g, mean) - xbar)^2); fc <- sum((1 - w / W)^2 / df)
  fw <- fa / (k - 1) / (1 + 2 * (k - 2) / (k^2 - 1) * fc); dfd <- (k^2 - 1) / (3 * fc)
  want <- list(f = f, df1 = k - 1, df2 = N - k, pLevene = pf(f, k - 1, N - k, lower.tail = FALSE), x2 = x2, df = k - 1, pBartlett = pchisq(x2, k - 1, lower.tail = FALSE),
               fWelch = fw, dfnWelch = k - 1, dfdWelch = dfd, pWelch = pf(fw, k - 1, dfd, lower.tail = FALSE))
  want[names(.more)] <- .more
  case("RptEqualityOfVariance", list(data = frame_of(groups)), want)
}
equality(expt); equality(expt_unequal); equality(substances)
equality(list(c(1.2, 3.4, NA, 2.2, 2.9), c(5.1, NA, 4.4, 9.2), c(0.5, 2.9, 6.0, 1.1)))
equality(list(1e6 + rnorm(8, 0, 1), 1e6 + rnorm(10, 0, 4), 1e6 + rnorm(7, 0, 2)))                # values far from 0
equality(list(c(1, 2), c(4, 9), c(3, 3.5)))                                                        # groups of two

# ---- what is refused, with the words of the message
refused <- function(report, inputs, text) case(report, inputs, list(refused = paste0("TemplateOperationCancelledException: ", text)))
groups_msg <- "The one way ANOVA needs at least two groups, each with at least one observation."
refused("RptOneWay", list(data = "1,2,3|*,*,*|4,5,6"), groups_msg)
refused("RptOneWay", list(data = "1,2,3"), groups_msg)
refused("RptOneWay", list(data = "1,2,3|4,5,6,7|"), groups_msg)
refused("RptOneWay", list(data = "1|2|3"), "There are no residual degrees of freedom: at least one group needs two observations or more.")
refused("RptOneWay", list(data = "5,5,5|5,5|5,5,5,5"), "The analysis cannot be calculated when all of the observations are the same.")
rows_msg <- "The two way ANOVA needs at least two columns and at least two rows with a value in every column."
refused("RptTwoWay", list(data = "1,*|2,3|3,4"), rows_msg)
refused("RptTwoWay", list(data = "1,2,3"), rows_msg)
refused("RptTwoWay", list(data = "5,5,5|5,5,5|5,5,5"), "The analysis cannot be calculated when all of the observations are the same.")
refused("RptTwoWay", list(data = "1,2,3|1,2,3"), "The analysis cannot be calculated when the residual sum of squares is 0: the values are exactly the sums of row and column effects.")
refused("RptTwoMulti", list(data2d = "5,5|5,5~5,5|5,5"), "The analysis cannot be calculated when all of the observations are the same.")
refused("RptTwoMulti", list(data2d = "1,1|3,3~2,2|6,6"), "The analysis cannot be calculated when the residual sum of squares is 0: the repeats of every cell are the same.")
refused("RptTwoMulti", list(data2d = "1,2|3,5"), "The replicated two way ANOVA needs at least two blocks and two treatments, each cell with at least one observation.")
refused("RptTwoNest", list(data2d = "5,5|5,5~5,5|5,5"), "The analysis cannot be calculated when all of the observations are the same.")
refused("RptTwoNest", list(data2d = "1,1|3,3~2,2|6,6"), "The analysis cannot be calculated when the residual sum of squares is 0: the observations of every subgroup are the same.")
refused("RptTwoNest", list(data2d = "1,2|3,4"), "Each subgroup must contain at least one observation, and there must be at least two groups.")
refused("RptEqualityOfVariance", list(data = "5,5,5|5,5|5,5,5,5"), "The equality of variance tests cannot be calculated when all of the observations of a group are the same.")
refused("RptEqualityOfVariance", list(data = "1,2,3"), "The equality of variance tests need at least two groups.")
refused("RptEqualityOfVariance", list(data = "1,2,3|4"), "Each group needs at least two observations for the equality of variance tests.")
refused("RptTukey", list(gamma = 0.95, data = "1|2|3"), "These comparisons need at least one residual degree of freedom.")
refused("RptLatin", list(observations = "1,2,3,4", column = "1,1,2,2", row = "1,2,1,2", treatment = "1,2,2,1"), "A Latin square needs at least three rows and columns to leave a residual degree of freedom.")

writeLines(cases, args[1]); writeLines(expected, args[2])
cat(length(cases), "cases,", length(expected), "figures\n")
