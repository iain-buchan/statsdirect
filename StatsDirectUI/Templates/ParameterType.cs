namespace StatsDirect.Templates
{
    /// <summary>
    /// The kind of parameter stored here, to avoid typeof() checks.
    /// </summary>
    public enum ParameterType
    {
        Boolean,
        ConfidenceInterval,
        /// <summary>
        /// Extension point for projects that wish to use Parameters for custom purposes.
        /// Those projects will have to find some other way of disambiguating the custom parameter!
        /// </summary>
        Custom,
        Date,
        Double,
        Double2By2,
        Double2By2ByK,
        EditGrid,
        Grid,
        Grid2D,
        GroupedCovariance,
        Integer,
        MultipleOptions,
        Option,
        Options,
        PickFromList,
        PickVariables,
        Special,
        String
    }
}