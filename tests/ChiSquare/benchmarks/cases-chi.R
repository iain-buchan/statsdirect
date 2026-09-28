# The cases of the benchmarks of the Chi-square Tests menu: a line has the kind of the analysis, a name, and what the analysis is
# given.
#   chi22   a b c d, confidence level, study (0 case-control, 1 cohort, 2 neither), Fisher's exact test asked for (0, 1)
#   chi2k   trend (0 without, 1 scores 1 to k, 2 scores given), k, then for each row: successes, failures, score
#   sim2k   trend, k, tables to draw, seed, confidence level, then the rows as for chi2k
#   mantel  confidence level, exact method (0, 1), k, then the four counts of each table as they are typed
#   woolf   confidence level, intermediates shown (0, 1), k, then the four counts of each table as they are typed
#   woolfws confidence level, intermediates shown, k, then for each table: size and number with the outcome of each group
# usage: Rscript --vanilla cases-chi.R cases-chi.txt
args <- commandArgs(trailingOnly = TRUE)
set.seed(20260929)
out <- character(0)
count <- c(chi22 = 0, chi2k = 0, sim2k = 0, mantel = 0, woolf = 0, woolfws = 0)
add <- function(kind, ...) {
  count[kind] <<- count[kind] + 1
  out <<- c(out, paste(c(kind, sprintf("%s%03d", kind, count[kind]), format(c(...), scientific = FALSE, trim = TRUE, digits = 15)), collapse = "\t"))
}
levels <- c(0.95, 0.99, 0.9)

# 2 by 2 tables: small, of tens, of hundreds, of thousands, of millions; near to and far from independence
table22 <- function(most, association) {
  n <- sample(4:most, 1)
  p1 <- runif(1, 0.05, 0.95); p2 <- runif(1, 0.05, 0.95)
  if (association == 0) p2 <- p1
  n1 <- max(1, round(n * runif(1, 0.2, 0.8))); n2 <- max(1, n - n1)
  a <- rbinom(1, n1, p1); c <- rbinom(1, n2, p2)
  c(a, n1 - a, c, n2 - c)
}
tables <- list()
for (i in 1:60) tables[[length(tables) + 1]] <- table22(40, i %% 2)
for (i in 1:40) tables[[length(tables) + 1]] <- table22(300, i %% 2)
for (i in 1:30) tables[[length(tables) + 1]] <- table22(3000, i %% 2)
for (i in 1:12) tables[[length(tables) + 1]] <- table22(40000, i %% 2)
for (i in 1:6) tables[[length(tables) + 1]] <- table22(3000000, i %% 2)
for (i in 1:4) tables[[length(tables) + 1]] <- table22(2000000000, i %% 2)
# an empty cell; all in a diagonal; the difference from what is expected below a half; totals of 19, 20 and 21; an expected count
# of 5 and just below it; an empty row or column
for (t in list(c(0, 10, 5, 5), c(10, 0, 5, 5), c(5, 5, 0, 10), c(5, 5, 10, 0), c(5, 0, 0, 5), c(0, 5, 5, 0), c(5, 5, 5, 6), c(5, 5, 5, 5), c(5, 5, 5, 4),
               c(5, 5, 5, 5.2), c(10, 10, 10, 10), c(10, 10, 10, 9), c(1, 1, 1, 1), c(1, 0, 0, 1), c(3, 1, 1, 4), c(12, 2, 3, 9), c(100, 0, 0, 100),
               c(2000, 1, 1, 2000), c(1, 5000, 5000, 1), c(41, 216, 64, 180), c(64, 180, 41, 216), c(72, 20, 684, 553), c(10, 0, 3, 12), c(0, 5, 7, 12),
               c(10, 2, 0, 12), c(10, 5, 3, 0), c(50, 1, 50, 99), c(0, 0, 3, 4), c(3, 4, 0, 0), c(0, 3, 0, 4), c(3, 0, 4, 0), c(0, 0, 0, 0),
               c(100000, 200000, 150000, 180000), c(600000, 700000, 650000, 800000), c(2.5, 3.5, 4.5, 5.5), c(10.4, 3.6, 7.2, 12.49), c(0.4, 5, 6, 0.3)))
  tables[[length(tables) + 1]] <- t
