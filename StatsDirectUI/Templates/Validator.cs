using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public ValidationMode ValidationMode { get; set; }
    }
}
