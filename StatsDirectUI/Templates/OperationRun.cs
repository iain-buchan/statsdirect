using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    /// <summary>
    /// The run of an operation: what the report keeps of it with each item the run puts there, so that a result can be continued in R
    /// or, later, run again on the workbook.  The template processor makes one per run, notes each parameter as it is asked, and writes
    /// the record (RunRecord) at each report step.
    /// </summary>
    public sealed class OperationRun
    {
        public Operation Operation { get; }
        public DateTime Started { get; } = DateTime.Now;
        /// <summary>The parameters as they were asked, in order; a parameter asked again in the same pass replaces its earlier note, and one
        /// asked in each pass of a loop (the groups of a survival plot) is noted once per pass with the value of that pass.</summary>
        public IList<RunInput> Inputs { get; } = new List<RunInput>();
        /// <summary>The JSON record of the run as it stood at the last report step, or null when none has been written.</summary>
        public string Record { get; set; }

        public OperationRun(Operation operation)
        {
            Operation = operation;
        }

        public void Note(Parameter parameter, string prompt, string pass = "", FilledParameter value = null)
        {
            if (string.IsNullOrEmpty(parameter?.Name))
                return;
            for (int i = Inputs.Count - 1; i >= 0; i--)
                if (Inputs[i].Name == parameter.Name && Inputs[i].Pass == pass)
                    Inputs.RemoveAt(i);
            RunInput input = new(parameter.Name, prompt, RunRecord.KindOf(parameter)) { Pass = pass, Value = value };
            if (parameter is Double2By2Parameter table)
            {
                // a 2 by 2 table is held as four named counts: the record puts them back together with the labels of its rows and columns
                input.Parts = new[] { table.TopLeftName, table.TopRightName, table.BottomLeftName, table.BottomRightName };
                input.RowLabels = new[] { table.TopRowPrompt ?? "", table.BottomRowPrompt ?? "" };
                input.ColumnLabels = new[] { table.LeftColumnPrompt ?? "", table.RightColumnPrompt ?? "" };
                input.RowsTitle = Title(table.RowsPrompt);
                input.ColumnsTitle = Title(table.ColumnsPrompt);
            }
            Inputs.Add(input);
        }

        /// <summary>A dialog's prompt as a name for a table's rows or columns: without a trailing colon or an aside in brackets.</summary>
        private static string Title(string prompt)
        {
            string s = (prompt ?? "").Trim();
            int bracket = s.IndexOf('(');
            if (bracket > 0)
                s = s[..bracket].Trim();
            return s.TrimEnd(':').Trim();
        }
    }

    /// <summary>A parameter as it was asked: its name, the prompt the user saw, and the kind of value it takes.</summary>
    public sealed class RunInput
    {
        public string Name { get; }
        public string Prompt { get; }
        public string Kind { get; }
        /// <summary>For a 2 by 2 table, the names of its four counts (top left, top right, bottom left, bottom right) and the labels of its rows and columns.</summary>
        public string[] Parts { get; set; }
        public string[] RowLabels { get; set; }
        public string[] ColumnLabels { get; set; }
        /// <summary>What the rows and the columns of a 2 by 2 table stand for, from the dialog's prompts.</summary>
        public string RowsTitle { get; set; }
        public string ColumnsTitle { get; set; }
        /// <summary>The pass of the loop or loops the parameter was asked in ("" outside any loop, "0", "1" ... inside one, "1.0" ... in nested loops).</summary>
        public string Pass { get; set; } = "";
        /// <summary>The value as it was asked, when the processor had it to hand; the bag's current value stands in otherwise.</summary>
        public FilledParameter Value { get; set; }

        public RunInput(string name, string prompt, string kind)
        {
            Name = name;
            Prompt = prompt;
            Kind = kind;
        }
    }
}
