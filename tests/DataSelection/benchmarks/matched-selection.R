# Independent gold values for the ordinary matched-column selection boundary.
# Missing observations are omitted as PAIRS, after retaining their worksheet rows.
# Run with R 4.6.2: Rscript matched-selection.R matched-selection.tsv
out <- if (length(commandArgs(TRUE))) commandArgs(TRUE)[1] else 'matched-selection.tsv'
i <- 1:100
x <- as.double(i)
y <- 2 * i + (i %% 3)
cases <- list(complete = list(x, y))
a <- x; a[1] <- NA; cases$leading <- list(a, y)
a <- x; a[100] <- NA; cases$trailing <- list(a, y)
a <- x; a[50] <- NA; cases$interior <- list(a, y)
a <- x; a[c(1, 100)] <- NA; b <- y; b[50] <- NA
cases$both_ends <- list(a, b)
keep <- -c(1, 10, 50, 100)
cases$hidden <- list(x[keep], y[keep])
a <- x; a[c(1, 50)] <- NA
cases$nonmonotone <- list(a, 2 * i + 25 * sin(i))
rows <- list()
for (name in names(cases)) {
  a <- cases[[name]][[1]]; b <- cases[[name]][[2]]
  pair <- complete.cases(a, b)
  d <- (a - b)[pair]
  tt <- t.test(a, b, paired = TRUE)
  signed <- wilcox.test(a, b, paired = TRUE, exact = TRUE, correct = FALSE)
  pearson <- cor.test(a[pair], b[pair], method = 'pearson')
  spearman <- cor.test(a[pair], b[pair], method = 'spearman', exact = FALSE)
  values <- c(n = length(d), mean = mean(d), sd = sd(d), sem = sd(d)/sqrt(length(d)),
    df = unname(tt$parameter), t = unname(tt$statistic), tail_2 = tt$p.value,
    from = tt$conf.int[1], to = tt$conf.int[2],
    signed_nonzero = sum(d != 0), signed_p = signed$p.value,
    pearson = unname(pearson$estimate), spearman = unname(spearman$estimate))
  rows[[name]] <- data.frame(case = name, key = names(values), value = sprintf('%.17g', values))
}
write.table(do.call(rbind, rows), out, sep = '\t', row.names = FALSE, quote = FALSE)
