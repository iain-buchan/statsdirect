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

        public OriginType Type => OriginType.Worksheet;

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

        /// <summary>
        /// True iff the original data was considered to have a title row.
        /// </summary>
        [XmlElement("has-title")]
        public bool HasTitle { get; set; }

        /// <summary>
        /// True iff the worksheet had a filter in place such that only some rows were shown when this data was selected.
        /// </summary>
        [XmlElement("was-filtered")]
        public bool WasFiltered { get; set; }

        /// <summary>The physical zero-based worksheet row of each acquired data value when hidden rows made the selection noncontiguous.
        /// An acquisition-time mapping only; the saved run records its actual row boundary rather than serializing this array.</summary>
        [XmlIgnore]
        public int[] DataRowIndices { get; set; }

        /// <summary>The selection was explicitly padded after acquisition. These
        /// virtual missing values cannot be reproduced by rereading its original range.</summary>
        [XmlIgnore]
        public bool WasPadded { get; set; }

        public int OriginGroup { get; set; }
    }
}
