using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using StatsDirect.Data;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace StatsDirect.Templates
{
    /// <summary>
    /// A container for parameters.
    /// </summary>
    [Serializable]
    [XmlRoot("parameter-bag")]
    public sealed class ParameterBag
        : IDictionary<string, FilledParameter?>
    {
        private readonly IDictionary<string, FilledParameter?> filledParameters;

        public ParameterBag()
        {
            filledParameters = new Dictionary<string, FilledParameter?>();
        }

        #region IDictionary<string,FilledParameter?> Members

        public void Add(string key, FilledParameter? value) => filledParameters.Add(key, value);

        public ParameterBag AddOutput(string key, object? value)
        {
            filledParameters.Add(key, FilledParameterFactory.Output(value));
            return this;
        }
        public ParameterBag AddOutput(IDictionary<string, object?> outputs)
        {
            foreach (KeyValuePair<string, object?> pair in outputs)
                filledParameters.Add(pair.Key, FilledParameterFactory.Output(pair.Value));
            return this;
        }

        public ParameterBag AddDefault(string key, object? value)
        {
            // If there's a proper input value already present, don't add the default.
            if (TryGetValue(key, out FilledParameter? candidate))
                if (candidate is not null && candidate.Direction == FilledParameterDirection.Input)
                    return this;
            filledParameters.Add(key, FilledParameterFactory.Input(value));
            return this;
        }

        public ParameterBag AddInput(string key, object? value)
        {
            // If there's a default value that we're overwriting with a proper input value, get rid of the default.
            if (TryGetValue(key, out FilledParameter? candidate))
                if (candidate is not null && candidate.Direction == FilledParameterDirection.Default)
                    Remove(key);
            filledParameters.Add(key, FilledParameterFactory.Input(value));
            return this;
        }

        public bool ContainsKey(string key) => filledParameters.ContainsKey(key);

        [XmlIgnore]
        public ICollection<string> Keys => filledParameters.Keys;

        public bool Remove(string key)
        {
            return filledParameters.Remove(key);
        }

        public bool TryGetValue(string key, out FilledParameter? value) =>
            filledParameters.TryGetValue(key, out value);

        public bool TryGetValue<T>(string key, out T? value) where T : class
        {
            if (!filledParameters.TryGetValue(key, out FilledParameter? fp))
            {
                // Nothing with that name in this bag
                value = default;
                return false;
            }
            if (null == fp)
            {
                // Something with that name, with no value
                value = default;
                return true;
            }
            object? o = fp.AsObject;
            if (o is null)
            {
                // Something with that name and a null value
                value = default;
                return true;
            }
            if (o is T t)
            {
                // Something with that name and the correct type
                value = t;
                return true;
            }
            // Something with that name, but the wrong type.  TODO: This should probably throw an exception instead?
            value = default;
            return false;
        }

        public T? GetValueOrThrow<T>(string key) where T : notnull
        {
            if (!filledParameters.TryGetValue(key, out FilledParameter? fp))
            {
                // Nothing with that name in this bag
                throw new Exception($"No parameter named '{key}' in the bag");
            }
            if (fp is null)
            {
                // Something with that name, with no value
                return default;
            }
            object? o = fp.AsObject;
            if (o is null)
            {
                // Something with that name and a null value
                return default;
            }
            if (o is T t)
            {
                // Something with that name and the correct type
                return t;
            }
            // Something with that name, but the wrong type.  TODO: This should probably throw an exception instead?
            throw new Exception($"Parameter named '{key}' in the bag is of type {o.GetType().FullName}, should be of type {typeof(T).FullName}");
        }

        public T GetNotNullValueOrThrow<T>(string key) where T : notnull
        {
            if (!filledParameters.TryGetValue(key, out FilledParameter? fp))
            {
                // Nothing with that name in this bag
                throw new Exception($"No parameter named '{key}' in the bag");
            }
            if (fp is null)
            {
                // Something with that name, with no value
                throw new Exception($"Parameter '{key}' has no FilledParameter");
            }
            object? o = fp.AsObject;
            if (o is null)
            {
                // Something with that name and a null value
                throw new Exception($"Parameter '{key}' has a null FilledParameter value");
            }
            if (o is T t)
            {
                // Something with that name and the correct type
                return t;
            }
            // Something with that name, but the wrong type.  TODO: This should probably throw an exception instead?
            throw new Exception($"Parameter named '{key}' in the bag is of type {o.GetType().FullName}, should be of type {typeof(T).FullName}");
        }

        /// <summary>
        /// Useful helper as we get variables in dataframes so frequently
        /// </summary>
        /// <typeparam name="T">Type of variable, for example DoubleVariable</typeparam>
        /// <param name="key">Key of parameter in bag</param>
        /// <param name="index">Index of variable in dataframe</param>
        public T GetVariableOrThrow<T>(string key, int index) where T : IVariable
        {
            DataFrame? dataFrame = GetValueOrThrow<DataFrame>(key);
            return dataFrame is null
                ? throw new Exception($"Parameter named '{key}' in the bag is null")
                : dataFrame.VariableOrThrow<T>(index);
        }

        public bool TryGetValue<T>(string key, out T? value) where T : struct
        {
            if (!filledParameters.TryGetValue(key, out FilledParameter? fp))
            {
                // Nothing with that name in this bag
                value = default;
                return false;
            }
            if (null == fp)
            {
                // Something with that name, with no value
                value = default;
                return true;
            }
            object? o = fp.AsObject;
            if (o is null)
            {
                // Something with that name and a null value
                value = default;
                return true;
            }
            if (o is T t)
            {
                // Something with that name and the correct type
                value = t;
                return true;
            }
            // Something with that name, but the wrong type.  TODO: This should probably throw an exception instead?
            value = default;
            return false;
        }

        [XmlIgnore]
        public ICollection<FilledParameter?> Values => filledParameters.Values;

        [XmlIgnore]
        public FilledParameter? this[string key]
        {
            get
            {
                if (!filledParameters.ContainsKey(key))
                    throw new ArgumentException($"Cannot find parameter '{key}'");
                return filledParameters[key];
            }
            set => filledParameters[key] = value;
        }

        #endregion

        #region ICollection<KeyValuePair<string,FilledParameter>> Members

        public void Add(KeyValuePair<string, FilledParameter?> item)
        {
            filledParameters.Add(item);
        }

        public void Clear()
        {
            filledParameters.Clear();
        }

        public bool Contains(KeyValuePair<string, FilledParameter?> item)
        {
            return filledParameters.Contains(item);
        }

        public void CopyTo(KeyValuePair<string, FilledParameter?>[] array, int arrayIndex)
        {
            filledParameters.CopyTo(array, arrayIndex);
        }

        [XmlIgnore]
        public int Count => filledParameters.Count;

        [XmlIgnore]
        public bool IsReadOnly => filledParameters.IsReadOnly;

        public bool Remove(KeyValuePair<string, FilledParameter?> item)
        {
            return filledParameters.Remove(item);
        }

        #endregion

        /// <summary>
        /// Enumeration interface removed and pairs set up for access due to XML serialization issues
        /// </summary>
        [XmlIgnore]
        public ICollection<KeyValuePair<string, FilledParameter?>> Pairs => filledParameters;

        #region IEnumerable<KeyValuePair<string,FilledParameter?>> Members
        IEnumerator<KeyValuePair<string, FilledParameter?>> IEnumerable<KeyValuePair<string, FilledParameter?>>.GetEnumerator()
        {
            return filledParameters.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members
        IEnumerator IEnumerable.GetEnumerator()
        {
            return filledParameters.GetEnumerator();
        }
        #endregion

        /// <summary>
        /// Returns a new ParameterBag containing the existing parameters.  This is a shallow copy, so while parameters may be added or removed users should be cautious about changing parameter values.
        /// </summary>
        public ParameterBag Copy()
        {
            ParameterBag copy = new();
            foreach (KeyValuePair<string, FilledParameter?> filledParameterPair in filledParameters)
                copy.Add(filledParameterPair);
            return copy;
        }

        /// <summary>
        /// Returns a new ParameterBag containing only the input parameters.
        /// </summary>
        /// <returns>a new ParameterBag containing only the input parameters</returns>
        public ParameterBag CopyWithoutOutputParameters()
        {
            ParameterBag copy = new();
            foreach (KeyValuePair<string, FilledParameter?> filledParameterPair in filledParameters)
                if (filledParameterPair.Value is not null && filledParameterPair.Value.IsInputParameter)
                    copy.Add(filledParameterPair);
            return copy;
        }

        internal bool TryGetVariable<T>(string key, int index, [NotNullWhen(true)]out T? variable) where T: IVariable
        {
            if (TryGetValue<DataFrame>(key, out DataFrame? dataFrame)
                && dataFrame is not null
                && dataFrame.TryGetVariable<T>(index, out variable))
                return true;
            variable = default;
            return false;
        }

        [XmlArray("parameters")]
        [XmlArrayItem("parameter", typeof(ParameterForXml))]
        public ParameterForXml[] ParametersForXml
        {
            get
            {
                ParameterForXml[] ps = new ParameterForXml[filledParameters.Count];
                int index = 0;
                foreach (KeyValuePair<string, FilledParameter?> pair in filledParameters)
                {
                    ParameterForXml p = new() { Name = pair.Key, Value = pair.Value};
                    ps[index++] = p;
                }
                return ps;
            }
            set
            {
                filledParameters.Clear();
                if (null != value)
                    foreach (ParameterForXml p in value)
                        filledParameters.Add(new KeyValuePair<string, FilledParameter?>(p.Name, p.Value));
            }
        }

        [XmlRoot("parameter")]
        public sealed class ParameterForXml
        {
            [XmlElement("name")]
            public string? Name { get; set; }

            [XmlIgnore]
            public FilledParameter? Value
            {
                get => FilledParameterFactory.Make(Direction, Data);
                set
                {
                    Direction = value.Direction;
                    Data = value.AsObject;
                }
            }

            [XmlElement("direction")]
            public FilledParameterDirection Direction { get; set; }

            [XmlElement("boolean", typeof(bool))]
            [XmlElement("datetime", typeof(DateTime))]
            [XmlElement("double", typeof(double))]
            [XmlElement("frame", typeof(DataFrame))]
            [XmlElement("int", typeof(int))]
            [XmlElement("string", typeof(string))]
            [XmlElement("string-list", typeof(List<string>))]
            public object? Data { get; set; }
        }
    }
}
