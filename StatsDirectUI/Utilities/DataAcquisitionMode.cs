namespace StatsDirect.Utilities
{
    public enum DataAcquisitionMode
    {
        NotSet = 0,
        /// <summary>
        /// Doubles; missing values are removed from the output
        /// </summary>
        NumericSkipMissing = 1,
        /// <summary>
        /// Doubles; interior missing rows use Constant.MISSING, with legacy trimming at the ends.
        /// </summary>
        NumericReplaceMissing = 2,
        /// <summary>
        /// Acquire identifiers for groups or subgroups
        /// </summary>
        GroupIdentifiers = 3,
        CategoryReplaceMissing = 4,
        CategoryCombineAllColumns = 5,
        Text = 101,
        DateReplaceMissing = 102,
        TextWithFormulae = 103,
        TextNoTitles = 104,
        NumericCodingTextToCategories = 105,
        NumericCodingTextToDummies = 106,
        /// <summary>
        /// Whatever we can get, of whatever types.  Used for R export.
        /// </summary>
        Variant = 107,
        /// <summary>
        /// Numeric values in their selected visible rows, including leading and trailing missing values.
        /// Used for matched observations and outcomes aligned with group identifiers.
        /// </summary>
        NumericPreserveRows = 108,
        /// <summary>
        /// Mixed numeric/categorical predictors in selected visible rows, retaining missing values before dummy coding.
        /// </summary>
        NumericCodingTextToDummiesPreserveRows = 109
    }
}
