# Sets of studies with two groups and a binary outcome, for the odds ratio, Peto odds ratio, relative risk and risk difference:
# written as a file of cases (events and total of the first group, events and total of the second, a row for each study)
args <- commandArgs(trailingOnly = TRUE)
out <- file(args[1], "w")
put <- function(name, x, level = 0.95) {
  cat(sprintf("case\t%s\tbin\t%d\t%s\n", name, nrow(x), format(level)), file = out)
  write.table(x, out, sep = "\t", row.names = FALSE, col.names = FALSE, quote = FALSE)
}
# the example of the help
put("help", cbind(c(83, 90, 129, 412, 1350, 60, 459, 499, 451, 260), c(155, 317, 210, 711, 2646, 166, 993, 961, 2180, 519),
                  c(3, 3, 7, 32, 7, 3, 18, 19, 39, 5), c(17, 46, 26, 163, 68, 30, 99, 75, 675, 33)))
set.seed(4711)
draw <- function(k, size, p1, p2, spread = 0.5) {
  n1 <- round(size * exp(rnorm(k, 0, 0.7))) + 2
  n2 <- round(size * exp(rnorm(k, 0, 0.7))) + 2
  shift <- rnorm(k, 0, spread)
  q1 <- plogis(qlogis(p1) + shift + rnorm(k, 0, 0.3))
  q2 <- plogis(qlogis(p2) + shift)
  cbind(rbinom(k, n1, q1), n1, rbinom(k, n2, q2), n2)
}
n <- 0
for (k in c(1, 2, 3, 4, 6, 10)) for (size in c(15, 400)) for (p in list(c(0.3, 0.4), c(0.05, 0.1))) {
  n <- n + 1
  put(sprintf("drawn%03d", n), draw(k, size, p[1], p[2]), c(0.95, 0.9, 0.99)[1 + n %% 3])
}
# sparse sets: cells of nothing
for (i in 1:12) {
  k <- sample(c(3, 5, 8), 1)
  put(sprintf("sparse%02d", i), draw(k, sample(c(8, 20), 1), 0.04, 0.08))
}
# every study without an event in the second group; every study without an event in the first
x <- draw(6, 30, 0.2, 0.2); x[, 3] <- 0; put("none.second", x)
x <- draw(6, 30, 0.2, 0.2); x[, 1] <- 0; put("none.first", x)
# every subject of the first group with an event; every subject of the second
x <- draw(6, 30, 0.2, 0.2); x[, 1] <- x[, 2]; put("all.first", x)
x <- draw(6, 30, 0.2, 0.2); x[, 3] <- x[, 4]; put("all.second", x)
# studies without an event in either group, and with an event in every subject, among others
x <- draw(7, 25, 0.3, 0.3); x[2, c(1, 3)] <- 0; x[5, c(1, 3)] <- 0; put("double.zero", x)
x <- draw(7, 25, 0.3, 0.3); x[3, 1] <- x[3, 2]; x[3, 3] <- x[3, 4]; put("double.all", x)
# a study of one subject in each group; a study with a group of nobody
x <- draw(5, 40, 0.3, 0.4); x[2, ] <- c(1, 1, 0, 1); put("one.each", x)
x <- draw(5, 40, 0.3, 0.4); x[4, ] <- c(0, 0, 3, 10); put("empty.group", x)
x <- draw(5, 40, 0.3, 0.4); x[1, ] <- c(1, 1, 0, 0); put("one.subject", x)
# the same table in every study; two studies the same
x <- matrix(rep(c(12, 40, 7, 38), each = 5), 5); put("identical", x)
x <- rbind(c(10, 50, 5, 50), c(10, 50, 5, 50), c(3, 40, 9, 41), c(22, 80, 20, 78)); put("tied", x)
# large studies
put("large", draw(9, 2000, 0.11, 0.1, 0.1))
close(out)
cat("cases written to", args[1], "\n")
