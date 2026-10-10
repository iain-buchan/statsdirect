internal static partial class Program
{
    // The record of a run travels with its entry: appended with it, replaced by more of the run, reported to the context menu, kept in the
    // saved report's metadata and given back after a reload, for the R script of the result
    private static async Task CheckRecordRoundTrip()
    {
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        string record = "{\"version\":1,\"operation\":\"TPaired\",\"title\":\"Paired t test\",\"inputs\":[{\"name\":\"data\",\"kind\":\"frame\",\"file\":\"test.xlsx\",\"sheet\":\"Agreement\",\"columns\":[{\"title\":\"1st\",\"column\":1,\"rows\":[1,21],\"hash\":\"0123456789abcdef\"}]}],\"parameters\":{\"gamma\":0.95},\"values\":{\"t\":-0.81}}";
        string id = await view.AppendAsync("<p>A result with a record</p>", "Paired t test", "TPaired", 1150, record);
        string plain = await view.AppendAsync("<p>A result without</p>", "Text", "", 0);
        var entry = await view.RecordAsync(id);
        Check(entry.GetProperty("title").GetString() == "Paired t test" && entry.GetProperty("operation").GetString() == "TPaired" && entry.GetProperty("record").GetProperty("values").GetProperty("t").GetDouble() == -0.81,
              "a result appended with the record of its run gives it back with its title and operation");
        Check((await view.RecordAsync(plain)).GetProperty("record").ValueKind == System.Text.Json.JsonValueKind.Null, "a result appended without a record gives none");
        await view.ExtendAsync(id, "<p>More of the result</p>", record.Replace("-0.81", "-0.82"));
        Check((await view.RecordAsync(id)).GetProperty("record").GetProperty("values").GetProperty("t").GetDouble() == -0.82, "more of a result replaces its record with the run's latest");
        await Js($"document.querySelector('#result-{id} .report-body').dispatchEvent(new MouseEvent('contextmenu',{{bubbles:true,cancelable:true}}))");
        var target = await view.CallAsync("contextTarget");
        Check(target.GetProperty("hasRecord").GetBoolean() && target.GetProperty("operation").GetString() == "TPaired", "a right-click on the result reports that it has a record, and its operation");
        await Js($"document.querySelector('#result-{plain} .report-body').dispatchEvent(new MouseEvent('contextmenu',{{bubbles:true,cancelable:true}}))");
        Check(!(await view.CallAsync("contextTarget")).GetProperty("hasRecord").GetBoolean(), "a right-click on a result without a record says so");
        var snapshot = await view.CallAsync("snapshot");
        Check(snapshot.GetProperty("entries")[0].GetProperty("record").GetProperty("operation").GetString() == "TPaired" && !snapshot.GetProperty("entries")[1].TryGetProperty("record", out _),
              "the saved report's metadata carries the record of each result that has one");
        await view.CallAsync("load", new { entries = snapshot.GetProperty("entries") });
        string reloaded = (await Js("document.querySelector('.report-entry').id.slice(7)")).GetString();
        Check((await view.RecordAsync(reloaded)).GetProperty("record").GetProperty("inputs")[0].GetProperty("columns")[0].GetProperty("hash").GetString() == "0123456789abcdef", "a report loaded again gives each result its record back");
    }
}
