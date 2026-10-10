using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StatsDirect.Templates
{
    /// <summary>
    /// The record of a run, as JSON, kept with each report item and saved with the report: the operation, when it ran, the data file,
    /// each input as it was asked (a frame read from a workbook as pointers: file, sheet, columns and rows, with a hash of each column's
    /// values; a frame typed in with its values), the settings, the scalar results, and the values of the pointer frames only when
    /// there are few.  The Mac version keeps the same record, so that a report moves between the two.  An R script of the analysis is
    /// written from the record and the workbook when the user asks for it.
    /// </summary>
    public static class RunRecord
    {
        public const int Version = 1;
        /// <summary>Above this many values in the pointer frames of a run, the values are not kept with the result: the workbook is read instead.</summary>
        public const int EmbeddingThreshold = 5000;
        private const int LongestText = 2000;
        private static readonly CultureInfo inv = CultureInfo.InvariantCulture;

        /// <summary>The kind of value a parameter takes, as the record names it.</summary>
        public static string KindOf(Parameter parameter) => parameter switch
        {
            FrameParameter => "frame",
            Frame2DParameter => "frame2d",
            ConfidenceIntervalParameter => "confidence",
            DoubleParameter => "number",
            IntegerParameter => "integer",
            BooleanParameter => "boolean",
            StringParameter => "text",
            DateParameter => "date",
            OptionParameter => "option",
            OptionsParameter => "options",
            PickFromListParameter => "choice",
            PickVariablesParameter => "variables",
            Double2By2Parameter => "table",
            Double2By2ByKParameter => "tables",
            EditGridParameter => "grid",
            GroupedCovarianceParameter => "covariance",
            RangeParameter => "range",
            _ => "other"
        };

        /// <summary>The record of the run as it stands, from the parameters the report step sees.</summary>
        public static string Write(OperationRun run, ParameterBag bag, int threshold = EmbeddingThreshold)
        {
            using MemoryStream stream = new();
            using (Utf8JsonWriter w = new(stream))
            {
                w.WriteStartObject();
                w.WriteNumber("version", Version);
                w.WriteString("operation", run.Operation?.Name ?? "");
                w.WriteString("title", run.Operation?.FriendlyName ?? run.Operation?.Name ?? "");
                w.WriteString("run", run.Started.ToString("o", inv));
                // the data file: that of the first frame read from a workbook
                WorksheetOrigin first = FirstOrigin(run, bag);
                if (first != null)
                {
                    w.WritePropertyName("source");
                    WriteSource(w, first.WorkbookPath);
                }
                // the inputs as they were asked
                int pointerValues = 0;
                List<KeyValuePair<string, DataFrame>> pointerFrames = new();
                RunInput table = null;
                double[] counts = null;
                w.WriteStartArray("inputs");
                foreach (RunInput input in run.Inputs)
                {
                    if (input.Kind == "table" && input.Parts != null && TableCounts(input, bag) is double[] cells)
                    {
                        // a 2 by 2 table typed in: its columns as a frame, for a recipe that takes columns, and its rows of counts with their labels
                        if (table == null)
                        {
                            table = input;
                            counts = cells;
                        }
                        w.WriteStartObject();
                        w.WriteString("name", input.Name);
                        w.WriteString("prompt", input.Prompt ?? "");
                        w.WriteString("kind", "table");
                        w.WriteStartArray("columns");
                        for (int c = 0; c < 2; c++)
                        {
                            w.WriteStartObject();
                            w.WriteString("title", input.ColumnLabels[c]);
                            w.WriteStartArray("values");
                            WriteScalar(w, cells[c]);
                            WriteScalar(w, cells[c + 2]);
                            w.WriteEndArray();
                            w.WriteEndObject();
                        }
                        w.WriteEndArray();
                        WriteTable(w, input, cells);
                        w.WriteEndObject();
                        continue;
                    }
                    if (!bag.TryGetValue(input.Name, out FilledParameter filled) || filled == null || !filled.HasData)
                        continue;
                    w.WriteStartObject();
                    w.WriteString("name", input.Name);
                    w.WriteString("prompt", input.Prompt ?? "");
                    if (filled.IsDataFrame)
                    {
                        DataFrame frame = filled.AsDataFrame;
                        if (IsPointerFrame(frame))
                        {
                            w.WriteString("kind", "frame");
                            WritePointer(w, frame);
                            pointerValues += Count(frame);
                            pointerFrames.Add(new KeyValuePair<string, DataFrame>(input.Name, frame));
                        }
                        else
                        {
                            w.WriteString("kind", "entered");
                            WriteColumns(w, frame);
                        }
                    }
                    else
                    {
                        w.WriteString("kind", input.Kind);
                        w.WritePropertyName("value");
                        WriteValue(w, filled.AsObject, 0, threshold);
                    }
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                // the settings: every input or default that is not a frame
                w.WriteStartObject("parameters");
                foreach (KeyValuePair<string, FilledParameter> pair in bag.Pairs)
                    if (IsSetting(pair.Key) && pair.Value != null && pair.Value.HasData && pair.Value.Direction != FilledParameterDirection.Output
                        && !pair.Value.IsDataFrame && Writable(pair.Value.AsObject))
                    {
                        w.WritePropertyName(pair.Key);
                        WriteValue(w, pair.Value.AsObject, 0, threshold);
                    }
                if (table != null && !bag.ContainsKey("counts"))
                    WriteTable(w, table, counts);   // the table's rows of counts and their labels, as a recipe takes a table typed in
                w.WriteEndObject();
                // the results: the scalar outputs
                w.WriteStartObject("values");
                foreach (KeyValuePair<string, FilledParameter> pair in bag.Pairs)
                    if (IsSetting(pair.Key) && pair.Value != null && pair.Value.HasData && pair.Value.Direction == FilledParameterDirection.Output && IsScalar(pair.Value.AsObject))
                    {
                        w.WritePropertyName(pair.Key);
                        WriteValue(w, pair.Value.AsObject, 0, threshold);
                    }
                w.WriteEndObject();
                // the values of the pointer frames, when few
                if (pointerFrames.Count > 0 && pointerValues <= threshold)
                {
                    w.WriteStartObject("data");
                    foreach (KeyValuePair<string, DataFrame> pointerFrame in pointerFrames)
                    {
                        w.WriteStartObject(pointerFrame.Key);
                        HashSet<string> titles = new();
                        foreach (IVariable variable in pointerFrame.Value.Variables)
                        {
                            w.WritePropertyName(UniqueTitle(variable.Title, titles));
                            WriteValues(w, variable);
                        }
                        w.WriteEndObject();
                    }
                    w.WriteEndObject();
                }
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>The values of a variable as the record holds them: a number, a string, a bool, a date, or null for a missing value.</summary>
        public static object[] Values(IVariable variable)
        {
            object[] values = new object[variable.Length];
            switch (variable)
            {
                case ClassifierVariable classifier:
                    for (int i = 0; i < values.Length; i++)
                    {
                        double id = classifier.Data[i];
                        values[i] = Missing(id) ? null : classifier.GroupWithId(id)?.Label ?? id.ToString("R", inv);
                    }
                    break;
                case GenericVariable<double> numbers:
                    for (int i = 0; i < values.Length; i++)
                        values[i] = Missing(numbers.Data[i]) ? null : numbers.Data[i];
                    break;
                case GenericVariable<string> texts:
                    for (int i = 0; i < values.Length; i++)
                        values[i] = texts.Data[i];
                    break;
                case GenericVariable<DateTime> dates:
                    for (int i = 0; i < values.Length; i++)
                        values[i] = dates.Data[i];
                    break;
                case GenericVariable<bool> flags:
                    for (int i = 0; i < values.Length; i++)
                        values[i] = flags.Data[i];
                    break;
                default:
                    for (int i = 0; i < values.Length; i++)
                    {
                        object o = variable.DataAsObject(i);
                        values[i] = o is double d ? (Missing(d) ? null : d) : o;
                    }
                    break;
            }
            return values;
        }

        /// <summary>
        /// The hash of a column's values, by which the two versions tell whether the data changed since a run: SHA-256 of the values
        /// as text joined by line feeds (numbers in round-trip form, a missing value as nothing, text as it is, dates in ISO 8601,
        /// TRUE or FALSE), the first 16 hexadecimal characters.
        /// </summary>
        public static string Hash(IVariable variable) => Hash(Values(variable));

        public static string Hash(IEnumerable<object> values)
        {
            StringBuilder text = new();
            bool firstValue = true;
            foreach (object value in values)
            {
                if (!firstValue)
                    text.Append('\n');
                firstValue = false;
                text.Append(Text(value));
            }
            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
            return Convert.ToHexString(digest, 0, 8).ToLowerInvariant();
        }

        public static string Text(object value) => value switch
        {
            null => "",
            double d => Missing(d) ? "" : d.ToString("R", inv),
            float f => ((double)f).ToString("R", inv),
            int i => i.ToString(inv),
            long l => l.ToString(inv),
            bool b => b ? "TRUE" : "FALSE",
            DateTime t => t.ToString("o", inv),
            string s => s,
            _ => value.ToString() ?? ""
        };

        public static bool Missing(double x) => Constant.MISSING == x || double.IsNaN(x) || double.IsInfinity(x);

        /// <summary>The four counts of a 2 by 2 table typed in, from the parameters that hold them, or null when any is missing.</summary>
        private static double[] TableCounts(RunInput input, ParameterBag bag)
        {
            double[] counts = new double[4];
            for (int i = 0; i < 4; i++)
            {
                if (!bag.TryGetValue(input.Parts[i] ?? "", out FilledParameter part) || part == null || !part.HasData || !part.IsDouble)
                    return null;
                counts[i] = part.AsDouble;
            }
            return counts;
        }

        /// <summary>The rows of counts of a 2 by 2 table and the labels of its rows and columns, as the recipes take them.</summary>
        private static void WriteTable(Utf8JsonWriter w, RunInput table, double[] counts)
        {
            w.WriteStartArray("counts");
            for (int r = 0; r < 2; r++)
            {
                w.WriteStartArray();
                WriteScalar(w, counts[2 * r]);
                WriteScalar(w, counts[2 * r + 1]);
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteStartArray("rowLabels");
            foreach (string label in table.RowLabels)
                w.WriteStringValue(label);
            w.WriteEndArray();
            w.WriteStartArray("columnLabels");
            foreach (string label in table.ColumnLabels)
                w.WriteStringValue(label);
            w.WriteEndArray();
        }

        private static int Count(DataFrame frame)
        {
            int n = 0;
            foreach (IVariable variable in frame.Variables)
                n += variable.Length;
            return n;
        }

        /// <summary>A frame read from a workbook: every variable has a worksheet origin.</summary>
        private static bool IsPointerFrame(DataFrame frame)
        {
            if (frame == null || frame.Variables.Count == 0)
                return false;
            foreach (IVariable variable in frame.Variables)
                if (variable.Origin is not WorksheetOrigin origin || string.IsNullOrEmpty(origin.WorksheetName))
                    return false;
            return true;
        }

        private static WorksheetOrigin FirstOrigin(OperationRun run, ParameterBag bag)
        {
            foreach (RunInput input in run.Inputs)
                if (bag.TryGetValue(input.Name, out FilledParameter filled) && filled != null && filled.HasData && filled.IsDataFrame && IsPointerFrame(filled.AsDataFrame))
                    return (WorksheetOrigin)filled.AsDataFrame.Variables[0].Origin;
            return null;
        }

        private static void WriteSource(Utf8JsonWriter w, string workbookPath)
        {
            w.WriteStartObject();
            string path = workbookPath ?? "";
            w.WriteString("file", SafeFileName(path));
            if (path.IndexOf(Path.DirectorySeparatorChar) >= 0 && File.Exists(path))
            {
                w.WriteString("path", path);
                w.WriteString("modified", File.GetLastWriteTime(path).ToString("o", inv));
            }
            else
                w.WriteBoolean("unsaved", true);
            w.WriteEndObject();
        }

        private static string SafeFileName(string path)
        {
            try { return Path.GetFileName(path) ?? path; }
            catch (ArgumentException) { return path; }
        }

        private static void WritePointer(Utf8JsonWriter w, DataFrame frame)
        {
            WorksheetOrigin first = (WorksheetOrigin)frame.Variables[0].Origin;
            w.WriteString("mode", first.Mode.ToString());
            w.WriteString("file", SafeFileName(first.WorkbookPath ?? ""));
            w.WriteString("path", first.WorkbookPath ?? "");
            w.WriteString("sheet", first.WorksheetName);
            w.WriteStartArray("columns");
            foreach (IVariable variable in frame.Variables)
            {
                WorksheetOrigin origin = (WorksheetOrigin)variable.Origin;
                w.WriteStartObject();
                w.WriteString("title", variable.Title ?? "");
                if (origin.WorksheetName != first.WorksheetName)
                    w.WriteString("sheet", origin.WorksheetName);
                w.WriteNumber("column", origin.Column + 1);
                w.WriteStartArray("rows");
                w.WriteNumberValue(origin.TopRow + 1);
                w.WriteNumberValue(origin.TopRow + origin.Rows);
                w.WriteEndArray();
                w.WriteBoolean("hasTitle", origin.HasTitle);
                w.WriteString("hash", Hash(variable));
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }

        /// <summary>A frame with its values: one typed in, or one of the settings.</summary>
        private static void WriteColumns(Utf8JsonWriter w, DataFrame frame)
        {
            w.WriteStartArray("columns");
            foreach (IVariable variable in frame.Variables)
            {
                w.WriteStartObject();
                w.WriteString("title", variable.Title ?? "");
                w.WritePropertyName("values");
                WriteValues(w, variable);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }

        private static void WriteValues(Utf8JsonWriter w, IVariable variable)
        {
            w.WriteStartArray();
            foreach (object value in Values(variable))
                WriteScalar(w, value);
            w.WriteEndArray();
        }

        private static string UniqueTitle(string title, HashSet<string> titles)
        {
            string candidate = title ?? "";
            for (int n = 2; !titles.Add(candidate); n++)
                candidate = (title ?? "") + "_" + n.ToString(inv);
            return candidate;
        }

        private static bool IsSetting(string key) => !string.IsNullOrEmpty(key) && !key.StartsWith("statsdirect", StringComparison.OrdinalIgnoreCase);

        private static bool IsScalar(object value) => value is double or int or long or bool || value is string s && s.Length <= LongestText;

        private static bool Writable(object value) =>
            value is null or double or int or long or bool or string or IList<string> or ParameterBag or IList<ParameterBag> or DataFrame2D;

        private static void WriteScalar(Utf8JsonWriter w, object value)
        {
            switch (value)
            {
                case null: w.WriteNullValue(); break;
                case double d: if (Missing(d)) w.WriteNullValue(); else w.WriteNumberValue(d); break;
                case float f: if (Missing(f)) w.WriteNullValue(); else w.WriteNumberValue(f); break;
                case int i: w.WriteNumberValue(i); break;
                case long l: w.WriteNumberValue(l); break;
                case bool b: w.WriteBooleanValue(b); break;
                case DateTime t: w.WriteStringValue(t.ToString("o", inv)); break;
                case string s: w.WriteStringValue(s); break;
                default: w.WriteStringValue(value.ToString()); break;
            }
        }

        private static void WriteValue(Utf8JsonWriter w, object value, int depth, int threshold)
        {
            switch (value)
            {
                case IList<string> list:
                    w.WriteStartArray();
                    foreach (string s in list)
                        w.WriteStringValue(s);
                    w.WriteEndArray();
                    break;
                case ParameterBag bag:
                    w.WriteStartObject();
                    if (depth < 3)
                        foreach (KeyValuePair<string, FilledParameter> pair in bag.Pairs)
                            if (IsSetting(pair.Key) && pair.Value != null && pair.Value.HasData && !pair.Value.IsDataFrame && Writable(pair.Value.AsObject))
                            {
                                w.WritePropertyName(pair.Key);
                                WriteValue(w, pair.Value.AsObject, depth + 1, threshold);
                            }
                    w.WriteEndObject();
                    break;
                case IList<ParameterBag> bags:
                    w.WriteStartArray();
                    if (depth < 3)
                        foreach (ParameterBag bag in bags)
                            WriteValue(w, bag, depth + 1, threshold);
                    w.WriteEndArray();
                    break;
                case DataFrame2D frame2d:
                    // a two-level frame with its values when there are few; else only that it was omitted
                    int count = 0;
                    foreach (IList<IVariable> variables in frame2d.Variables)
                        foreach (IVariable variable in variables)
                            count += variable.Length;
                    w.WriteStartObject();
                    if (count <= threshold)
                    {
                        w.WriteStartArray("frames");
                        foreach (IList<IVariable> variables in frame2d.Variables)
                        {
                            w.WriteStartArray();
                            foreach (IVariable variable in variables)
                            {
                                w.WriteStartObject();
                                w.WriteString("title", variable.Title ?? "");
                                w.WritePropertyName("values");
                                WriteValues(w, variable);
                                w.WriteEndObject();
                            }
                            w.WriteEndArray();
                        }
                        w.WriteEndArray();
                    }
                    else
                        w.WriteBoolean("omitted", true);
                    w.WriteEndObject();
                    break;
                default:
                    WriteScalar(w, value);
                    break;
            }
        }
    }
}
