# The cases of the benchmarks of the Proportions menu: a line has the kind of the analysis, a name, and what the analysis is given
# (single: n, r, the proportion of the null hypothesis, confidence level; paired: n, the numbers responding in both, in the first
# only and in the second only, confidence level; unpaired: n and r of each of two samples, confidence level).
# usage: Rscript --vanilla cases-prop.R cases-prop.txt
args <- commandArgs(trailingOnly = TRUE)
set.seed(20261001)
out <- character(0)
count <- c(single = 0, paired = 0, unpaired = 0)
add <- function(kind, ...) {
  count[kind] <<- count[kind] + 1
  out <<- c(out, paste(c(kind, sprintf("%s%03d", kind, count[kind]), format(c(...), scientific = FALSE, trim = TRUE, digits = 15)), collapse = "\t"))
}
levels <- c(0.95, 0.99, 0.9)

# a single proportion: from 1 observation to a thousand million; none, all and some responding; null proportions from 0 to 1,
# near to and far from the proportion observed
for (n in c(1, 2, 3, 5, 10, 17, 30, 100, 101, 1000, 1074, 1075, 5000, 100000, 1000000, 1000001, 20000000, 1000000000)) {
  for (r in unique(c(0, 1, floor(n / 2), ceiling(n / 2), floor(0.4 * n), floor(0.1 * n), floor(0.49 * n), n - 1, n))) {
    if (r < 0 || r > n) next
    for (pi in c(0.5, sample(c(0.1, 0.3, 0.75, 0.01, 0.999, 0, 1, r / n), 2)))
      add("single", n, r, pi, sample(levels, 1))
  }
}
for (i in 1:150) { n <- sample(1:3000, 1); add("single", n, sample(0:n, 1), round(runif(1), 3), sample(levels, 1)) }
# close to what is expected, in large samples
for (n in c(40000, 1000000, 1000001, 50000000)) for (pi in c(0.5, 0.2, 0.003)) for (z in c(-6, -3, -1, -0.2, 0, 0.3, 2, 5, 9, 40))
  add("single", n, max(0, min(n, round(n * pi + z * sqrt(n * pi * (1 - pi))))), pi, sample(levels, 1))

# paired proportions: the pairs that differ are s and t
for (i in 1:120) {
  m <- sample(c(10, 60, 600, 5000, 200000), 1)
  r <- sample(0:m, 1); s <- sample(0:m, 1); t <- sample(0:m, 1); d <- sample(0:m, 1)
  add("paired", r + s + t + d, r, s, t, sample(levels, 1))
}
for (v in list(c(50, 20, 12, 2), c(50, 20, 2, 12), c(50, 40, 0, 0), c(1, 0, 1, 0), c(1, 0, 0, 1), c(1, 1, 0, 0), c(1, 0, 0, 0), c(40, 5, 10, 10), c(400, 100, 150, 100), c(30, 0, 12, 0),
               c(30, 0, 0, 12), c(30, 10, 20, 0), c(30, 0, 30, 0), c(30, 0, 15, 15), c(2000, 400, 537, 537), c(2000, 400, 530, 544), c(2000, 300, 600, 474), c(2000, 300, 600, 475),
               c(2500, 700, 600, 500), c(3000, 200, 1400, 1300), c(1000000, 300000, 200000, 199000), c(1000000, 300000, 250000, 249900), c(900000000, 1000, 400000000, 399990000),
               c(5000, 0, 5000, 0), c(5000, 0, 0, 5000), c(3000, 1000, 2000, 0), c(100000, 0, 50400, 49600)))
  add("paired", v, sample(levels, 1))

# two independent proportions
for (i in 1:160) {
  m <- sample(c(8, 40, 400, 6000, 300000), 1)
  n1 <- sample(1:m, 1); n2 <- sample(1:m, 1)
  same <- i %% 2
  p1 <- runif(1, 0.02, 0.98); p2 <- if (same) p1 else runif(1, 0.02, 0.98)
  add("unpaired", n1, rbinom(1, n1, p1), n2, rbinom(1, n2, p2), sample(levels, 1))
}
for (v in list(c(257, 41, 244, 64), c(244, 64, 257, 41), c(10, 0, 10, 0), c(10, 10, 10, 10), c(10, 0, 10, 10), c(10, 10, 10, 0), c(10, 0, 12, 5), c(10, 10, 12, 5), c(1, 0, 1, 1), c(1, 1, 1, 1),
               c(1, 0, 1, 0), c(5, 2, 7, 3), c(1000, 500, 1000, 500), c(1000, 500, 1000, 501), c(3000, 1000, 2500, 900), c(40000, 12000, 50000, 15300), c(2000000, 600000, 3000000, 901000),
               c(50000000, 20000000, 60000000, 24010000), c(900000000, 300000000, 1000000000, 333300000), c(257.4, 41.3, 244.2, 64.4), c(10.5, 2.5, 11.5, 6.5), c(3, 0, 1000000, 5), c(2, 2, 900000, 7)))
  add("unpaired", v, sample(levels, 1))
writeLines(out, args[1])
cat(length(out), "cases:", paste(names(count), count, collapse = ", "), "\n")
