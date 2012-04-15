using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsDirect.Expressions
{
    public class FunctionDefinition
    {
        public string Name { get; set; }
        public string ClrName { get; set; }
        private readonly List<ArgumentDefinition> argumentDefinitions;

        public FunctionDefinition(string name, string clrName, IList<string> parameterNamesAndDefaults)
        {
            Name = name;
            ClrName = clrName;

            if (null == parameterNamesAndDefaults)
                return;

            if (parameterNamesAndDefaults.Count % 2 != 0)
                throw new ArgumentException("Must contain pairs of name, default", "parameterNamesAndDefaults");

            argumentDefinitions = new List<ArgumentDefinition>(parameterNamesAndDefaults.Count / 2);

            for (int i = 0; i < parameterNamesAndDefaults.Count; i += 2)
            {
                string parameterName = parameterNamesAndDefaults[i];
                string parameterDefault = parameterNamesAndDefaults[i + 1];
                argumentDefinitions.Add(new ArgumentDefinition(parameterName, null != parameterDefault, parameterDefault));
            }
        }

        public FunctionDefinition(string name, string clrName, IEnumerable<ArgumentDefinition> argumentDefinitions)
        {
            Name = name;
            ClrName = clrName;

            if (null == argumentDefinitions)
                return;

            this.argumentDefinitions = new List<ArgumentDefinition>(argumentDefinitions);
        }

        public List<ArgumentDefinition> ArgumentDefinitions
        {
            get { return argumentDefinitions; }
        }

        public override string ToString()
        {
            return Name + "(" + string.Join(", ", argumentDefinitions.Select(x=>x.ToString()).ToArray()) + ")";
        }
    }

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
