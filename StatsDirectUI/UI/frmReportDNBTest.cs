using DotNetBrowser;
using DotNetBrowser.DOM;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using System.Collections.Generic;

namespace StatsDirect.UI
{
    public partial class frmReportDNBTest : StatsDirectForm, IReport
    {
        const string HTML_REPORT_START = @"<html><head><style>body,h1,h2,th,td { font-family: Calibri,Arial; font-size: 10pt; } .ci { color: blue; } .grandtotal { color: #000080; } .model { color: #000080; } .pval { color: #008000; } .score { color: #008080; } .subtotal { color: #800000; } .warn {color: red; }</style></head><body>";
        const string HTML_REPORT_END = @"</body></html>";

        private Browser browser;

        public frmReportDNBTest()
        {
            // Lightweight (non-GPU accelerated) browsers are recommended for MDI applications as otherwise there can be some odd z-order behaviours.
            // These, in turn, are quite heavily optimised with the following options.
            BrowserPreferences.SetChromiumSwitches(
              "--disable-gpu",
              "--disable-gpu-compositing",
              "--enable-begin-frame-scheduling",
              "--software-rendering-fps=60"
            );

            InitializeComponent();
            SdApplication.SoleInstance.EnsureBuiltInMenuItemsCanShowHelp(menuStrip1);
            browser = browserView.Browser;
            string html = HTML_REPORT_START
                + HTML_REPORT_END;
            browser.LoadHTML(html);
            browser.ExecuteJavaScript("document.body.contentEditable='true'");
        }

        void IReport.AppendRenderable(IRenderable renderable, int helpContextId, Operation operation, string redoInformation)
        {
            string safeRedoInformation = string.IsNullOrWhiteSpace(redoInformation) ? string.Empty : redoInformation.Replace(@"\", "&#92;");
            string html = new HtmlRenderer(SdApplication.SoleInstance).Render(renderable);
            DOMDocument document = browser.GetDocument();
            DOMElement body = document.GetElementByTagName("body");
            DOMElement wrappedRenderable = document.CreateElement("span");
            wrappedRenderable.Attributes.Add("class", "statsDirectResults");
            wrappedRenderable.Attributes.Add("operation", operation.Name);
            wrappedRenderable.Attributes.Add("helpContextId", helpContextId.ToString());
            wrappedRenderable.Attributes.Add("redoInformation", safeRedoInformation);
            wrappedRenderable.SetInnerHTML(html);
            body.AppendChild(wrappedRenderable);
            // browser.ExecuteJavaScript("document.body.contentEditable='true'");
        }

        private void Browser_DocumentLoadedInMainFrameEvent(object sender, DotNetBrowser.Events.LoadEventArgs e)
        {
            browser.ExecuteJavaScript("document.body.contentEditable='true'");
        }

        // TODO: Add FormClosing handler that disposes BrowserView, then Browser

        public override IList<Pane> AvailablePanes => new List<Pane> { ((IForm)this).SelectedPane };

        public override Pane SelectedPane => new Pane(Text, WindowInformation, 0);

        public override bool SelectPane(Pane pane)
        {
            // There's only ever one, do nothing
            return true;
        }
    }
}
