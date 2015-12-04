namespace StatsDirect.Expressions
{
    public class ArgumentDefinition
    {
        public string ParameterName { get; set; }
        public bool IsOptional { get; set; }
        public string ParameterDefault { get; set; }

        public ArgumentDefinition(string parameterName, bool isOptional, string parameterDefault)
        {
            ParameterName = parameterName;
            IsOptional = isOptional;
            ParameterDefault = parameterDefault;
        }

        public ArgumentDefinition(string parameterName)
        {
            ParameterName = parameterName;
            IsOptional = false;
            ParameterDefault = null;
        }

        public ArgumentDefinition(string parameterName, string parameterDefault)
        {
            ParameterName = parameterName;
            IsOptional = true;
            ParameterDefault = parameterDefault;
        }

        public override string ToString()
        {
            // Mandatory parameters only show their name.
            if (!IsOptional)
                return ParameterName;

            // Optional parameters show different strings depending on whether or not they have a default.
            if (null != ParameterDefault)
                return string.Format("{0}:={1} (default)", ParameterName, ParameterDefault);
            return ParameterName + " (optional)";
        }
    }
}
