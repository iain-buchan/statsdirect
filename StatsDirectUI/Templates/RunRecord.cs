using StatsDirect.Builtins;
using StatsDirect.Data;
using System.Linq;
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
        internal const string AcquisitionKey = "statsdirect-acquisition-snapshots";
        internal const string RootOperationKey = "statsdirect-root-operation";

        /// <summary>An acquisition-time copy of the record's JSON, independent of arrays that an analysis may sort or filter.</summary>
        internal sealed class InputSnapshot
        {
            internal JsonElement Content;
            internal JsonElement Held;
            internal JsonElement Source;
            internal int PointerCount;
            internal int HeldCount;
            internal ColumnSnapshot[] Columns;
            // Dummy-coded worksheet predictors are saved as entered values without
            // origins. Their row-preserving parameter still ties them to the other
            // frames; ordinary helper/title frames do not have that contract.
            internal bool AlignedEntered;
            internal bool KeepAcquiredRows;
            internal int[] EnteredCounts, EnteredLastValues;
            internal InputSnapshot Copy() => new() { Content = Content, Held = Held, Source = Source, PointerCount = PointerCount,
                HeldCount = HeldCount, Columns = Columns, AlignedEntered = AlignedEntered, KeepAcquiredRows = KeepAcquiredRows,
                EnteredCounts = EnteredCounts, EnteredLastValues = EnteredLastValues };
        }

        /// <summary>Acquired column bounds and a constant-size hash state. Trailing missing
        /// values need only newline separators, so a later aligned cutoff can be hashed
        /// without retaining large data arrays or reading an analysis's mutated values.</summary>
        internal sealed class ColumnSnapshot
        {
            internal readonly int Count, LastValueCount;
            internal readonly bool Positional, CanTrim;
            internal readonly int[] DataRows;
            private readonly IncrementalHash prefix;
            private static readonly byte[] Separator = { 10 };

            internal ColumnSnapshot(IVariable variable, bool acquired)
            {
                object[] values = Values(variable);
                Count = LastValueCount = values.Length;
                while (LastValueCount > 0 && values[LastValueCount - 1] == null) LastValueCount--;
                var origin = (WorksheetOrigin)variable.Origin;
                DataRows = origin.DataRowIndices?.ToArray();
                // Only positional acquisition can distinguish an unused blank tail
                // from explicit missing markers kept by NumericReplaceMissing.
                Positional = origin.Mode is DataAcquisitionMode.NumericPreserveRows
                    or DataAcquisitionMode.NumericCodingTextToDummiesPreserveRows or DataAcquisitionMode.CategoryCombineAllColumns;
                CanTrim = acquired && (origin.Mode switch
                {
                    DataAcquisitionMode.NumericPreserveRows or DataAcquisitionMode.NumericCodingTextToDummiesPreserveRows
                        => !origin.WasFiltered || DataRows?.Length == Count,
                    DataAcquisitionMode.CategoryCombineAllColumns => !origin.WasFiltered,
                    _ => false
                });
                prefix = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                for (int i = 0; i < LastValueCount; i++)
                {
                    if (i > 0) prefix.AppendData(Separator);
                    prefix.AppendData(Encoding.UTF8.GetBytes(Text(values[i])));
                }
            }

            internal string HashAt(int count)
            {
                IncrementalHash copy;
                lock (prefix) copy = prefix.Clone(); // one acquisition may serve concurrent report exports
                using IncrementalHash hash = copy;
                int missing = count - LastValueCount - (LastValueCount == 0 && count > 0 ? 1 : 0);
                Span<byte> separators = stackalloc byte[256];
                separators.Fill(10);
                while (missing > 0)
                {
                    int block = Math.Min(missing, separators.Length);
                    hash.AppendData(separators[..block]);
                    missing -= block;
                }
                return Convert.ToHexString(hash.GetHashAndReset(), 0, 8).ToLowerInvariant();
            }

            internal int KeptCount(int rows) => CanTrim ? Math.Min(rows, Count) : Count;
        }

        private static JsonElement Json(Action<Utf8JsonWriter> write)
        {
            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream)) write(writer);
            using JsonDocument document = JsonDocument.Parse(stream.ToArray());
            return document.RootElement.Clone();
        }

        internal static InputSnapshot Capture(FilledParameter value, string kind, bool rowAligned = false, bool acquired = true)
        {
            if (value?.HasData != true) return null;
            InputSnapshot snapshot = new();
            snapshot.Content = Json(w =>
            {
                w.WriteStartObject();
                if (value.IsDataFrame)
                {
                    DataFrame frame = value.AsDataFrame;
                    bool padded = frame.Variables.Any(v => v.Origin is WorksheetOrigin { WasPadded: true });
                    if (IsPointerFrame(frame) && !padded)
                    {
                        w.WriteString("kind", "frame");
                        snapshot.Columns = frame.Variables.Select(variable => new ColumnSnapshot(variable, acquired)).ToArray();
                        WritePointer(w, frame, snapshot.Columns);
                        snapshot.PointerCount = Count(frame);
                        snapshot.Source = Json(source => WriteSource(source, ((WorksheetOrigin)frame.Variables[0].Origin).WorkbookPath));
                        snapshot.HeldCount = snapshot.Columns.Sum(c => c.Positional ? c.LastValueCount : c.Count);
                        if (snapshot.HeldCount <= EmbeddingThreshold)
                            snapshot.Held = Json(data =>
                            {
                                data.WriteStartObject();
                                HashSet<string> titles = new();
                                for (int i = 0; i < frame.VariableCount; i++)
                                {
                                    IVariable variable = frame.Variables[i];
                                    data.WritePropertyName(UniqueTitle(variable.Title, titles));
                                    object[] values = Values(variable);
                                    int count = snapshot.Columns[i].Positional ? snapshot.Columns[i].LastValueCount : values.Length;
                                    data.WriteStartArray();
                                    for (int j = 0; j < count; j++) WriteScalar(data, values[j]);
                                    data.WriteEndArray();
                                }
                                data.WriteEndObject();
                            });
                    }
                    else
                    {
                        w.WriteString("kind", "entered");
                        WriteColumns(w, frame);
                        snapshot.AlignedEntered = rowAligned || padded;
                        snapshot.KeepAcquiredRows = padded;
                        if (snapshot.AlignedEntered)
                        {
                            object[][] values = frame.Variables.Select(Values).ToArray();
                            snapshot.EnteredCounts = values.Select(v => v.Length).ToArray();
                            snapshot.EnteredLastValues = values.Select(v =>
                            {
                                int count = v.Length;
                                while (count > 0 && v[count - 1] == null) count--;
                                return count;
                            }).ToArray();
                        }
                        if (padded && frame.Variables.Select(v => v.Origin).OfType<WorksheetOrigin>().FirstOrDefault() is WorksheetOrigin origin)
                            snapshot.Source = Json(source => WriteSource(source, origin.WorkbookPath));
                    }
                }
                else
                {
                    w.WriteString("kind", kind);
                    w.WritePropertyName("value");
                    WriteValue(w, value.AsObject, 0, EmbeddingThreshold);
                }
                w.WriteEndObject();
            });
            return snapshot;
        }

        private static void WriteSnapshot(Utf8JsonWriter writer, InputSnapshot snapshot)
        {
            foreach (JsonProperty property in snapshot.Content.EnumerateObject()) property.WriteTo(writer);
        }

        /// <summary>A frame in the bag that an earlier run acquired and this run took over without asking for it again.</summary>
        private static bool IsInheritedFrame(OperationRun run, KeyValuePair<string, FilledParameter> pair) =>
            IsSetting(pair.Key) && pair.Value != null && pair.Value.HasData && pair.Value.Direction != FilledParameterDirection.Output
            && (pair.Value.IsDataFrame || pair.Value.AsObject is DataFrame2D) && !run.Inputs.Any(i => i.Name == pair.Key);

        /// <summary>
        /// The rows the record keeps of the run's pointer frames: up to the last row that holds a value in any column of any of them, so
        /// that the trailing rows blank in every column (a whole-column selection's rows below the data) are left out, while frames
        /// selected together keep the same row count and so stay aligned. This is independent
        /// of the limit on embedded data. Unknown legacy filtered coordinates are left intact.
        /// </summary>
        private static int KeptRows(IEnumerable<InputSnapshot> snapshots)
        {
            int kept = 0;
            int original = 0;
            foreach (InputSnapshot snapshot in snapshots)
            {
                if (snapshot?.AlignedEntered == true && snapshot.EnteredCounts != null)
                {
                    original = Math.Max(original, snapshot.EnteredCounts.DefaultIfEmpty().Max());
                    kept = Math.Max(kept, (snapshot.KeepAcquiredRows ? snapshot.EnteredCounts : snapshot.EnteredLastValues).DefaultIfEmpty().Max());
                    continue;
                }
                if (snapshot == null || snapshot.Content.ValueKind != JsonValueKind.Object || snapshot.Content.GetProperty("kind").GetString() != "frame")
                    continue;
                if (snapshot.Columns == null)
                    return -1;
                foreach (ColumnSnapshot column in snapshot.Columns)
                {
                    if (!column.Positional) continue;
                    original = Math.Max(original, column.Count);
                    // Unknown filtered coordinates still protect their matched row
                    // alignment. Non-positional modes do not constrain this cutoff.
                    kept = Math.Max(kept, column.CanTrim ? column.LastValueCount : column.Count);
                }
            }
            return kept > 0 ? kept : original; // no observations: retain valid original ranges
        }

        /// <summary>
        /// A pointer frame's snapshot cut to the kept rows: each column's held values, its row range and its hash then describe the same
        /// rows, so that reading the recorded rows back gives values with the recorded hash. The snapshot itself when nothing is cut.
        /// </summary>
        private static InputSnapshot Trim(InputSnapshot snapshot, int keptRows)
        {
            if (keptRows < 0) keptRows = int.MaxValue;
            if (snapshot?.AlignedEntered == true && snapshot.EnteredCounts != null && keptRows >= 0)
            {
                InputSnapshot entered = snapshot.Copy();
                entered.Content = Json(w =>
                {
                    w.WriteStartObject();
                    foreach (JsonProperty property in snapshot.Content.EnumerateObject())
                    {
                        if (property.Name != "columns") { property.WriteTo(w); continue; }
                        w.WriteStartArray("columns");
                        int i = 0;
                        foreach (JsonElement column in property.Value.EnumerateArray())
                        {
                            int count = Math.Min(keptRows, snapshot.EnteredCounts[i++]);
                            w.WriteStartObject();
                            foreach (JsonProperty p in column.EnumerateObject())
                            {
                                if (p.Name != "values") { p.WriteTo(w); continue; }
                                w.WriteStartArray("values");
                                int j = 0;
                                foreach (JsonElement value in p.Value.EnumerateArray()) { if (j++ == count) break; value.WriteTo(w); }
                                w.WriteEndArray();
                            }
                            w.WriteEndObject();
                        }
                        w.WriteEndArray();
                    }
                    w.WriteEndObject();
                });
                return entered;
            }
            if (snapshot == null || keptRows < 0 || snapshot.Content.ValueKind != JsonValueKind.Object || snapshot.Columns == null
                || snapshot.Content.GetProperty("kind").GetString() != "frame" || !snapshot.Content.TryGetProperty("columns", out JsonElement columns))
                return snapshot;
            if (snapshot.Columns.Length != columns.GetArrayLength())
                return snapshot;
            InputSnapshot trimmed = new() { Source = snapshot.Source, Columns = snapshot.Columns,
                HeldCount = snapshot.HeldCount, PointerCount = snapshot.Columns.Sum(column => column.KeptCount(keptRows)) };
            if (snapshot.Held.ValueKind == JsonValueKind.Object && trimmed.PointerCount <= EmbeddingThreshold) trimmed.Held = Json(data =>
            {
                data.WriteStartObject();
                int column = 0;
                foreach (JsonProperty h in snapshot.Held.EnumerateObject())
                {
                    int count = snapshot.Columns[column++].KeptCount(keptRows);
                    data.WritePropertyName(h.Name);
                    data.WriteStartArray();
                    int i = 0;
                    foreach (JsonElement value in h.Value.EnumerateArray()) { if (i++ == count) break; value.WriteTo(data); }
                    for (int j = h.Value.GetArrayLength(); j < count; j++) data.WriteNullValue();
                    data.WriteEndArray();
                }
                data.WriteEndObject();
            });
            trimmed.Content = Json(w =>
            {
                w.WriteStartObject();
                foreach (JsonProperty property in snapshot.Content.EnumerateObject())
                {
                    if (property.Name != "columns")
                    {
                        property.WriteTo(w);
                        continue;
                    }
                    w.WriteStartArray("columns");
                    int c = 0;
                    foreach (JsonElement column in columns.EnumerateArray())
                    {
                        ColumnSnapshot acquired = snapshot.Columns[c++];
                        int count = acquired.KeptCount(keptRows);
                        w.WriteStartObject();
                        foreach (JsonProperty p in column.EnumerateObject())
                        {
                            if (p.Name == "rows" && count < acquired.Count && acquired.CanTrim && p.Value.ValueKind == JsonValueKind.Array && p.Value.GetArrayLength() == 2)
                            {
                                // Filtered observations retain their physical worksheet coordinates.
                                int firstRow = p.Value[0].GetInt32(), lastRow = p.Value[1].GetInt32();
                                bool hasTitle = column.TryGetProperty("hasTitle", out JsonElement title) && title.ValueKind == JsonValueKind.True;
                                w.WriteStartArray("rows");
                                w.WriteNumberValue(firstRow);
                                int end = count > 0 && acquired.DataRows != null && count <= acquired.DataRows.Length
                                    ? acquired.DataRows[count - 1] + 1 : firstRow - 1 + (hasTitle ? 1 : 0) + count;
                                w.WriteNumberValue(Math.Min(lastRow, end));
                                w.WriteEndArray();
                            }
                            else if (p.Name == "hash" && acquired.CanTrim)
                                w.WriteString("hash", acquired.HashAt(count));
                            else
                                p.WriteTo(w);
                        }
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            });
            return trimmed;
        }

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
                w.WriteString("rootOperation", run.RootOperation);
                w.WriteString("title", run.Operation?.FriendlyName ?? run.Operation?.Name ?? "");
                w.WriteString("run", run.Started.ToString("o", inv));
                // the data file: that of the first frame read from a workbook
                JsonElement source = run.Inputs.Select(input => input.Snapshot?.Source ?? default)
                    .Concat(run.InheritedInputs.Values.Where(snapshot => snapshot != null).Select(snapshot => snapshot.Source))
                    .FirstOrDefault(element => element.ValueKind == JsonValueKind.Object);
                WorksheetOrigin first = source.ValueKind == JsonValueKind.Object ? null : FirstOrigin(run, bag);
                if (source.ValueKind == JsonValueKind.Object || first != null)
                {
                    w.WritePropertyName("source");
                    if (source.ValueKind == JsonValueKind.Object) source.WriteTo(w);
                    else WriteSource(w, first.WorkbookPath);
                }
                // the inputs as they were asked
                int pointerValues = 0;
                List<KeyValuePair<string, InputSnapshot>> pointerFrames = new();
                RunInput table = null;
                double[] counts = null;
                // The snapshots of the inputs, taken now where the run holds none, so that the kept rows below see every frame. A selection
                // that runs past the data, such as a whole column on a sheet whose other columns are longer, carries trailing rows that are
                // blank in every column; the record keeps the rows up to the last value in any pointer frame of the run, the same count
                // for every frame, so that frames selected together stay aligned row by row (see Trim).
                Dictionary<RunInput, InputSnapshot> snapshots = new();
                foreach (RunInput input in run.Inputs)
                    if (!(input.Kind == "table" && input.Parts != null))
                    {
                        input.Snapshot ??= Capture(input.Value ?? (bag.TryGetValue(input.Name, out FilledParameter fallback) ? fallback : null), input.Kind, input.PreserveRowAlignment, acquired: false);
                        snapshots[input] = input.Snapshot;
                    }
                Dictionary<string, InputSnapshot> inherited = new();
                foreach (KeyValuePair<string, FilledParameter> pair in bag.Pairs)
                    if (IsInheritedFrame(run, pair))
                        inherited[pair.Key] = run.InheritedInputs.TryGetValue(pair.Key, out InputSnapshot acquired) && acquired != null ? acquired
                            : Capture(pair.Value, pair.Value.IsDataFrame ? "frame" : "frame2d", acquired: false);
                int keptRows = KeptRows(snapshots.Values.Concat(inherited.Values));
                w.WriteStartArray("inputs");
                foreach (RunInput input in run.Inputs)
                {
                    if (input.Kind == "table" && input.Parts != null && (input.TableCounts ?? CaptureTableCounts(input, bag)) is double[] cells)
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
                    // the value as it was asked, else the bag's: a parameter asked in each pass of a loop keeps each pass's value
                    FilledParameter filled = input.Value ?? (bag.TryGetValue(input.Name, out FilledParameter current) ? current : null);
                    InputSnapshot snapshot = Trim(snapshots.TryGetValue(input, out InputSnapshot taken) ? taken : input.Snapshot ?? Capture(filled, input.Kind), keptRows);
                    if (snapshot == null)
                        continue;
                    w.WriteStartObject();
                    w.WriteString("name", input.Name);
                    w.WriteString("prompt", input.Prompt ?? "");
                    WriteSnapshot(w, snapshot);
                    if (snapshot.Content.GetProperty("kind").GetString() == "frame")
                    {
                        pointerValues += snapshot.PointerCount;
                        // A frame asked in each loop pass has its own held key.
                        string held = pointerFrames.Any(p => p.Key == input.Name) ? input.Name + "#" + (pointerFrames.Count(p => p.Key == input.Name || p.Key.StartsWith(input.Name + "#", StringComparison.Ordinal)) + 1) : input.Name;
                        if (held != input.Name) w.WriteString("held", held);
                        pointerFrames.Add(new KeyValuePair<string, InputSnapshot>(held, snapshot));
                    }
                    w.WriteEndObject();
                }
                // the frames the run inherited from an earlier one: a follow-on operation (a prediction, the residuals, an exact P by
                // simulation) takes the data of the result it was asked from without asking for them again
                foreach (KeyValuePair<string, FilledParameter> pair in bag.Pairs)
                {
                    if (!IsInheritedFrame(run, pair))
                        continue;
                    w.WriteStartObject();
                    w.WriteString("name", pair.Key);
                    w.WriteString("prompt", "");
                    w.WriteBoolean("inherited", true);
                    InputSnapshot snapshot = Trim(inherited[pair.Key], keptRows);
                    WriteSnapshot(w, snapshot);
                    if (snapshot.Content.GetProperty("kind").GetString() == "frame")
                    {
                        pointerValues += snapshot.PointerCount;
                        pointerFrames.Add(new KeyValuePair<string, InputSnapshot>(pair.Key, snapshot));
                    }
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                // the settings: every input or default that is not a frame
                w.WriteStartObject("parameters");
                // (a value the run asked for and the operation wrote back as a result under the same name, as the sample size
                // calculators do with the standard deviation, is still the run's setting)
                foreach (KeyValuePair<string, FilledParameter> pair in bag.Pairs)
                    if (IsSetting(pair.Key) && pair.Value != null && pair.Value.HasData && (pair.Value.Direction != FilledParameterDirection.Output || run.Inputs.Any(i => i.Name == pair.Key))
                        && !pair.Value.IsDataFrame && Writable(pair.Value.AsObject))
                    {
                        w.WritePropertyName(pair.Key);
                        // Mutable structured settings (not recalculated scalar results) retain their acquired values too.
                        InputSnapshot snapshot = run.Inputs.LastOrDefault(input => input.Name == pair.Key)?.Snapshot;
                        if (snapshot == null) run.InheritedInputs.TryGetValue(pair.Key, out snapshot);
                        if (snapshot != null && snapshot.Content.TryGetProperty("value", out JsonElement acquired)
                            && acquired.ValueKind is JsonValueKind.Object or JsonValueKind.Array) acquired.WriteTo(w);
                        else WriteValue(w, pair.Value.AsObject, 0, threshold);
                    }
                if (!bag.ContainsKey("ci"))
                    foreach (KeyValuePair<string, FilledParameter> pair in bag.Pairs)
                        if (pair.Value?.HasData == true && pair.Value.AsObject is GroupedCovarianceData covariance) { w.WriteNumber("ci", covariance.GAMMA); break; }
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
                if (pointerFrames.Count > 0 && pointerValues <= threshold && pointerFrames.All(frame => frame.Value.Held.ValueKind == JsonValueKind.Object))
                {
                    w.WriteStartObject("data");
                    foreach (KeyValuePair<string, InputSnapshot> pointerFrame in pointerFrames)
                    {
                        w.WritePropertyName(pointerFrame.Key);
                        pointerFrame.Value.Held.WriteTo(w);
                    }
                    w.WriteEndObject();
                }
                w.WriteEndObject();
            }
            // Follow-on operations receive the fitted frames, which may already be sorted or filtered. Carry their original
            // acquisition provenance in the private context so the next run need not mistake those arrays for worksheet order.
            bag[AcquisitionKey] = FilledParameterFactory.Input(run.AcquisitionSnapshots());
            bag[RootOperationKey] = FilledParameterFactory.Input(run.RootOperation);
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
        internal static double[] CaptureTableCounts(RunInput input, ParameterBag bag)
        {
            if (bag == null) return null;
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
            if (!string.IsNullOrEmpty(table.RowsTitle))
                w.WriteString("rowsTitle", table.RowsTitle);
            if (!string.IsNullOrEmpty(table.ColumnsTitle))
                w.WriteString("columnsTitle", table.ColumnsTitle);
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

        private static void WritePointer(Utf8JsonWriter w, DataFrame frame, ColumnSnapshot[] snapshots)
        {
            WorksheetOrigin first = (WorksheetOrigin)frame.Variables[0].Origin;
            w.WriteString("mode", first.Mode.ToString());
            w.WriteString("file", SafeFileName(first.WorkbookPath ?? ""));
            w.WriteString("path", first.WorkbookPath ?? "");
            w.WriteString("sheet", first.WorksheetName);
            w.WriteStartArray("columns");
            int index = 0;
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
                ColumnSnapshot acquired = snapshots[index++];
                w.WriteString("hash", acquired.HashAt(acquired.Count));
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
            value is null or double or int or long or bool or string or IList<string> or IList<bool> or IList<double> or ParameterBag or IList<ParameterBag> or DataFrame2D;

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
                case IList<bool> flags:   // the marks of a pick from a list (the observer that is a reference standard)
                    w.WriteStartArray();
                    foreach (bool flag in flags)
                        w.WriteBooleanValue(flag);
                    w.WriteEndArray();
                    break;
                case IList<double> numbers:   // numeric chart settings such as histogram edges and counts
                    w.WriteStartArray();
                    foreach (double number in numbers) WriteScalar(w, number);
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
                            count += variable?.Length ?? 0;
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
                                w.WriteString("title", variable?.Title ?? "");
                                w.WritePropertyName("values");
                                if (variable != null) WriteValues(w, variable); else { w.WriteStartArray(); w.WriteEndArray(); }   // a combination with no rows
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
                case GroupedCovarianceData covariance:
                    // the grouped covariance's data in the shape of a two-level frame: for each group, the predictor's levels first and then
                    // the outcome's replicates at each level, so that a recipe reads it as it reads a two-level frame
                    int values = 0;
                    for (int g = 1; g <= covariance.k; g++)
                        for (int j = 1; j <= covariance.nxi[g]; j++)
                            values += 1 + covariance.ny[g, j];
                    w.WriteStartObject();
                    if (values <= threshold)
                    {
                        w.WriteStartArray("frames");
                        for (int g = 1; g <= covariance.k; g++)
                        {
                            w.WriteStartArray();
                            w.WriteStartObject();
                            w.WriteString("title", covariance.cx[g]?.Title ?? "");
                            w.WriteStartArray("values");
                            for (int j = 1; j <= covariance.nxi[g]; j++) WriteScalar(w, covariance.xt[g, j]);
                            w.WriteEndArray();
                            w.WriteEndObject();
                            for (int j = 1; j <= covariance.nxi[g]; j++)
                            {
                                w.WriteStartObject();
                                w.WriteString("title", "");
                                w.WriteStartArray("values");
                                for (int r = 1; r <= covariance.ny[g, j]; r++) WriteScalar(w, covariance.y[g, j, r]);
                                w.WriteEndArray();
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
