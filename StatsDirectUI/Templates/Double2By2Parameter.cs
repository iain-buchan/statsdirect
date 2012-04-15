using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class Double2By2Parameter: Parameter
    {
        private string columnsPrompt;
        private string leftColumnPrompt;
        private string rightColumnPrompt;
        private string rowsPrompt;
        private string topRowPrompt;
        private string bottomRowPrompt;
        private string topLeftName;
        private string topRightName;
        private string bottomLeftName;
        private string bottomRightName;
        
        [XmlElement(ElementName = "columns-prompt")]
        public string ColumnsPrompt
        {
            get { return columnsPrompt; }
            set { columnsPrompt = value; }
        }

        [XmlElement(ElementName = "left-column-prompt")]
        public string LeftColumnPrompt
        {
            get { return leftColumnPrompt; }
            set { leftColumnPrompt = value; }
        }

        [XmlElement(ElementName = "right-column-prompt")]
        public string RightColumnPrompt
        {
            get { return rightColumnPrompt; }
            set { rightColumnPrompt = value; }
        }

        [XmlElement(ElementName = "rows-prompt")]
        public string RowsPrompt
        {
            get { return rowsPrompt; }
            set { rowsPrompt = value; }
        }

        [XmlElement(ElementName = "top-row-prompt")]
        public string TopRowPrompt
        {
            get { return topRowPrompt; }
            set { topRowPrompt = value; }
        }

        [XmlElement(ElementName = "bottom-row-prompt")]
        public string BottomRowPrompt
        {
            get { return bottomRowPrompt; }
            set { bottomRowPrompt = value; }
        }

        [XmlElement(ElementName = "top-left-name")]
        public string TopLeftName
        {
            get { return topLeftName; }
            set { topLeftName = value; }
        }

        [XmlElement(ElementName = "top-right-name")]
        public string TopRightName
        {
            get { return topRightName; }
            set { topRightName = value; }
        }

        [XmlElement(ElementName = "bottom-left-name")]
        public string BottomLeftName
        {
            get { return bottomLeftName; }
            set { bottomLeftName = value; }
        }

        [XmlElement(ElementName = "bottom-right-name")]
        public string BottomRightName
        {
            get { return bottomRightName; }
            set { bottomRightName = value; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Double2By2; }
        }
    }
}
