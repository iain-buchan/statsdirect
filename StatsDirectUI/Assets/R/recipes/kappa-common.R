# Use the report's saved grade order when available. Older records contain only
# the labels. Sort their numbers by value and their text by UTF-16 code units,
# as the report does, without depending on the locale in which R runs.
kappa_numeric_labels <- function(labels) {
  decimal <- trimws(labels)
  numeric <- grepl("^[+-]?(?:[0-9][0-9,]*(?:\\.[0-9]*)?|\\.[0-9]+)(?:[eE][+-]?[0-9]+)?$", decimal, perl = TRUE)
  value <- rep(NA_real_, length(labels))
  value[numeric] <- as.numeric(gsub(",", "", decimal[numeric], fixed = TRUE))
  value
}

kappa_grade_order <- function(labels, saved) {
  labels <- unique(as.character(labels[!is.na(labels)]))
  saved <- unlist(saved, use.names = FALSE)
  if (length(saved) == length(labels) && setequal(saved, labels)) return(saved)
  number <- kappa_numeric_labels(labels)
  ordinal <- vapply(iconv(enc2utf8(labels), from = "UTF-8", to = "UTF-16BE", toRaw = TRUE),
                    function(bytes) paste(sprintf("%02x", as.integer(bytes)), collapse = ""), "")
  labels[order(labels != "", !is.finite(number), ifelse(is.finite(number), number, 0), ordinal, method = "radix")]
}
