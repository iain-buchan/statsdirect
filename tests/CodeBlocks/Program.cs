using System.Text.Json;
using StatsDirect.UI.WebReports;

// The program's reader of fenced code in a reply, against the examples shared with the conversation page and the Mac host
// (Learning/Shared/Learning/code-block-examples.json; the page and the reference reader are checked against the same file by the
// shared JavaScript tests).  Deterministic: no window, no application assembly.
internal static class Program
{
    private static int checks;
    private static void Check(bool ok, string description) { if (!ok) throw new Exception("FAIL " + description); checks++; }
    private static string Show(IEnumerable<CompletedCodeBlocks.Block> blocks) => JsonSerializer.Serialize(blocks.Select(b => new { r = b.R, shown = b.Shown, text = b.Text }));

    private static int Main()
    {
        try
        {
            using var examples = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "code-block-examples.json")));
            var replies = new Dictionary<string, string>();
            foreach (var example in examples.RootElement.GetProperty("cases").EnumerateArray())
            {
                string name = example.GetProperty("name").GetString(), markdown = example.GetProperty("markdown").GetString();
                replies.Add(name, markdown);
                var expected = example.GetProperty("blocks").EnumerateArray().Select(b =>
                {
                    string shown = b.GetProperty("shown").GetString();
                    return new CompletedCodeBlocks.Block(b.GetProperty("r").GetBoolean(), shown, b.TryGetProperty("text", out var text) ? text.GetString() : shown);
                }).ToList();
                var read = CompletedCodeBlocks.ReadBlocks(markdown);
                Check(read.SequenceEqual(expected), $"{name}: expected {Show(expected)}, read {Show(read)}");
            }
            Check(replies.Count >= 36, "the shared examples hold at least the 36 replies of learning-v1.1.6");
            foreach (var click in examples.RootElement.GetProperty("matches").EnumerateArray())
            {
                string name = click.GetProperty("name").GetString();
                int found = CompletedCodeBlocks.Find(CompletedCodeBlocks.ReadBlocks(replies[click.GetProperty("case").GetString()]),
                    click.GetProperty("position").GetInt32(), click.GetProperty("shown").GetString(), click.GetProperty("rOnly").GetBoolean());
                Check(found == click.GetProperty("expect").GetInt32(), $"{name}: expected {click.GetProperty("expect").GetInt32()}, found {found}");
            }
            // The entry point the R script builder uses gives the code to open or copy, with the reply's own characters.
            Check(CompletedCodeBlocks.Read("```python\nno_r()\n```\n~~~RSCRIPT\nx <- 1\n~~~\n```r\nincomplete <-").SequenceEqual(new[] { "x <- 1" }), "R blocks only, and never an unfinished block");
            Check(CompletedCodeBlocks.Read("```python\na\n```\n```r\n\tb\n```", rOnly: false).SequenceEqual(new[] { "a", "\tb" }), "every completed block, with a leading tab kept");
            Check(CompletedCodeBlocks.ReadBlocks(null).Count == 0 && CompletedCodeBlocks.ReadBlocks("").Count == 0, "no reply, no blocks");
            Console.WriteLine($"PASS {checks} code block checks against the shared examples");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return 1;
        }
    }
}
