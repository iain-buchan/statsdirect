using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;
using StatsDirect.Data;

namespace StatsDirect.Templates
{
    /// <summary>
    /// A container for parameters.
    /// </summary>
    [Serializable]
    [XmlRoot("parameter-bag")]
    public sealed class ParameterBag
    {
        private IDictionary<string, FilledParameter> filledParameters;

        public ParameterBag()
        {
            filledParameters = new Dictionary<string, FilledParameter>();
        }

        public ParameterBag(string Name, FilledParameter Parameter)
            : this()
        {
            Add(Name, Parameter);
        }

        #region IDictionary<string,FilledParameter> Members

        public void Add(string key, FilledParameter value)
        {
            filledParameters.Add(key, value);
        }

        public void AddOutput(string key, object value)
        {
            filledParameters.Add(key, new FilledParameter(false, value));
        }

        public void SetOutput(string key, object value)
        {
            FilledParameter fp;
            if (TryGetValue(key, out fp))
            {
                fp.Data = value;
            }
            else
            {
                filledParameters.Add(key, new FilledParameter(false, value));
            }
        }

        public void AddInput(string key, object value)
        {
            filledParameters.Add(key, new FilledParameter(true, value));
        }

        public bool ContainsKey(string key)
        {
            return filledParameters.ContainsKey(key);
        }

        [XmlIgnore]
        public ICollection<string> Keys
        {
            get { return filledParameters.Keys; }
        }

        public bool Remove(string key)
        {
            return filledParameters.Remove(key);
        }

        public bool TryGetValue(string key, out FilledParameter value)
        {
            return filledParameters.TryGetValue(key, out value);
        }

        [XmlIgnore]
        ICollection<FilledParameter> Values
        {
            get { return filledParameters.Values; }
        }

        [XmlIgnore]
        public FilledParameter this[string key]
        {
            get
            {
                if (!filledParameters.ContainsKey(key))
                    throw new ArgumentException("Cannot find parameter '" + key + "'");
                return filledParameters[key];
            }
            set
            {
                filledParameters[key] = value;
            }
        }

        #endregion

        #region ICollection<KeyValuePair<string,FilledParameter>> Members

        public void Add(KeyValuePair<string, FilledParameter> item)
        {
            filledParameters.Add(item);
        }

        public void Clear()
        {
            filledParameters.Clear();
        }

        public bool Contains(KeyValuePair<string, FilledParameter> item)
        {
            return filledParameters.Contains(item);
        }

        public void CopyTo(KeyValuePair<string, FilledParameter>[] array, int arrayIndex)
        {
            filledParameters.CopyTo(array, arrayIndex);
        }

        [XmlIgnore]
        public int Count
        {
            get { return filledParameters.Count; }
        }

        [XmlIgnore]
        public bool IsReadOnly
        {
            get { return filledParameters.IsReadOnly; }
        }

        public bool Remove(KeyValuePair<string, FilledParameter> item)
        {
            return filledParameters.Remove(item);
        }

        #endregion

        /// <summary>
        /// Enumeration interface removed and pairs set up for access due to XML serialization issues
        /// </summary>
        [XmlIgnore]
        public ICollection<KeyValuePair<string, FilledParameter>> Pairs
        {
            get { return filledParameters; }
        }

        #region IEnumerable<KeyValuePair<string,FilledParameter>> Members
        /*
        IEnumerator<KeyValuePair<string, FilledParameter>> IEnumerable<KeyValuePair<string, FilledParameter>>.GetEnumerator()
        {
            return filledParameters.GetEnumerator();
        }
         */

        #endregion

        #region IEnumerable Members
        /*
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return filledParameters.GetEnumerator();
        }
        */
        #endregion

        /// <summary>
        /// Returns a new ParameterBag containing only the input parameters.
        /// </summary>
        /// <returns>a new ParameterBag containing only the input parameters</returns>
        public ParameterBag CopyWithoutOutputParameters()
        {
            ParameterBag copy = new ParameterBag();
            foreach (KeyValuePair<string, FilledParameter> filledParameterPair in filledParameters)
                if (filledParameterPair.Value.IsInputParameter)
                    copy.Add(filledParameterPair);
            return copy;
        }

