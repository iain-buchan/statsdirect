namespace StatsDirect.Templates
{
    public enum ValidationMode
    {
        NotSet,
        Boolean,
        CheckForNonDummiedCategories,
        NoMissingData,
        NonNegative,
        PersonTimeSize,
        Pooling,
        Positive,
        Square,
        SquareBins,
        TwoBinsAndNoMissingData,
        ZeroToOneExclusive
    }
}