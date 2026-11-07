# Reproduce the new cell-selection regression oracles with base R.
# R's formula method splits row-aligned values by group, then t.test.default
# removes missing observations separately within each independent sample.
out <- if (length(commandArgs(TRUE))) commandArgs(TRUE)[1] else 'row-alignment.tsv'
i <- 1:200
ids <- ifelse(i <= 90, 'A', 'B')
values <- 100 + ((i * 17) %% 41) / 5 + ifelse(i > 90, 1.25, 0)
cases <- list(complete = list(ids, values))
x <- values; x[200] <- NA; cases$trailing <- list(ids, x)
x <- values; x[1] <- NA; cases$leading <- list(ids, x)
x <- values; x[c(90, 91)] <- NA; cases$interior <- list(ids, x)
g <- ids; x <- values; g[131:200] <- NA; x[131:200] <- NA; cases$tail <- list(g, x)
keep <- -c(1, 51, 151, 200); cases$hidden <- list(ids[keep], values[keep])
x <- values; x[c(1,200)] <- NA; cases$header_missing <- list(ids, x)
rows <- list()
for (name in names(cases)) {
  dat <- data.frame(group = cases[[name]][[1]], value = cases[[name]][[2]])
  dat <- dat[!is.na(dat$group), , drop = FALSE]
  equal <- t.test(value ~ group, dat, var.equal = TRUE)
  welch <- t.test(value ~ group, dat)
  wanted <- c(n0 = sum(dat$group == 'A' & !is.na(dat$value)),
    n1 = sum(dat$group == 'B' & !is.na(dat$value)),
    mean_0 = unname(equal$estimate[1]), mean_1 = unname(equal$estimate[2]),
    t = unname(equal$statistic), p_2 = equal$p.value, df = unname(equal$parameter),
    from = equal$conf.int[1], to = equal$conf.int[2],
    t_unequal = unname(welch$statistic), p_2_unequal = welch$p.value,
    df_unequal = unname(welch$parameter), from_unequal = welch$conf.int[1], to_unequal = welch$conf.int[2])
  rows[[name]] <- data.frame(case = name, key = names(wanted), value = sprintf('%.17g', wanted))
}
x <- values; x[c(1, 6, 102)] <- NA
paired <- t.test(x[1:100], x[101:200], paired = TRUE)
d <- (x[1:100] - x[101:200]); d <- d[!is.na(d)]
wanted <- c(n = length(d), mean = mean(d), sd = sd(d), sem = sd(d)/sqrt(length(d)),
  df = unname(paired$parameter), t = unname(paired$statistic), tail_2 = paired$p.value,
  from = paired$conf.int[1], to = paired$conf.int[2])
rows$paired <- data.frame(case = 'paired', key = names(wanted), value = sprintf('%.17g', wanted))
write.table(do.call(rbind, rows), out, sep = '\t', row.names = FALSE, quote = FALSE)
