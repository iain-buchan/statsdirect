using System;
using System.Xml.Serialization; 

using StatsDirect.Utilities; 

namespace StatsDirect.Data
{
    [ Serializable ]
    [ XmlType( "worksheet-origin" ) ]
    public class WorksheetOrigin : IOrigin
        
     
    { 
        private int _column; 
        private int _topRow; 
        private string _workbookPath; 
        private string _worksheetName; 
        private int _rows; 
        private DataAcquisitionMode _mode; 
        
        public WorksheetOrigin( string WorkbookPath, string WorksheetName, int Column, int TopRow, int Rows, DataAcquisitionMode Mode ) 
        { 
            _workbookPath = WorkbookPath; 
            _worksheetName = WorksheetName; 
            _column = Column; 
            _topRow = TopRow; 
            _rows = Rows; 
            _mode = Mode; 
        } 
        
        ///  <summary>
        ///  Intended to be used only by the XML deserializer
        ///  </summary>
        public WorksheetOrigin() 
        { 
            //  Nothing else required
        } 
        
        private OriginType Type 
        { 
            get 
            { 
                return OriginType.Worksheet; 
            } 
        } // interface properties implemented by Type
        OriginType IOrigin.Type
        { 
            get
            {
                return Type;
            }
        }
        
        
        // TRANSMISSINGCOMMENT: Property Column
        [ XmlElement( "column" ) ]
        public int Column 
        { 
            get 
            { 
                return _column; 
            } 
            set 
            { 
                _column = value; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property Mode
        [ XmlElement( "mode" ) ]
        public DataAcquisitionMode Mode 
        { 
            get 
            { 
                return _mode; 
            } 
            set 
            { 
                _mode = value; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property TopRow
        [ XmlElement( "top-row" ) ]
        public int TopRow 
        { 
            get 
            { 
                return _topRow; 
            } 
            set 
            { 
                _topRow = value; 
            } 
        } 
        
        ///  <summary>
        ///  The number of rows that were specified for the original column.  As a special case, any negative value indicates the whole column.
        ///  </summary>
        [ XmlElement( "rows" ) ]
        public int Rows 
        { 
            get 
            { 
                return _rows; 
            } 
            set 
            { 
                _rows = value; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property WorkbookPath
        [ XmlElement( "workbook-path" ) ]
        public string WorkbookPath 
        { 
            get 
            { 
                return _workbookPath; 
            } 
            set 
            { 
                _workbookPath = value; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property WorksheetName
        [ XmlElement( "worksheet-name" ) ]
        public string WorksheetName 
        { 
            get 
            { 
                return _worksheetName; 
            } 
            set 
            { 
                _worksheetName = value; 
            } 
        } 
        
    } 
    
    
} 
