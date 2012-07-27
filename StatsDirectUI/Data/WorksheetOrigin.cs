using System;
using System.Xml.Serialization;

using StatsDirect.Utilities;

namespace StatsDirect.Data
{
    [Serializable]
    [XmlType("worksheet-origin")]
    public class WorksheetOrigin : IOrigin
    {
        public WorksheetOrigin(string workbookPath, string worksheetName, int column, int topRow, int rows, DataAcquisitionMode mode, bool hasTitle)
        {
            WorkbookPath = workbookPath;
            WorksheetName = worksheetName;
            Column = column;
            TopRow = topRow;
            Rows = rows;
            Mode = mode;
            HasTitle = hasTitle;
        }

        ///  <summary>
        ///  Intended to be used only by the XML deserializer
        ///  </summary>
        public WorksheetOrigin()
        {
            //  Nothing else required
        }

        public OriginType Type
        {
            get
            {
                return OriginType.Worksheet;
            }
        } // interface properties implemented by Type


        [XmlElement("column")]
        public int Column { get; set; }

        [XmlElement("mode")]
        public DataAcquisitionMode Mode { get; set; }

        [XmlElement("top-row")]
        public int TopRow { get; set; }

        ///  <summary>
        ///  The number of rows that were specified for the original column.  As a special case, any negative value indicates the whole column.
        ///  </summary>
        [XmlElement("rows")]
        public int Rows { get; set; }

        [XmlElement("workbook-path")]
        public string WorkbookPath { get; set; }

        [XmlElement("worksheet-name")]
        public string WorksheetName { get; set; }

        [XmlElement("has-title")]
        public bool HasTitle { get; set; }
    }


}