i <- 0
for (t in tables) {
  i <- i + 1
  add("chi22", t, levels[1 + i %% 3], 0, 0)
  add("chi22", t, levels[1 + i %% 3], 1, i %% 2)
  add("chi22", t, levels[1 + i %% 3], 2, (i + 1) %% 2)
}

# 2 by k tables
table2k <- function(k, most, trend) {
  n <- pmax(1, round(runif(k, 0.2, 1) * most))
  p <- runif(1, 0.1, 0.9) + trend * (1:k) / k * runif(1, -0.4, 0.4) + (1 - trend) * runif(k, -0.1, 0.1)
  p <- pmin(0.98, pmax(0.02, p))
  s <- rbinom(k, n, p)
  rbind(s, n - s)
}
scores <- function(k) {
  what <- sample(1:4, 1)
  if (what == 1) return(cumsum(sample(1:3, k, replace = TRUE)))
  if (what == 2) return(round(sort(runif(k, -5, 5)), 2))
  if (what == 3) return(rev(1:k))
  sample(c(0, 1, 2, 5, 10), k, replace = TRUE)
}
give2k <- function(kind, t, s, ...) {
  k <- ncol(t)
  for (type in 0:2) {
    if (kind == "sim2k" && type == 0) next
    add(kind, type, k, ..., as.vector(rbind(t[1, ], t[2, ], if (type == 2) s else 1:k)))
  }
}
for (i in 1:40) { k <- sample(2:8, 1); give2k("chi2k", table2k(k, 30, i %% 2), scores(k)) }
for (i in 1:30) { k <- sample(2:8, 1); give2k("chi2k", table2k(k, 400, i %% 2), scores(k)) }
for (i in 1:15) { k <- sample(2:12, 1); give2k("chi2k", table2k(k, 20000, i %% 2), scores(k)) }
for (i in 1:5) { k <- sample(2:30, 1); give2k("chi2k", table2k(k, 50000000, i %% 2), scores(k)) }
# the example of the help; the same the other way up; two rows; one row; an empty cell; a column with nothing; a row with nothing;
# the same proportion in every row; scores that are all the same; scores that are the same for some rows; a trend that is all of the
# chi-square; none of it; counts that are not whole numbers
special <- list(
  list(rbind(c(19, 29, 24), c(497, 560, 269)), c(1, 2, 5)), list(rbind(c(24, 29, 19), c(269, 560, 497)), c(1, 2, 5)),
  list(rbind(c(8, 3), c(2, 9)), c(0, 1)), list(rbind(c(8), c(2)), c(1)), list(rbind(c(0, 3, 6), c(9, 5, 2)), c(1, 2, 4)),
  list(rbind(c(0, 0, 0), c(9, 5, 2)), c(1, 2, 4)), list(rbind(c(4, 5, 6), c(0, 0, 0)), c(1, 2, 4)), list(rbind(c(4, 0, 6), c(3, 0, 2)), c(1, 2, 4)),
  list(rbind(c(5, 10, 20), c(10, 20, 40)), c(1, 2, 3)), list(rbind(c(5, 8, 20), c(10, 20, 40)), c(2, 2, 2)), list(rbind(c(5, 8, 20, 3), c(10, 20, 40, 9)), c(1, 1, 2, 2)),
  list(rbind(c(10, 20, 30), c(30, 20, 10)), c(1, 2, 3)), list(rbind(c(10, 30, 10), c(30, 10, 30)), c(1, 2, 3)),
  list(rbind(c(2.5, 3.5, 7.25), c(6.5, 4.5, 1.75)), c(1, 2, 3)), list(rbind(c(1, 1, 1, 1, 1), c(1, 1, 1, 1, 1)), c(1, 2, 3, 4, 5)),
  list(rbind(c(1, 0, 0, 0, 3), c(0, 2, 2, 2, 0)), c(1, 2, 3, 4, 5)), list(rbind(c(1000000, 1000100, 1000200), c(1000000, 999900, 999800)), c(1, 2, 3)))
for (s in special) give2k("chi2k", s[[1]], s[[2]])

