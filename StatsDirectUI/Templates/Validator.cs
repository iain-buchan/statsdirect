using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public class Validator
    {
        /// <summary>
        /// A way in which the data should be validated
        /// </summary>
        [XmlText]
        public string ValidatorName { get; set; }
    }
}
