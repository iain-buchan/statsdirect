using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StatsDirect.UI.WebReports;

internal static class CompletedCodeBlocks
{
    internal static string[] Read(string markdown, bool rOnly = true)
    {
        var blocks = new List<string>();
        string[] lines = (markdown ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var opening = Regex.Match(lines[i], @"^( {0,3})(`{3,}|~{3,})\s*([^`]*)$");
            if (!opening.Success) continue;
            string fence = opening.Groups[2].Value, language = opening.Groups[3].Value.Trim().Split(' ', '\t')[0];
            int indent = opening.Groups[1].Length;
            bool r = language.Equals("r", StringComparison.OrdinalIgnoreCase) || language.Equals("rscript", StringComparison.OrdinalIgnoreCase);
            var code = new List<string>();
            while (++i < lines.Length && !Regex.IsMatch(lines[i], @"^ {0,3}" + fence[0] + "{" + fence.Length + @",}\s*$"))
            {
                string line = lines[i]; int trim = 0; while (trim < Math.Min(indent,line.Length) && line[trim] == ' ') trim++;
                code.Add(line[trim..]);
            }
            if ((!rOnly || r) && i < lines.Length) blocks.Add(string.Join("\n", code));
        }
        return blocks.ToArray();
    }
}
