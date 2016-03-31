using System.Windows.Forms;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.Commands;
using DevExpress.XtraRichEdit.Export;
using DevExpress.XtraRichEdit.Export.Html;
using DevExpress.Office.Utils;

namespace StatsDirect.UI
{
    /// <remarks>From https://www.devexpress.com/Support/Center/Example/Details/E3665</remarks>
    public class CustomCopySelectionCommand : CopySelectionCommand
    {
        public CustomCopySelectionCommand(IRichEditControl control)
            : base(control)
        {
        }

        protected override void ExecuteCore()
        {
            RichEditControl richEditControl = (RichEditControl)Control;
            richEditControl.BeforeExport += OnBeforeExport;
            string htmlForClipboard = string.Empty;

            richEditControl.Options.Export.Html.ExportRootTag = ExportRootTag.Html;

            try
            {
                string html = Control.Document.GetHtmlText(Control.Document.Selection, new CustomUriProvider(), richEditControl.Options.Export.Html);
                htmlForClipboard = CF_HTMLHelper.GetHtmlClipboardFormat(html);
            }
            finally
            {
                richEditControl.BeforeExport -= OnBeforeExport;
            }

            DataObject data = new DataObject();
            data.SetData(OfficeDataFormats.Rtf, richEditControl.Document.GetRtfText(richEditControl.Document.Selection));
            data.SetData(OfficeDataFormats.UnicodeText, richEditControl.Document.GetText(richEditControl.Document.Selection));
            data.SetData(OfficeDataFormats.Html, htmlForClipboard);
            Clipboard.Clear();
            Clipboard.SetDataObject(data, false);
        }

        void OnBeforeExport(object sender, BeforeExportEventArgs e)
        {
            HtmlDocumentExporterOptions exporterOptions = e.Options as HtmlDocumentExporterOptions;

            if (exporterOptions != null)
            {
                exporterOptions.CssPropertiesExportType = CssPropertiesExportType.Inline;
                exporterOptions.ExportRootTag = ExportRootTag.Body;
                exporterOptions.EmbedImages = false; // To delegate handling into a CustomUriProvider
            }
        }
    }
}

