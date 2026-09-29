# The cases of the benchmarks of the Rates menu: a line has the kind of the analysis, a name, the confidence level, and what the
# analysis is given; a list of numbers, one for each stratum, has commas between them.
#   two     level a b pt1 pt2 cml (1 or 0)
#   smr     level nunit dead rates times screen (1: the rates and the times in one grid)
#   direct  level nunit events times reference screen (1: the three columns in one grid)
#   stdrr   level nunit model (poisson or binomial) a pt1 b pt2 reference
#   rate    level events time
# usage: Rscript --vanilla cases-rates.R cases-rates.txt
args <- commandArgs(trailingOnly = TRUE)
set.seed(20260929)
out <- character(0)
count <- c(two = 0, smr = 0, direct = 0, stdrr = 0, rate = 0)
num <- function(x) format(x, scientific = FALSE, trim = TRUE, digits = 15)
list_of <- function(x) paste(ifelse(is.na(x), "NA", num(x)), collapse = ",")
add <- function(kind, ...) {
  count[kind] <<- count[kind] + 1
  out <<- c(out, paste(c(kind, sprintf("%s%03d", kind, count[kind]), ...), collapse = "\t"))
}
levels <- c(0.95, 0.99, 0.9)

# ---- two crude rates: events from none to a million, person-time from a fraction to thousands of millions
for (i in 1:150) {
  size <- sample(c(5, 40, 300, 5000, 200000), 1)
  a <- sample(0:size, 1); b <- sample(0:size, 1)
  if (a + b == 0) a <- 1
  pt1 <- signif(runif(1, 0.5, 20) * max(a, 1) * sample(c(0.01, 1, 100, 10000), 1), 6); pt2 <- signif(pt1 * runif(1, 0.2, 5), 6)
  add("two", sample(levels, 1), num(a), num(b), num(pt1), num(pt2), i %% 2)
}
for (v in list(c(30, 60, 54308.7, 51477.5, 1), c(0, 12, 100, 100, 1), c(12, 0, 100, 100, 1), c(0, 1, 5, 7, 1), c(1, 0, 5, 7, 1), c(7, 7, 100, 100, 1), c(10, 20, 50, 100, 1), c(10, 20, 50, 100, 0),
               c(400, 0, 9, 1, 1), c(0, 400, 9, 1, 1), c(400000, 380000, 1e9, 1e9, 1), c(500000, 499000, 2.5e8, 2.5e8, 0), c(3, 5, 0.004, 0.009, 1), c(2.5, 7.5, 40, 90, 0), c(1, 1, 1, 1, 1),
               c(1000, 3, 50, 5000, 1), c(3, 1000, 5000, 50, 1), c(25, 25, 1e-6, 2e-6, 1), c(1e6, 1e6, 3e6, 3.1e6, 0)))
  add("two", sample(levels, 1), num(v[1]), num(v[2]), num(v[3]), num(v[4]), v[5])

# ---- indirect standardization: reference rates by a multiplier, person-time of the index population, deaths observed
for (i in 1:120) {
  k <- sample(1:20, 1)
  nunit <- sample(c(1, 100, 1000, 100000, 1000000), 1)
  rates <- signif(runif(k, 0.00001, 0.02) * nunit, 5)
  times <- round(runif(k, 1, sample(c(50, 3000, 1e6), 1)), sample(0:1, 1))
  expected <- sum(rates / nunit * times)
  dead <- if (i %% 7 == 0) 0 else max(0, round(expected * runif(1, 0.2, 3) + sample(0:3, 1)))
  if (i %% 9 == 0 && k > 2) rates[sample(1:k, 1)] <- NA
  add("smr", sample(levels, 1), num(nunit), num(dead), list_of(rates), list_of(times), i %% 2)
}
for (v in list(list(1000000, 14, c(5.859, 13.050, 46.937, 161.503, 271.358), c(1080, 12860, 11510, 10330, 7790)), list(1, 0, c(0.001, 0.002), c(100, 300)), list(1, 1, c(0.001, 0.002), c(100, 300)),
               list(1000, 250000, c(3, 5, 8), c(2e7, 3e7, 1e7)), list(1, 3, c(0.5), c(4)), list(100000, 40, c(12, 0, 30), c(50000, 80000, 0)), list(1, 2000000, c(0.01, 0.02), c(6e7, 7e7)),
               list(1, 5, c(1e-9, 2e-9), c(100, 100)), list(1, 100, c(0.2, 0.3), c(1000, 2000))))
  for (screen in 0:1) add("smr", sample(levels, 1), num(v[[1]]), num(v[[2]]), list_of(v[[3]]), list_of(v[[4]]), screen)

