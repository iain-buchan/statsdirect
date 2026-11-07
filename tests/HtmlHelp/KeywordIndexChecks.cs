using StatsDirect.UI.HtmlHelp;
using System.Text.Json;

internal static class KeywordIndexChecks
{
    internal static void Run(Action<bool, string> check, string assets, string output)
    {
        var packaged = new HelpCatalog(assets);
        if (packaged.HasKeywordIndex)
        {
            var packagedEntries = packaged.IndexEntries;
            check(packagedEntries.Any(entry => entry.Term == "Gaussian distribution" && entry.Path == "distributions/normal.htm") &&
                  packagedEntries.Any(entry => entry.Term == "Paired t test" && entry.Path == "parametric_methods/paired_t.htm"),
                "The generated Index restores authored keywords rather than listing page titles");
        }
        else check(packaged.IndexEntries.Count == 0, "The older published bundle reports its unavailable keyword index without inventing title entries");

        string source = Path.Combine(output, "keyword-index-fixture");
        Directory.CreateDirectory(Path.Combine(source, "Data"));
        Directory.CreateDirectory(Path.Combine(source, "resources"));
        File.WriteAllText(Path.Combine(source, "index.html"), "<title>Reader wrapper</title>");
        File.WriteAllText(Path.Combine(source, "contents.htm"), "<title>Home</title>");
        File.WriteAllText(Path.Combine(source, "alpha.htm"), "<title>A page title different from its keywords</title>");
        File.WriteAllText(Path.Combine(source, "beta.htm"), "<title>Another page</title>");
        File.WriteAllText(Path.Combine(source, "resources", "helper.htm"), "<title>Resource helper</title>");
        File.WriteAllText(Path.Combine(output, "index-outside.htm"), "<title>Outside</title>");
        File.WriteAllText(Path.Combine(source, "Data", "Alias.xml"), """
            <CatapultAliasFile>
              <Map Name="CONTENTS" Link="contents.htm" ResolvedId="1000" />
              <Map Name="ALPHA" Link="alpha.htm" ResolvedId="1001" />
              <Map Name="ALPHA_ALIAS" Link="alpha.htm" ResolvedId="1002" />
              <Map Name="BETA" Link="beta.htm" ResolvedId="1003" />
            </CatapultAliasFile>
            """);
        string metadata = Path.Combine(source, "keyword-index.json");
        File.Delete(metadata);
        void Write(object value) => File.WriteAllText(metadata, JsonSerializer.Serialize(value));
        var legacy = new HelpCatalog(source);
        check(!legacy.HasKeywordIndex && legacy.IndexEntries.Count == 0, "A bundle without keyword metadata has no Index entries");
        Write(new { schemaVersion = 1, entries = new[] {
            new {term="beta keyword",title="Beta topic",path="beta.htm"},
            new {term="Alpha keyword",title="Beta topic",path="beta.htm"},
            new {term="Alpha keyword",title="Alpha topic",path="alpha.htm"},
            new {term="ALPHA KEYWORD",title="Duplicate alias",path="ALPHA_ALIAS"},
            new {term="Numbers & symbols < 5",title="Title \"quoted\" & plain",path="alpha.htm#section"} } });
        var catalogue = new HelpCatalog(source);
        var entries = catalogue.IndexEntries;
        check(catalogue.HasKeywordIndex && entries.Count == 4, "Index metadata is available and duplicate term-target aliases collapse");
        check(entries.Count(entry => entry.Term == "Alpha keyword") == 2 &&
              entries.Where(entry => entry.Term == "Alpha keyword").Select(entry => entry.Path).ToHashSet().SetEquals(new[] {"alpha.htm","beta.htm"}),
            "One keyword retains every distinct target topic");
        check(entries.Select(entry => entry.Term).SequenceEqual(new[] {"Alpha keyword","Alpha keyword","beta keyword","Numbers & symbols < 5"}) &&
              entries[0].Title == "Alpha topic" && entries[1].Title == "Beta topic", "Index sorts English keywords and target titles without changing displayed case");
        check(entries[^1].Term == "Numbers & symbols < 5" && entries[^1].Title == "Title \"quoted\" & plain" && entries[^1].Path == "alpha.htm#section",
            "Index retains JSON text and authored target fragments without interpreting HTML");

        bool Rejects(string json)
        {
            File.WriteAllText(metadata, json);
            try { _ = new HelpCatalog(source).IndexEntries; return false; }
            catch (InvalidDataException ex) { return ex.Message.Contains("index", StringComparison.OrdinalIgnoreCase); }
        }
        check(Rejects("{") && Rejects("[]") && Rejects("{\"schemaVersion\":2,\"entries\":[]}") &&
              Rejects("{\"schemaVersion\":1,\"entries\":{}}"), "Malformed JSON and unsupported Index schemas produce a clear data error");
        check(new[] { "null", "{}", "{\"term\":\" \",\"title\":\"Title\",\"path\":\"alpha.htm\"}",
            "{\"term\":\"Term\",\"title\":5,\"path\":\"alpha.htm\"}", "{\"term\":\"Term\",\"title\":\"Title\",\"path\":null}",
            "{\"term\":\"\\ud800\",\"title\":\"Title\",\"path\":\"alpha.htm\"}" }
            .All(entry => Rejects("{\"schemaVersion\":1,\"entries\":[" + entry + "]}")),
            "Every Index entry requires nonempty term, title and path strings");
        check(new[] { "../index-outside.htm", "%2e%2e/index-outside.htm", "https://example.invalid/topic.htm",
            "https://statsdirect.com/help/alpha.htm", "//example.invalid/topic.htm", "file:///C:/test.htm", "missing.htm", "index.html", "resources/helper.htm" }
            .All(target => Rejects(JsonSerializer.Serialize(new {schemaVersion=1,entries=new[] {new {term="Unsafe",title="Title",path=target}}}))),
            "Index rejects external, traversal, missing and helper-page targets instead of exposing a partial list");
        Write(new {schemaVersion=1,entries=Array.Empty<object>()});
        check(new HelpCatalog(source).HasKeywordIndex && new HelpCatalog(source).IndexEntries.Count == 0,
            "An authored empty keyword index is distinct from a legacy bundle without metadata");
    }
}
