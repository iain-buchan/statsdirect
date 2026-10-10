using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

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
    /// Writes the R script of a result from the record of its run: the settings, the results, the history of the inputs, the data
    /// (the values kept with the result, or read again from the workbook by the record's pointers), the shared helpers and the
    /// recipe of the operation, or, where there is none, the data and settings alone.  The layout is that of the Mac version, so
    /// that the recipes serve both.
    /// </summary>
    public static class RScriptWriter
    {
        private static readonly CultureInfo inv = CultureInfo.InvariantCulture;

        public static RScript Write(JsonElement record, string title, Func<FramePointer, FrameValues> read)
        {
            string operation = Text(record, "operation") ?? "";
            string analysisTitle = string.IsNullOrEmpty(title) ? Text(record, "title") ?? operation : title;
            RRecipe recipe = RRecipes.Find(operation);
            string detail = recipe?.Detail ?? "Data and settings only: an equivalent R implementation of this method is not yet available.";
            List<string> notes = new(), history = new();
            StringBuilder frames = new();
            HashSet<string> keys = new();
            bool unavailable = false;
            JsonElement kept = record.TryGetProperty("data", out JsonElement d) && d.ValueKind == JsonValueKind.Object ? d : default;
            if (record.TryGetProperty("inputs", out JsonElement inputs) && inputs.ValueKind == JsonValueKind.Array)
                foreach (JsonElement input in inputs.EnumerateArray())
                {
                    string name = Text(input, "name") ?? "input", prompt = Text(input, "prompt") ?? "", kind = Text(input, "kind") ?? "other";
                    if (kind != "frame" && kind != "entered" && kind != "table")
                    {
                        history.Add(NamedList(new[] { ("name", Quote(name)), ("title", Quote(prompt)), ("kind", Quote(kind)),
                                                      ("value", input.TryGetProperty("value", out JsonElement v) ? Literal(v) : "NULL") }));
                        continue;
                    }
                    string key = Unique(name, keys);
                    FramePointer pointer = kind == "frame" ? FramePointer.From(input) : null;
                    IList<ColumnValues> columns = null;
                    if (kind == "entered" || kind == "table")
                    {
                        columns = Entered(input);
                        notes.Add(kind == "table" ? $"{Quote(key)} holds the columns of the table of counts typed in for the analysis; parameters$counts holds its rows with their labels."
                                                  : $"{Quote(key)} holds the values typed in for the analysis.");
                    }
                    else if (kept.ValueKind == JsonValueKind.Object && kept.TryGetProperty(name, out JsonElement held) && held.ValueKind == JsonValueKind.Object)
                    {
                        columns = Kept(held);
                        notes.Add($"{Quote(key)} holds the values kept with the result, as read from {Quote(pointer.File)}, sheet {Quote(pointer.Sheet)}, when the analysis ran.");
                    }
                    else
                    {
                        FrameValues values;
                        try { values = read?.Invoke(pointer); }
                        catch (Exception ex) { values = new FrameValues { Unavailable = ex.Message }; }
                        if (values != null && values.Unavailable == null)
                        {
                            columns = values.Columns;
                            notes.Add($"{Quote(key)} was read from {Quote(pointer.File)}, sheet {Quote(pointer.Sheet)}, {Where(pointer)}, on {DateTime.Now.ToString("yyyy-MM-dd HH:mm", inv)}.");
                        }
                        else
                        {
                            unavailable = true;
                            notes.Add($"{Quote(key)} could not be read: {values?.Unavailable ?? "no workbook is available"}. Put its columns ({string.Join(", ", pointer.Columns.Select(c => Quote(c.Title)))}) into data_frames[[{Quote(key)}]] by hand.");
                        }
                    }
                    frames.Append("\n# Input: ").Append(Quote(key)).Append(prompt.Length > 0 ? " (" + prompt + ")" : "").Append('\n');
                    if (columns != null)
                    {
                        frames.Append("data_frames[[").Append(Quote(key)).Append("]] <- ").Append(NamedList(columns.Select(c => (c.Title ?? "", Vector(c.Values))))).Append('\n');
                        foreach (ColumnValues column in columns.Where(c => c.Changed))
                            frames.Append("# NOTE: the values of ").Append(Quote(column.Title)).Append(" are not those the analysis used: the column has changed in the workbook since it ran.\n");
                    }
                    else
                        frames.Append("data_frames[[").Append(Quote(key)).Append("]] <- NULL   # not available: see the note at the top\n");
                    List<(string, string)> where = new();
                    if (pointer != null)
                    {
                        where.Add(("file", Quote(pointer.File)));
                        where.Add(("sheet", Quote(pointer.Sheet)));
                    }
                    where.Add(("data_frame", Quote(key)));
                    history.Add(NamedList(new[] { ("name", Quote(name)), ("title", Quote(prompt)), ("kind", Quote(kind)), ("value", NamedList(where)) }));
                }
            StringBuilder s = new();
            s.Append("# StatsDirect: continue this completed analysis in R\n");
            s.Append("# ").Append(analysisTitle).Append(" (").Append(operation).Append("), run on ").Append(RunTime(record)).Append(".\n");
            foreach (string note in notes)
                s.Append("# ").Append(note).Append('\n');
            s.Append("# ").Append(detail).Append('\n');
            s.Append("# No packages are needed. This script can also be saved and run outside StatsDirect.\n");
            s.Append("operation <- ").Append(Quote(operation)).Append('\n');
            s.Append("analysis_title <- ").Append(Quote(analysisTitle)).Append('\n');
            s.Append("parameters <- ").Append(record.TryGetProperty("parameters", out JsonElement parameters) ? Literal(parameters) : "list()").Append('\n');
            s.Append("original_results <- ").Append(record.TryGetProperty("values", out JsonElement values2) ? Literal(values2) : "list()").Append('\n');
            s.Append("# Data inputs in this history refer to the exact selected columns in data_frames.\n");
            s.Append("input_history <- list(").Append(string.Join(",\n  ", history)).Append(")\n");
            s.Append("data_frames <- list()\n");
            s.Append(frames);
            s.Append("\n# The output tables are in the report itself; a recipe recomputes what it needs.\nresult_tables <- list()\n\n");
            s.Append(RRecipes.Helpers).Append('\n');
            s.Append("# Analysis: edit or extend the following code.\n");
            if (recipe != null)
                s.Append(RRecipes.Recipe(recipe.File));
            else
                s.Append("cat(").Append(Quote(detail + "\n")).Append(")\nstr(data_frames)\nprint(parameters)\n# original_results holds the StatsDirect results for reference.\n# Add your own R analysis here.\n");
            return new RScript { Text = s.ToString(), HasRecipe = recipe != null, Detail = detail, DataUnavailable = unavailable };
        }

        internal static string Text(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static string RunTime(JsonElement record)
        {
            string run = Text(record, "run");
            return run != null && DateTime.TryParse(run, inv, DateTimeStyles.RoundtripKind, out DateTime time) ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm", inv) : run ?? "an unknown date";
        }

        private static string Where(FramePointer pointer)
        {
            if (pointer.Columns.Count == 0)
                return "no columns";
            int first = pointer.Columns.Min(c => c.Column), last = pointer.Columns.Max(c => c.Column);
            int top = pointer.Columns.Min(c => c.FirstRow), bottom = pointer.Columns.Max(c => c.LastRow);
            return (first == last ? "column " + first : "columns " + first + " to " + last) + ", rows " + top + " to " + bottom;
        }

        private static string Unique(string name, HashSet<string> keys)
        {
            string key = name;
            for (int n = 2; !keys.Add(key); n++)
                key = name + "_" + n.ToString(inv);
            return key;
        }

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

        /// <summary>An R string literal.</summary>
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
                        if (ch < ' ' || ch == '\x7f') s.Append("\\u").Append(((int)ch).ToString("x4", inv));
                        else s.Append(ch);
                        break;
                }
            return s.Append('"').ToString();
        }

        /// <summary>A named list; setNames allows empty and repeated names without treating them as R code.</summary>
        public static string NamedList(IEnumerable<(string name, string value)> entries)
        {
            (string name, string value)[] list = entries.ToArray();
            if (list.Length == 0)
                return "list()";
            return "setNames(list(" + string.Join(",\n  ", list.Select(e => e.value)) + "), c(" + string.Join(", ", list.Select(e => Quote(e.name))) + "))";
        }

        /// <summary>A vector of a column's values: numeric when every value is a number or missing, else text.</summary>
        public static string Vector(object[] values)
        {
            bool numeric = values.All(v => v == null || v is double);
            if (values.Length == 0)
                return numeric ? "numeric(0)" : "character(0)";
            return "c(" + string.Join(", ", values.Select(v => numeric ? Number(v) : v == null ? "NA_character_" : Quote(v is double x ? x.ToString("R", inv) : v is bool b ? (b ? "TRUE" : "FALSE") : v is DateTime t ? t.ToString("yyyy-MM-dd", inv) : v.ToString()))) + ")";
        }

        private static string Number(object value)
        {
            if (value is not double x)
                return "NA_real_";
            if (double.IsNaN(x)) return "NaN";
            if (double.IsPositiveInfinity(x)) return "Inf";
            if (double.IsNegativeInfinity(x)) return "-Inf";
            return x.ToString("R", inv);
        }

        /// <summary>The R literal of a JSON value: numbers, TRUE/FALSE, strings, lists and named lists, NULL.</summary>
        public static string Literal(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Number => Number(value.GetDouble()),
            JsonValueKind.True => "TRUE",
            JsonValueKind.False => "FALSE",
            JsonValueKind.String => Quote(value.GetString()),
            JsonValueKind.Array => "list(" + string.Join(", ", value.EnumerateArray().Select(Literal)) + ")",
            JsonValueKind.Object => NamedList(value.EnumerateObject().Select(p => (p.Name, Literal(p.Value)))),
            _ => "NULL"
        };
    }
}