# ---- direct standardization: events and person-time of the index population, sizes of the reference population
for (i in 1:120) {
  k <- sample(1:20, 1)
  nunit <- sample(c(1, 1000, 100000), 1)
  times <- round(runif(k, 20, sample(c(200, 5000, 1e6), 1)))
  events <- rbinom(k, times, runif(k, 0, sample(c(0.002, 0.05, 0.6), 1)))
  if (i %% 8 == 0) events[] <- 0
  if (i %% 11 == 0) events[sample(1:k, 1)] <- times[sample(1:k, 1)][1]
  events <- pmin(events, times)
  ref <- if (i %% 3 == 0) signif(prop.table(runif(k)), 4) else round(runif(k, 100, 1e6))
  if (i %% 10 == 0 && k > 1) ref[sample(1:k, 1)] <- 0
  if (i %% 13 == 0 && k > 2) events[sample(1:k, 1)] <- NA
  add("direct", sample(levels, 1), num(nunit), list_of(events), list_of(times), list_of(ref), if (any(is.na(events))) 0 else i %% 2)
}
for (v in list(list(100000, c(1, 0, 1, 2, 8, 21, 46, 103, 254, 371, 300), c(38, 150, 322, 344, 443, 379, 256, 189, 136, 57, 9) * 1000, c(15343, 64718, 170355, 181677, 162066, 139237, 117811, 80294, 48426, 17303, 2770)),
               list(1, c(5), c(100), c(1)), list(1000, c(0, 0), c(100, 200), c(1, 1)), list(1, c(100, 200), c(100, 200), c(1, 3)), list(1, c(3, 9), c(40.5, 77.25), c(0.4, 0.6)),
               list(1, c(250000, 300000), c(2e7, 3e7), c(1e6, 2e6)), list(1, c(7, 2), c(3, 1.5), c(1, 1))))
  for (screen in 0:1) add("direct", sample(levels, 1), num(v[[1]]), list_of(v[[2]]), list_of(v[[3]]), list_of(v[[4]]), screen)

# ---- two populations standardized and compared
for (i in 1:120) {
  k <- sample(1:12, 1)
  nunit <- sample(c(1, 1000, 100000), 1)
  pt1 <- round(runif(k, 20, sample(c(300, 5000, 1e6), 1)), 1); pt2 <- round(pt1 * runif(k, 0.3, 3), 1)
  top <- sample(c(0.003, 0.05, 0.5), 1)
  a <- rbinom(k, floor(pt1), runif(k, 0, top)); b <- rbinom(k, floor(pt2), runif(k, 0, top))
  if (i %% 6 == 0) a[sample(1:k, 1)] <- 0
  if (i %% 7 == 0) b[sample(1:k, 1)] <- 0
  if (i %% 23 == 0) a[] <- 0
  if (i %% 29 == 0) b[] <- 0
  ref <- if (i %% 3 == 0) signif(prop.table(runif(k)), 4) else round(runif(k, 100, 1e6))
  if (i %% 10 == 0 && k > 1) ref[sample(1:k, 1)] <- 0
  add("stdrr", sample(levels, 1), num(nunit), c("poisson", "binomial")[1 + i %% 2], list_of(a), list_of(pt1), list_of(b), list_of(pt2), list_of(ref))
}
example <- list(c(2, 55, 32, 21, 27, 19, 25, 9), c(285.1, 4179.1, 3291.2, 1994.7, 1498.9, 763.5, 254.4, 46.7), c(267, 791, 589, 759, 1237, 1924, 3066, 3559),
               c(442057, 493449, 321348, 229076, 203168, 144202, 79262, 32315) * 10, c(442057, 493449, 321348, 229076, 203168, 144202, 79262, 32315))
for (model in c("poisson", "binomial")) {
  add("stdrr", 0.95, 1000, model, list_of(example[[1]]), list_of(example[[2]]), list_of(example[[3]]), list_of(example[[4]]), list_of(example[[5]]))
  add("stdrr", 0.95, 1, model, "5", "100", "9", "150", "1")
  add("stdrr", 0.99, 1, model, "5,0", "100,80", "9,4", "150,90", "1,2")
  add("stdrr", 0.95, 1, model, "5,3", "100,80", "9,0", "150,90", "1,2")
  add("stdrr", 0.95, 1, model, "0,0", "100,80", "9,4", "150,90", "1,2")
  add("stdrr", 0.9, 1, model, "5,3", "100,80", "0,0", "150,90", "1,2")
}

# ---- the confidence interval of a rate
for (v in list(c(14, 400), c(0, 10), c(1, 1e6), c(250000, 3e7), c(3.5, 12), c(1e7, 1e9)))
  add("rate", sample(levels, 1), num(v[1]), num(v[2]))

# ---- direct standardization of strata with more events than person-time: a rate above 1, which the binomial model has no place for
for (v in list(list(1, c(3, 400), c(50, 100), c(1, 3)), list(1000, c(12, 7, 30), c(4.5, 9, 60), c(100, 200, 300)), list(1, c(250000, 2), c(1000, 50), c(0.3, 0.7)),
               list(100000, c(0, 51, 8), c(20, 50, 400), c(5, 0, 9))))
  for (screen in 0:1) add("direct", sample(levels, 1), num(v[[1]]), list_of(v[[2]]), list_of(v[[3]]), list_of(v[[4]]), screen)
writeLines(out, args[1])
cat(length(out), "cases:", paste(names(count), count, collapse = ", "), "\n")
