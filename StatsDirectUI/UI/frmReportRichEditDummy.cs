using System;
using System.Windows.Forms;
using StatsDirect.Templates;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;
using DevExpress.XtraRichEdit.Commands;

namespace StatsDirect.UI
{
    public partial class frmReportRichEditDummy: Form
    {
        public frmReportRichEditDummy()
        {
            InitializeComponent();
        }

        #region IReport Members

        public string RtfText
        {
            get => richEditControl1.Document.RtfText;
            set
            {
                Document document = richEditControl1.Document;
                document.BeginUpdate();
                richEditControl1.Document.RtfText = value;
                document.EndUpdate();
            }
        }

        public void AppendRtfText(string Rtf, int helpContextId, Operation operation, string redoInformation)
        {
            Document document = richEditControl1.Document;
            // DocumentPosition initialEnd = document.Range.End;
            document.BeginUpdate();

            // Append the text, surrounding it with the specified help context if required
            document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!help!-> " + helpContextId.ToString() + @" <-!help!! }}");
            // Add redo information if present
            if (!string.IsNullOrEmpty(redoInformation))
            {
                string safeXml = redoInformation.Replace(@"\", "&#92;");
                document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!redo!-> " + "\"" + operation.Name + "\" " + safeXml + @" <-!redo!! }}");
            }
            string[] splitInserts = Rtf.Split(new[] { "/split/" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string piece in splitInserts)
                if (piece.StartsWith(@"{\rtf"))
                    document.InsertRtfText(document.Range.End, piece);

            // TODO: Reset help at the end of the document.  For now (DXperience 10.2), this causes hidden text at the end, making insertion of text hard.
            // document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!help!-> " + AmbientHelpContextId.ToString() + @" <-!help!! }}");

            // Ensure the appended text is visible by scrolling the selection into view - the selection is the caret at the end of the old text
            // document.CaretPosition = initialEnd;
            document.CaretPosition = document.Range.End;

            document.EndUpdate();

            richEditControl1.ScrollToCaret();
        }

        #endregion
    }
}