        /// <summary>
        /// Returns a new ParameterBag containing only details that will be required when the corresponding Operation is redone.
        /// </summary>
        /// <returns>a new ParameterBag containing only details that will be required when the corresponding Operation is redone.</returns>
        public ParameterBag CopyAndStripForRedo(bool shouldKeepData)
        {
            ParameterBag copy = new ParameterBag();
            foreach (KeyValuePair<string, FilledParameter> filledParameterPair in filledParameters)
                if (filledParameterPair.Value.IsInputParameter)
                {
                    FilledParameter copiedFilledParameter = filledParameterPair.Value.CopyAndStripForRedo(shouldKeepData);
                    if (null != copiedFilledParameter)
                        copy.Add(filledParameterPair.Key, copiedFilledParameter);
                }
            return copy;
        }

        public void RefillForRedo(IRefillSource refillSource)
        {
            foreach (KeyValuePair<string, FilledParameter> filledParameterPair in filledParameters)
                filledParameterPair.Value.RefillForRedo(refillSource);
        }

        [XmlArray("parameters")]
        [XmlArrayItem("parameter", typeof(ParameterForXml))]
        public ParameterForXml[] ParametersForXml
        {
            get
            {
                ParameterForXml[] ps = new ParameterForXml[filledParameters.Count];
                int index = 0;
                foreach (KeyValuePair<string, FilledParameter> pair in filledParameters)
                {
                    ParameterForXml p = new ParameterForXml {Name = pair.Key, Value = pair.Value};
                    ps[index++] = p;
                }
                return ps;
            }
            set
            {
                filledParameters.Clear();
                if (null != value)
                {
                    foreach (ParameterForXml p in value)
                    {
                        filledParameters.Add(new KeyValuePair<string,FilledParameter>(p.Name, p.Value));
                    }
                }
            }
        }

        [XmlRoot("parameter")]
        public sealed class ParameterForXml
        {
            private bool _isInputParameter;
            private object _data;

            [XmlElement("name")]
            public string Name { get; set; }

            [XmlIgnore]
            public FilledParameter Value
            {
                get { return new FilledParameter(_isInputParameter, _data); }
                set
                {
                    _isInputParameter = value.IsInputParameter;
                    _data = value.Data;
                }
            }

            [XmlElement("is-input")]
            public bool IsInputParameter
            {
                get { return _isInputParameter; }
                set { _isInputParameter = value; }
            }

            [XmlElement("boolean", typeof(Boolean))]
            [XmlElement("datetime", typeof(DateTime))]
            [XmlElement("double", typeof(Double))]
            [XmlElement("frame", typeof(DataFrame))]
            [XmlElement("int", typeof(Int32))]
            [XmlElement("string", typeof(String))]
            [XmlElement("string-list", typeof(List<string>))]
            public object Data
            {
                get { return _data; }
                set { _data = value; }
            }
        }

        public string SerializeForRedo(bool shouldKeepData)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                ParameterBag strippedParameters = CopyAndStripForRedo(shouldKeepData);
                bf.Serialize(ms, strippedParameters);
                byte[] strippedBytes = ms.ToArray();
                return Convert.ToBase64String(strippedBytes);
            }
        }

        public static ParameterBag DeserializeAndRefillForRedo(string serializedBag, IRefillSource refillSource)
        {
            using (StringReader sr = new StringReader(serializedBag))
            {
                return DeserializeAndRefillForRedo(sr, refillSource);
            }
        }

        public static ParameterBag DeserializeAndRefillForRedo(StringReader xr, IRefillSource refillSource)
        {
            string strippedString = xr.ReadToEnd();
            if (null == strippedString)
                return null;
            byte[] strippedBytes = Convert.FromBase64String(strippedString);
            using (MemoryStream ms = new MemoryStream(strippedBytes))
            {
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                ParameterBag restoredParameters = (ParameterBag) bf.Deserialize(ms);
                restoredParameters.RefillForRedo(refillSource);
                return restoredParameters;
            }
        }
    }
}
