namespace StatsDirect.Utilities
{
    public enum DataAcquisitionMode
    {
        NotSet = 0,
        /// <summary>
        /// Acquire doubles; missing rows are removed from the output
        /// </summary>
        NumericSkipMissing = 1,
        /// <summary>
        /// Acquire doubles; missing rows are preserved in the output, replaced with Constant.MISSING.
        /// </summary>
        NumericReplaceMissing = 2,
        /// <summary>
        /// Ported from SD2, unknown purpose
        /// </summary>
        MODE3 = 3,
        CategoryReplaceMissing = 4,
        CategoryCombineAllColumns = 5,
        /// <summary>
        /// Ported from SD2, unknown purpose
        /// </summary>
        MODE6 = 6,
        Text = 101,
        DateReplaceMissing = 102,
        TextWithFormulae = 103,
        TextNoTitles = 104,
        NumericCodingTextToCategories = 105
    }
}