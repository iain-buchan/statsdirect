namespace StatsDirect.Expressions
{
    public class ArgumentDefinition
    {
        public string Name { get; private set; }
        public bool IsOptional { get; private set; }
        public string Default { get; private set; }
        public DataType DataType { get; private set; }

        public ArgumentDefinition(string name, DataType dataType, bool isOptional = false, string parameterDefault = null)
        {
            Name = name;
            DataType = dataType;
            IsOptional = isOptional;
            Default = parameterDefault;
        }

        public override string ToString()
        {
            // Mandatory parameters only show their name.
            if (!IsOptional)
                return Name;

            // Optional parameters show different strings depending on whether or not they have a default.
            if (null != Default)
                return string.Format("{0}:={1} (default)", Name, Default);
            return Name + " (optional)";
        }
    }
}
