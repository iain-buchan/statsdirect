using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace StatsDirect.UI.Learning;

internal static class ReportAIContext
{
    // Only visible text and labels. Native run records, workbook paths and raw
    // analysis inputs must never enter the provider's report snapshot.
    internal static async Task<JsonObject> SnapshotAsync(frmReportWebView report, string resultId = null)
    {
        var view = report.LearningReport;
        await view.Ready;
        long revision = view.Revision;
        string script = """
            (()=>{
              const root=document.getElementById('results');
              if(!root)throw Error('The report is still loading. Please try again.');
              const entries=[...root.querySelectorAll('.report-entry')];
              const at=node=>{const el=node?.nodeType===1?node:node?.parentElement;const entry=el?.closest('.report-entry');return entry&&root.contains(entry)?entry:null;};
              const selected=entries.find(e=>e.dataset.resultId===RESULT_ID)||at(window.getSelection()?.anchorNode)||entries.at(-1);
              const text=root.innerText,focus=selected?.innerText||'';
              const charts=[...root.querySelectorAll('svg,img')].filter(e=>e.getClientRects().length&&getComputedStyle(e).visibility!=='hidden');
              const chartText=charts.map(e=>e.tagName.toLowerCase()==='svg'?[...e.querySelectorAll('text')].filter(t=>t.getClientRects().length&&getComputedStyle(t).visibility!=='hidden').map(t=>t.textContent).join(' '):(e.getAttribute('alt')||'')).join('\n');
              return {text:text.slice(0,18000),totalCharacters:text.length,textLimit:18000,truncated:text.length>18000,
                focusedResult:{id:selected?.dataset.resultId||'',text:focus.slice(0,6000),truncated:focus.length>6000},
                resultIDs:entries.slice(0,40).map(e=>e.dataset.resultId),resultCount:entries.length,resultsTruncated:entries.length>40,
                chartCount:charts.length,chartText:chartText.slice(0,2000),chartTextTruncated:chartText.length>2000,
                chartImagesIncluded:false,snapshot:true};
            })()
            """;
        var snapshot = JsonNode.Parse(await view.Browser.CoreWebView2.ExecuteScriptAsync(script.Replace("RESULT_ID", JsonSerializer.Serialize(resultId ?? "")))) as JsonObject
            ?? throw new InvalidOperationException("The report could not be read. Please try again.");
        if (view.Revision != revision) throw new InvalidOperationException("The report changed while it was read. Please send again.");
        snapshot["documentId"] = report.Id(); snapshot["title"] = report.Text.Limit(200); snapshot["kind"] = "report";
        var results = new JsonArray();
        foreach (var id in snapshot.A("resultIDs"))
        {
            var entry = JsonNode.Parse((await view.RecordAsync(id.GetValue<string>())).GetRawText());
            results.Add(new JsonObject { ["id"] = id.DeepClone(), ["title"] = entry.S("title").Limit(200), ["operation"] = entry.S("operation") });
            if (snapshot["focusedResult"].S("id") == id.GetValue<string>())
            { snapshot["focusedResult"]["title"] = entry.S("title").Limit(200); snapshot["focusedResult"]["operation"] = entry.S("operation"); }
        }
        if (view.Revision != revision) throw new InvalidOperationException("The report changed while it was read. Please send again.");
        snapshot.Remove("resultIDs"); snapshot["results"] = results;
        snapshot["scope"] = "Displayed report text and chart labels only. No chart pixels, hidden run records, original worksheet values or source file paths are included unless displayed in the report. The report may contain user edits.";
        return snapshot;
    }
}
