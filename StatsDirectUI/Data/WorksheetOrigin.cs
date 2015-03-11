using System;
using System.Xml.Serialization;

using StatsDirect.Utilities;

namespace StatsDirect.Data
{
    [Serializable]
    [XmlType("worksheet-origin")]
    public class WorksheetOrigin : IOrigin
    {
        public WorksheetOrigin(string workbookPath, string worksheetName, int column, int topRow, int rows, DataAcquisitionMode mode, bool hasTitle, bool wasFiltered, int originGroup)
        {
            WorkbookPath = workbookPath;
            WorksheetName = worksheetName;
            Column = column;
            TopRow = topRow;
            Rows = rows;
            Mode = mode;
            HasTitle = hasTitle;
            WasFiltered = wasFiltered;
            OriginGroup = originGroup;
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
        }

        [XmlElement("column")]
        public int Column { get; private set; }

        [XmlElement("mode")]
        public DataAcquisitionMode Mode { get; private set; }

        [XmlElement("top-row")]
        public int TopRow { get; private set; }

        ///  <summary>
        ///  The number of rows that were specified for the original column.  As a special case, any negative value indicates the whole column.
        ///  </summary>
        [XmlElement("rows")]
        public int Rows { get; private set; }

        [XmlElement("workbook-path")]
        public string WorkbookPath { get; private set; }

        [XmlElement("worksheet-name")]
        public string WorksheetName { get; private set; }

        /// <summary>
        /// True iff the original data was considered to have a title row.
        /// </summary>
        [XmlElement("has-title")]
        public bool HasTitle { get; private set; }

        /// <summary>
        /// True iff the worksheet had a filter in place such that only some rows were shown when this data was selected.
        /// </summary>
        [XmlElement("was-filtered")]
        public bool WasFiltered { get; private set; }

        public int OriginGroup { get; set; }
    }
}