# tables for the simulation: small enough for every table with the same totals to be listed
for (i in 1:10) { k <- sample(3:5, 1); give2k("sim2k", table2k(k, 12, i %% 2), scores(k), 200000, sample(1:100000, 1), 0.99) }
# tables of which many have the same statistic as the one observed: the same total in every row
for (t in list(rbind(c(1, 3, 5), c(5, 3, 1)), rbind(c(5, 3, 1), c(1, 3, 5)), rbind(c(2, 4, 2), c(6, 4, 6)), rbind(c(3, 3, 3, 3), c(3, 3, 3, 3)), rbind(c(0, 2, 4, 6), c(6, 4, 2, 0)),
               rbind(c(7, 1, 1, 7), c(1, 7, 7, 1)), rbind(c(10, 12, 14), c(10, 8, 6)), rbind(c(1, 2, 3, 4, 5), c(5, 4, 3, 2, 1)), rbind(c(40, 50, 60), c(60, 50, 40))))
  give2k("sim2k", t, c(-1, 0, 1, 2, 3)[seq_len(ncol(t))] * 10, 200000, sample(1:100000, 1), 0.95)

# series of 2 by 2 tables
series <- function(k, most, empty) {
  v <- numeric(0)
  for (i in 1:k) {
    t <- table22(most, sample(0:1, 1))
    if (empty && runif(1) < 0.3) t[sample(1:4, 1)] <- 0
    v <- c(v, t)
  }
  v
}
sets <- list()
for (i in 1:30) sets[[length(sets) + 1]] <- series(sample(1:10, 1), 40, i %% 3 == 0)
for (i in 1:20) sets[[length(sets) + 1]] <- series(sample(2:10, 1), 400, i %% 4 == 0)
for (i in 1:8) sets[[length(sets) + 1]] <- series(sample(2:20, 1), 50000, FALSE)
# the example of the help; tables that are all the same; one table; tables with an empty row or column; counts that are not whole
sets[[length(sets) + 1]] <- c(83, 3, 72, 14, 90, 3, 227, 43, 129, 7, 81, 19, 412, 32, 299, 131, 1350, 7, 1296, 61, 60, 3, 106, 27, 459, 18, 534, 81, 499, 19, 462, 56, 451, 39, 1729, 636, 260, 5, 259, 28)
sets[[length(sets) + 1]] <- rep(c(12, 7, 5, 14), 4)
sets[[length(sets) + 1]] <- c(12, 7, 5, 14)
sets[[length(sets) + 1]] <- c(12, 7, 5, 14, 0, 0, 4, 9, 3, 8, 6, 2)
sets[[length(sets) + 1]] <- c(12, 7, 5, 14, 4, 9, 0, 0, 3, 8, 6, 2)
sets[[length(sets) + 1]] <- c(12, 7, 5, 14, 0, 9, 0, 5, 3, 8, 6, 2)
sets[[length(sets) + 1]] <- c(12, 7, 5, 14, 9, 0, 5, 0, 3, 8, 6, 2)
sets[[length(sets) + 1]] <- c(0, 0, 4, 9)
sets[[length(sets) + 1]] <- c(0, 0, 4, 9, 0, 3, 0, 8)
sets[[length(sets) + 1]] <- c(5, 0, 0, 7, 3, 0, 0, 4)
sets[[length(sets) + 1]] <- c(0, 5, 7, 0, 0, 3, 4, 0)
sets[[length(sets) + 1]] <- c(5, 0, 2, 7, 3, 0, 1, 4)
sets[[length(sets) + 1]] <- c(2.5, 3.5, 4.5, 5.5, 10.4, 3.6, 7.2, 12.49)
sets[[length(sets) + 1]] <- c(1, 1, 1, 1, 1, 1, 1, 1)
i <- 0
for (s in sets) {
  i <- i + 1
  k <- length(s) / 4
  # the exact method takes minutes for many tables of thousands: it is asked for where the tables have fewer than 20,000 subjects
  add("mantel", levels[1 + i %% 3], as.numeric(sum(s) < 20000), k, s)
  add("mantel", levels[1 + i %% 3], 0, k, s)
  add("woolf", levels[1 + i %% 3], 1, k, s)
  add("woolf", levels[1 + i %% 3], 0, k, s)
  # the worksheet has the groups in columns: the first row of the table is the first group
  w <- numeric(0)
  for (j in 1:k) { t <- s[(4 * j - 3):(4 * j)]; w <- c(w, t[1] + t[2], t[1], t[3] + t[4], t[3]) }
  add("woolfws", levels[1 + i %% 3], 1, k, w)
}
writeLines(out, args[1])
cat(length(out), "cases:", paste(names(count), count, collapse = ", "), "\n")
