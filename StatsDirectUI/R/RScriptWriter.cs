using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StatsDirect.R
{
    /// <summary>A frame of a run as its record points to it: the workbook, the sheet and the columns the analysis read.</summary>
    public sealed class FramePointer
    {
        public string Name { get; set; }
        public string File { get; set; }
        public string Path { get; set; }
        public string Sheet { get; set; }
        public string Mode { get; set; }
        public IList<ColumnPointer> Columns { get; } = new List<ColumnPointer>();

        public static FramePointer From(JsonElement input)
        {
            FramePointer pointer = new()
            {
                Name = RScriptWriter.Text(input, "name"),
                File = RScriptWriter.Text(input, "file") ?? "",
                Path = RScriptWriter.Text(input, "path") ?? "",
                Sheet = RScriptWriter.Text(input, "sheet") ?? "",
                Mode = RScriptWriter.Text(input, "mode") ?? "NumericReplaceMissing"
            };
            if (input.TryGetProperty("columns", out JsonElement columns) && columns.ValueKind == JsonValueKind.Array)
                foreach (JsonElement column in columns.EnumerateArray())
                {
                    ColumnPointer c = new()
                    {
                        Title = RScriptWriter.Text(column, "title") ?? "",
                        Sheet = RScriptWriter.Text(column, "sheet") ?? pointer.Sheet,
                        Column = column.TryGetProperty("column", out JsonElement n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0,
                        HasTitle = column.TryGetProperty("hasTitle", out JsonElement h) && h.ValueKind == JsonValueKind.True,
                        Hash = RScriptWriter.Text(column, "hash")
                    };
                    if (column.TryGetProperty("rows", out JsonElement rows) && rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() == 2)
                    {
                        c.FirstRow = rows[0].GetInt32();
                        c.LastRow = rows[1].GetInt32();
                    }
                    pointer.Columns.Add(c);
                }
            return pointer;
        }
    }

    /// <summary>A column of a frame as the record points to it: 1-based column and rows of the selection as the user made it.</summary>
    public sealed class ColumnPointer
    {
        public string Title { get; set; }
        public string Sheet { get; set; }
        public int Column { get; set; }
        public int FirstRow { get; set; }
        public int LastRow { get; set; }
        public bool HasTitle { get; set; }
        public string Hash { get; set; }
    }

    /// <summary>The values read for a frame by its pointer, or why they could not be.</summary>
    public sealed class FrameValues
    {
        public IList<ColumnValues> Columns { get; } = new List<ColumnValues>();
        public string Unavailable { get; set; }
    }

    public sealed class ColumnValues
    {
        public string Title { get; set; }
        /// <summary>Numbers, text, flags or dates, as RunRecord.Values gives them; null for a missing value.</summary>
        public object[] Values { get; set; }
        /// <summary>Whether the values differ from those the analysis used, by the hash kept with the result.</summary>
        public bool Changed { get; set; }
        public bool Moved { get; set; }     // found at another column than the record's: the data moved along the sheet since the run
    }

    /// <summary>The script written for a result.</summary>
    public sealed class RScript
    {
        public string Text { get; set; }
        public bool HasRecipe { get; set; }
        public string Detail { get; set; }
        public bool DataUnavailable { get; set; }
    }

    /// <summary>
    /// Writes the R script of a result from the record of its run: a short script of data, settings and analysis, as a user would
    /// write it to explore in R or RStudio.  The data are plain named vectors (the values kept with the result, or read again from
    /// the workbook by the record's pointers, with a note on any column that has changed since the analysis); a table of counts is a
    /// matrix; the settings are plain assignments; the analysis is the recipe of the operation, derived from the R code in the help,
    /// whose header names the variables it takes.  Without a recipe the script holds the data and settings for the user's own analysis.
    /// </summary>
    public static class RScriptWriter
    {
        private static readonly CultureInfo inv = CultureInfo.InvariantCulture;

        /// <summary>A column of the run's data with its provenance, as the script will write it.</summary>
        private sealed class Column
        {
            public string Title;
            public object[] Values;
            public string Where;        // "column 1, rows 2 to 21" when read from a sheet
            public bool Changed;
            public bool Unavailable;
            public bool Inherited;      // of a frame the run inherited from the run it continues
        }

        /// <summary>A recipe's header: the variables it takes.</summary>
        /// <summary>One item of a recipe's "# data:" line: "name" takes the next column, "name..." the remaining columns as a list,
        /// "name = input" the first column of the input of that name, "name... = input" all of that input's columns as a list.</summary>
        private sealed class DataItem
        {
            public string Variable;
            public string Input;        // null: positional
            public bool IsList;
        }

        private sealed class Binding
        {
            public List<DataItem> Data = new();         // the variables, positional ones in the order of the frames' columns
            public string List;                         // a variable that takes every column not named above, as a named list
            public string Table;                        // a variable that takes a table of counts as a matrix
            public string Matrix;                       // a variable that takes a frame of counts as a matrix
            public List<(string variable, string parameter)> Settings = new();
            public List<(string variable, string input)> Nested = new();   // variables that take a two-level input (a frame2d, the grouped covariance's data) as a list of lists
            public string Body = "";
        }

        public static RScript Write(JsonElement record, string title, Func<FramePointer, FrameValues> read)
        {
            string operation = Text(record, "operation") ?? "";
            string analysisTitle = string.IsNullOrEmpty(title) ? Text(record, "title") ?? operation : title;
            RRecipe recipe = RRecipes.Find(operation);
            Binding binding = recipe != null ? Parse(RRecipes.Recipe(recipe.File)) : null;
            string detail = recipe?.Detail;
            List<string> notes = new();
            List<Column> columns = new();
            List<(string[][] counts, string[] rows, string[] cols, string rowsTitle, string colsTitle)> tables = new();
            bool unavailable = false;
            JsonElement kept = record.TryGetProperty("data", out JsonElement d) && d.ValueKind == JsonValueKind.Object ? d : default;
            string now = DateTime.Now.ToString("d MMM yyyy HH:mm", inv);
            List<(string name, int start, int count)> groups = new();   // the columns of each input, for the variables bound to an input by name
            Dictionary<string, (JsonElement value, bool inherited)> nested = new();   // the two-level inputs (a frame2d, the grouped covariance's data) by name
            if (record.TryGetProperty("inputs", out JsonElement inputs) && inputs.ValueKind == JsonValueKind.Array)
                foreach (JsonElement input in inputs.EnumerateArray())
                {
                    string name = Text(input, "name") ?? "input", kind = Text(input, "kind") ?? "other";
                    int start = columns.Count;
                    bool inherited = input.TryGetProperty("inherited", out JsonElement inheritedFlag) && inheritedFlag.ValueKind == JsonValueKind.True;
                    try
                    {
                    if (kind == "table")
                    {
                        tables.Add((Rows(input, "counts"), Strings(input, "rowLabels"), Strings(input, "columnLabels"), Text(input, "rowsTitle"), Text(input, "columnsTitle")));
                        continue;
                    }
                    if ((kind == "frame2d" || kind == "covariance") && input.TryGetProperty("value", out JsonElement levels))
                    {
                        // a two-level input: its blocks or groups, each a list of columns, kept whole for a nested list
                        nested[name] = (levels, inherited);
                        continue;
                    }
                    if (kind != "frame" && kind != "entered")
                        continue;
                    if (kind == "entered")
                    {
                        foreach (ColumnValues c in Entered(input))
                            columns.Add(new Column { Title = c.Title, Values = c.Values });
                        continue;
                    }
                    FramePointer pointer = FramePointer.From(input);
                    string source = $"\"{pointer.File}\", sheet \"{pointer.Sheet}\"";
                    if (kept.ValueKind == JsonValueKind.Object && kept.TryGetProperty(Text(input, "held") ?? name, out JsonElement held) && held.ValueKind == JsonValueKind.Object)
                    {
                        int i = 0;
                        foreach (ColumnValues c in Kept(held))
                            columns.Add(new Column { Title = c.Title, Values = c.Values, Where = Where(pointer, i++) });
                        notes.Add($"Data: {source}, the values the analysis used.");
                        continue;
                    }
                    FrameValues values;
                    try { values = read?.Invoke(pointer); }
                    catch (Exception ex) { values = new FrameValues { Unavailable = ex.Message }; }
                    if (values != null && values.Unavailable == null)
                    {
                        int i = 0;
                        foreach (ColumnValues c in values.Columns)
                            columns.Add(new Column { Title = c.Title, Values = c.Values, Where = Where(pointer, i++), Changed = c.Changed });
                        notes.Add($"Data: {source}, read on {now}.");
                        foreach (ColumnValues c in values.Columns.Where(c => c.Changed))
                            notes.Add($"NOTE: \"{c.Title}\" has changed in the workbook since the analysis ran; these are its values now.");
                        foreach (ColumnValues c in values.Columns.Where(c => c.Moved))
                            notes.Add($"NOTE: \"{c.Title}\" has moved along the sheet since the analysis ran, by columns inserted or removed to its left; its values are as they were.");
                    }
                    else
                    {
                        unavailable = true;
                        foreach (ColumnPointer c in pointer.Columns)
                            columns.Add(new Column { Title = c.Title, Values = Array.Empty<object>(), Where = Where(pointer, pointer.Columns.IndexOf(c)), Unavailable = true });
                        notes.Add($"Data: {source} could not be read ({values?.Unavailable ?? "no workbook is available"}): put the values in by hand.");
                    }
                    }
                    finally
                    {
                        if (kind == "frame" || kind == "entered")
                            groups.Add((name, start, columns.Count - start));
                        for (int i = start; i < columns.Count; i++) columns[i].Inherited = inherited;
                    }
                }
            StringBuilder s = new();
            s.Append("# ").Append(Plain(analysisTitle)).Append(", from StatsDirect, ").Append(RunTime(record)).Append('\n');
            foreach (string note in notes.Distinct())
                s.Append("# ").Append(Plain(note)).Append('\n');
            if (detail != null)
                s.Append("# ").Append(Plain(detail)).Append('\n');
            s.Append('\n');
            // the data
            HashSet<string> names = new();
            HashSet<int> bound = new();   // the columns taken by a variable bound to an input by name
            // a recipe that names every input it takes (no positional column, no rest list, no table or matrix): the run's other inputs, such
            // as a grid of new values a prediction asks for, are not the analysis's and are left out of its script
            bool namedOnly = binding != null && binding.List == null && binding.Matrix == null && binding.Table == null && binding.Data.All(i => i.Input != null) && (binding.Data.Count > 0 || binding.Nested.Count > 0);
            bool ownFrames = columns.Any(c => !c.Inherited);   // the run asked for frames itself: the bag's inherited frames are not its data
            void ListOf(string variable, IEnumerable<int> indices)
            {
                s.Append(variable).Append(" <- list(").Append(string.Join(",\n  ", indices.Select(i => Quote(columns[i].Title) + " = " + Vector(columns[i].Values)))).Append(")\n");
                names.Add(variable);
            }
            void Missing(string variable, string why)
            {
                // an optional column the run did not give (stratum labels, weights, a group identifier): the recipe can test for it
                names.Add(variable);
                s.Append(variable).Append(" <- NULL   # ").Append(why).Append('\n');
            }
            void NestedList(string variable, JsonElement value)
            {
                // a two-level input (blocks and treatments, groups and subgroups, the grouped covariance's groups with the predictor's
                // levels and then the outcome's replicates) as a list of lists, the inner vectors named by their titles when they have any
                names.Add(variable);
                if (!value.TryGetProperty("frames", out JsonElement frames) || frames.ValueKind != JsonValueKind.Array)
                {
                    s.Append(variable).Append(" <- NULL   # the run's data were too many to keep with the result\n");
                    return;
                }
                s.Append(variable).Append(" <- list(\n  ").Append(string.Join(",\n  ", frames.EnumerateArray().Select(level =>
                    "list(" + string.Join(", ", level.EnumerateArray().Select(c => (string.IsNullOrEmpty(Text(c, "title")) ? "" : Quote(Text(c, "title")) + " = ") + Vector(c.TryGetProperty("values", out JsonElement v) ? Objects(v) : Array.Empty<object>()))) + ")"))).Append(")\n");
            }
            if (binding != null)
                foreach (DataItem item in binding.Data.Where(i => i.Input != null))
                {
                    // a prompt repeated for each series, group or period gives several inputs of one name: a list takes every one of
                    // their columns, a vector the first not yet taken
                    List<int> indices = groups.Where(g => g.name == item.Input).SelectMany(g => Enumerable.Range(g.start, g.count)).Where(i => !bound.Contains(i)).ToList();
                    if (!groups.Any(g => g.name == item.Input)) { Missing(item.Variable, $"no \"{item.Input}\" in this run"); continue; }
                    if (item.IsList) { ListOf(item.Variable, indices); foreach (int i in indices) bound.Add(i); }
                    else if (indices.Count > 0) { int i = indices[0]; Assign(s, item.Variable, columns[i], names); bound.Add(i); }
                    else Missing(item.Variable, $"no column of \"{item.Input}\" is left for it");
                }
            HashSet<string> nestedTaken = new();
            if (binding != null)
                foreach ((string variable, string input) in binding.Nested)
                {
                    if (!nested.TryGetValue(input, out (JsonElement value, bool inherited) two)) { Missing(variable, $"no \"{input}\" in this run"); continue; }
                    nestedTaken.Add(input);
                    NestedList(variable, two.value);
                }
            int next = 0;
            void Skip() { while (next < columns.Count && bound.Contains(next)) next++; }
            if (binding != null)
                foreach (DataItem item in binding.Data.Where(i => i.Input == null))
                {
                    Skip();
                    if (next >= columns.Count) { Missing(item.Variable, "no column for it in this run"); continue; }
                    Assign(s, item.Variable, columns[next], names);
                    bound.Add(next++);
                }
            Skip();
            if (binding?.List != null)
            {
                ListOf(binding.List, Enumerable.Range(0, columns.Count).Where(i => !bound.Contains(i) && !(ownFrames && columns[i].Inherited)));   // the rest list: not the bag's grids
                next = columns.Count;
            }
            for (; next < columns.Count; next++)
                if (!bound.Contains(next) && !(binding != null && columns[next].Inherited) && !namedOnly)   // an inherited frame the recipe does not take is left out
                    Assign(s, SafeName(columns[next].Title, names), columns[next], names);
            foreach (KeyValuePair<string, (JsonElement value, bool inherited)> pair in nested)
                if (!nestedTaken.Contains(pair.Key) && !(binding != null && pair.Value.inherited) && !namedOnly)   // as for the columns: without a recipe every input is written
                    NestedList(SafeName(pair.Key, names), pair.Value.value);
            foreach ((string[][] counts, string[] rows, string[] cols, string rowsTitle, string colsTitle) in tables)
            {
                string variable = binding?.Table ?? SafeName("counts", names);
                s.Append(variable).Append(" <- matrix(c(").Append(string.Join(", ", counts.SelectMany(r => r))).Append("), ").Append(counts.Length).Append(", byrow = TRUE");
                if (rows.Length == counts.Length && cols.Length == (counts.Length > 0 ? counts[0].Length : 0))
                    s.Append(",\n  dimnames = list(").Append(rowsTitle != null ? Quote(rowsTitle) + " = " : "").Append("c(").Append(string.Join(", ", rows.Select(Quote))).Append("), ")
                     .Append(colsTitle != null ? Quote(colsTitle) + " = " : "").Append("c(").Append(string.Join(", ", cols.Select(Quote))).Append("))");
                s.Append(")\n");
                names.Add(variable);
            }
            if (binding?.Matrix != null && columns.Count > 0 && binding.Data.Count == 0 && binding.List == null)
            {
                // a frame of counts, its columns the columns of the matrix: written in place of the vectors above
                s.Clear();
                s.Append("# ").Append(Plain(analysisTitle)).Append(", from StatsDirect, ").Append(RunTime(record)).Append('\n');
                foreach (string note in notes.Distinct())
                    s.Append("# ").Append(Plain(note)).Append('\n');
                if (detail != null)
                    s.Append("# ").Append(Plain(detail)).Append('\n');
                s.Append('\n');
                s.Append(binding.Matrix).Append(" <- cbind(").Append(string.Join(",\n  ", columns.Select(c => Quote(c.Title) + " = " + Vector(c.Values)))).Append(")\n");
                names.Add(binding.Matrix);
            }
            // the settings
            JsonElement parameters = record.TryGetProperty("parameters", out JsonElement p) && p.ValueKind == JsonValueKind.Object ? p : default;
            if (binding != null)
            {
                foreach ((string variable, string parameter) in binding.Settings)
                    if (parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty(parameter, out JsonElement value) && IsScalar(value))
                        s.Append(variable).Append(" <- ").Append(Literal(value)).Append('\n');
            }
            else if (parameters.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty property in parameters.EnumerateObject())
                    if (IsScalar(property.Value) && !property.Name.StartsWith("statsdirect", StringComparison.OrdinalIgnoreCase))
                        s.Append(SafeName(property.Name, names)).Append(" <- ").Append(Literal(property.Value)).Append("   # ").Append(Plain(property.Name)).Append('\n');
            s.Append('\n');
            // the analysis
            if (binding != null)
                s.Append(binding.Body.TrimEnd('\n')).Append('\n');
            else
                s.Append("# Add your analysis here.\n");
            return new RScript { Text = s.ToString(), HasRecipe = recipe != null, Detail = detail ?? "Data and settings only: no R recipe of this method yet.", DataUnavailable = unavailable };
        }

        private static void Assign(StringBuilder s, string variable, Column column, HashSet<string> names)
        {
            names.Add(variable);
            s.Append(variable).Append(" <- ");
            if (column.Unavailable)
                s.Append("NULL");
            else
                s.Append(Vector(column.Values));
            s.Append("   # ").Append(Plain(column.Title));
            if (column.Where != null)
                s.Append(", ").Append(column.Where);
            if (column.Unavailable)
                s.Append(": not read, see above");
            s.Append('\n');
        }

        private static string Where(FramePointer pointer, int index)
        {
            if (index < 0 || index >= pointer.Columns.Count)
                return null;
            ColumnPointer c = pointer.Columns[index];
            return "column " + c.Column + ", rows " + c.FirstRow + " to " + c.LastRow;
        }

        /// <summary>The recipe's header and body.  The header lines at the top of the file: "# data: a, b", "# data: a, b, rest...",
        /// "# table: counts", "# matrix: counts", "# settings: confidence = gamma, mu = population-mean".</summary>
        private static Binding Parse(string recipe)
        {
            Binding b = new();
            string[] lines = recipe.Replace("\r\n", "\n").Split('\n');
            // the header is the comment block at the top: its keyword lines bind the variables; the body starts after the last line of
            // the header that is a keyword line or a line about the recipe itself, so that the analysis keeps its own opening comment
            int last = -1;
            for (int i = 0; i < lines.Length && (lines[i].Trim().Length == 0 || lines[i].StartsWith("#", StringComparison.Ordinal)); i++)
            {
                Match m = Regex.Match(lines[i], "^#\\s*(data|table|matrix|nested|settings|recipe):\\s*(.*)$", RegexOptions.IgnoreCase);
                if (!m.Success)
                {
                    if (Regex.IsMatch(lines[i], "^# (StatsDirect recipe|From the help|hand-edited)", RegexOptions.IgnoreCase))
                        last = i;
                    continue;
                }
                last = i;
                string what = m.Groups[1].Value.ToLowerInvariant(), rest = m.Groups[2].Value.Trim();
                switch (what)
                {
                    case "data":
                        foreach (string item in rest.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                        {
                            int eq = item.IndexOf('=');
                            string variable = (eq > 0 ? item[..eq] : item).Trim(), input = eq > 0 ? item[(eq + 1)..].Trim() : null;
                            bool list = variable.EndsWith("...", StringComparison.Ordinal);
                            if (list) variable = variable[..^3].Trim();
                            if (list && input == null) b.List = variable;
                            else b.Data.Add(new DataItem { Variable = variable, Input = input, IsList = list });
                        }
                        break;
                    case "table": b.Table = rest; break;
                    case "matrix": b.Matrix = rest; break;
                    case "nested":
                        foreach (string item in rest.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                        {
                            int eq = item.IndexOf('=');
                            if (eq > 0) b.Nested.Add((item[..eq].Trim(), item[(eq + 1)..].Trim()));
                        }
                        break;
                    case "settings":
                        foreach (string item in rest.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                        {
                            int eq = item.IndexOf('=');
                            if (eq > 0) b.Settings.Add((item[..eq].Trim(), item[(eq + 1)..].Trim()));
                        }
                        break;
                }
            }
            b.Body = string.Join("\n", lines.Skip(last + 1)).TrimStart('\n');
            return b;
        }

        internal static string Text(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static string RunTime(JsonElement record)
        {
            string run = Text(record, "run");
            return run != null && DateTime.TryParse(run, inv, DateTimeStyles.RoundtripKind, out DateTime time) ? time.ToLocalTime().ToString("d MMM yyyy HH:mm", inv) : run ?? "";
        }

        private static string[][] Rows(JsonElement input, string property)
        {
            if (!input.TryGetProperty(property, out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
                return Array.Empty<string[]>();
            return rows.EnumerateArray().Select(r => r.ValueKind == JsonValueKind.Array ? r.EnumerateArray().Select(Literal).ToArray() : new[] { Literal(r) }).ToArray();
        }

        private static string[] Strings(JsonElement input, string property) =>
            input.TryGetProperty(property, out JsonElement a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()).ToArray() : Array.Empty<string>();

        private static IList<ColumnValues> Entered(JsonElement input)
        {
            List<ColumnValues> columns = new();
            if (input.TryGetProperty("columns", out JsonElement array) && array.ValueKind == JsonValueKind.Array)
                foreach (JsonElement column in array.EnumerateArray())
                    columns.Add(new ColumnValues { Title = Text(column, "title") ?? "", Values = column.TryGetProperty("values", out JsonElement values) ? Objects(values) : Array.Empty<object>() });
            return columns;
        }

        private static IList<ColumnValues> Kept(JsonElement held)
        {
            List<ColumnValues> columns = new();
            foreach (JsonProperty property in held.EnumerateObject())
                columns.Add(new ColumnValues { Title = property.Name, Values = Objects(property.Value) });
            return columns;
        }

        private static object[] Objects(JsonElement array)
        {
            if (array.ValueKind != JsonValueKind.Array)
                return Array.Empty<object>();
            return array.EnumerateArray().Select(v => v.ValueKind switch
            {
                JsonValueKind.Number => (object)v.GetDouble(),
                JsonValueKind.String => v.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            }).ToArray();
        }

        /// <summary>A name R accepts, made from a column title or a parameter name, unique among those given.</summary>
        public static string SafeName(string title, HashSet<string> names)
        {
            string s = Regex.Replace((title ?? "").Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
            if (s.Length == 0 || char.IsDigit(s[0]))
                s = "x" + s;
            string candidate = s;
            for (int n = 2; names.Contains(candidate); n++)
                candidate = s + n.ToString(inv);
            names.Add(candidate);
            return candidate;
        }

        /// <summary>An R string literal.</summary>
        /// <summary>Text for a comment line: a control character (a line break in a column title, say) would end the comment and leave
        /// what follows to run as R, so each becomes a space.</summary>
        private static string Plain(string value) => string.Concat((value ?? "").Select(ch => ch < ' ' || ch == '\x7f' ? ' ' : ch));

        public static string Quote(string value)
        {
            StringBuilder s = new("\"");
            foreach (char ch in value ?? "")
                switch (ch)
                {
                    case '"': s.Append("\\\""); break;
                    case '\\': s.Append("\\\\"); break;
                    case '\n': s.Append("\\n"); break;
                    case '\r': s.Append("\\r"); break;
                    case '\t': s.Append("\\t"); break;
                    default:
                        if (ch == '\0') s.Append(' ');   // R cannot hold a NUL in a string, and its parser refuses the escape outright: a space, as in a comment
                        else if (ch < ' ' || ch == '\x7f') s.Append("\\u").Append(((int)ch).ToString("x4", inv));
                        else s.Append(ch);
                        break;
                }
            return s.Append('"').ToString();
        }

        /// <summary>A vector of a column's values: numeric when every value is a number or missing, else text.</summary>
        public static string Vector(object[] values)
        {
            bool numeric = values.All(v => v == null || v is double);
            if (values.Length == 0)
                return numeric ? "numeric(0)" : "character(0)";
            return "c(" + string.Join(", ", values.Select(v => numeric ? Number(v) : v == null ? "NA" : Quote(v is double x ? x.ToString("R", inv) : v is bool b ? (b ? "TRUE" : "FALSE") : v is DateTime t ? t.ToString("yyyy-MM-dd", inv) : v.ToString()))) + ")";
        }

        private static string Number(object value)
        {
            if (value is not double x)
                return "NA";
            if (double.IsNaN(x)) return "NaN";
            if (double.IsPositiveInfinity(x)) return "Inf";
            if (double.IsNegativeInfinity(x)) return "-Inf";
            return x.ToString("R", inv);
        }

        private static bool IsScalar(JsonElement value) => value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.String;

        /// <summary>The R literal of a JSON value: numbers, TRUE/FALSE, strings, lists and named lists, NULL.</summary>
        public static string Literal(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Number => Number(value.GetDouble()),
            JsonValueKind.True => "TRUE",
            JsonValueKind.False => "FALSE",
            JsonValueKind.String => Quote(value.GetString()),
            JsonValueKind.Array => "list(" + string.Join(", ", value.EnumerateArray().Select(Literal)) + ")",
            JsonValueKind.Object => "list(" + string.Join(", ", value.EnumerateObject().Select(p => Quote(p.Name) + " = " + Literal(p.Value))) + ")",
            _ => "NULL"
        };
    }
}
