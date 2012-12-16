namespace StatsDirect.Templates
{
    public enum ValidationMode
    {
        NotSet = 0,
        Pooling = 1,
        Square = 2,
        SquareBins = 3,
        CheckForNonDummiedCategories = 4,
        Boolean = 5,
        Positive = 6,
        ZeroToOneExclusive = 7,
        TwoBinsAndNoMissingData = 8
    }
}