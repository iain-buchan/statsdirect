# The columns of the benchmarks of the Frequencies analysis: a line "case", the name and the kind (numbers or text), and then a
# line with the cells of the column, NA for a cell that is empty.
# usage: Rscript --vanilla cases-frequencies.R cases-frequencies.txt
args <- commandArgs(trailingOnly = TRUE)
set.seed(20260928)
out <- character(0)
add <- function(name, kind, cells) out <<- c(out, paste("case", name, kind, sep = "\t"), paste(cells, collapse = "\t"))
with.missing <- function(cells, k) { cells[sample(length(cells) - 1, k)] <- NA; cells }   # the last cell is never empty

add("help", "numbers", c(3, 3, 4, 1, 1, 2, 5, 3))
add("decimals", "numbers", c("-1", "0.5", "-10", "100", "3", "-1", "2.25", "0.5", "7", "7", "7"))
add("numbers.missing", "numbers", c(3, NA, 4, NA, 1, 3, NA, 2, 10, 20, 10))
add("text", "text", c("b", "a", "C", "c", "a", "B", "b", "a", "Zebra", "apple", "Apple"))
add("text.missing", "text", c("yes", "no", NA, "yes", "maybe", NA, "no", "yes"))
add("one.value", "numbers", rep(4, 6))
add("all.different", "numbers", c(12, 7, 100, 9, 1, 25))
add("likert", "numbers", sample(1:7, 300, replace = TRUE, prob = c(1, 2, 4, 6, 4, 2, 1)))
add("likert.missing", "numbers", with.missing(sample(1:5, 250, replace = TRUE), 23))
add("many.numbers", "numbers", sample(c(-20:40, 0.25, 1.5, 1000, 12345), 400, replace = TRUE))
add("many.labels", "text", sample(c(letters, LETTERS[1:10], "ward 1", "ward 2", "ward 10", "Ward 3"), 500, replace = TRUE))
add("thousands", "numbers", with.missing(sample(1:300, 6000, replace = TRUE), 150))
writeLines(out, args[1])
