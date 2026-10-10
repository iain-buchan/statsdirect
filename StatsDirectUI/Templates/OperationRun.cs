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
        /// <summary>The parameters as they were asked, in order; a parameter asked again replaces its earlier note.</summary>
        public IList<RunInput> Inputs { get; } = new List<RunInput>();
        /// <summary>The JSON record of the run as it stood at the last report step, or null when none has been written.</summary>
        public string Record { get; set; }

        public OperationRun(Operation operation)
        {
            Operation = operation;
        }

        public void Note(Parameter parameter, string prompt)
        {
            if (string.IsNullOrEmpty(parameter?.Name))
                return;
            for (int i = Inputs.Count - 1; i >= 0; i--)
                if (Inputs[i].Name == parameter.Name)
                    Inputs.RemoveAt(i);
            RunInput input = new(parameter.Name, prompt, RunRecord.KindOf(parameter));
            if (parameter is Double2By2Parameter table)
            {
                // a 2 by 2 table is held as four named counts: the record puts them back together with the labels of its rows and columns
                input.Parts = new[] { table.TopLeftName, table.TopRightName, table.BottomLeftName, table.BottomRightName };
                input.RowLabels = new[] { table.TopRowPrompt ?? "", table.BottomRowPrompt ?? "" };
                input.ColumnLabels = new[] { table.LeftColumnPrompt ?? "", table.RightColumnPrompt ?? "" };
            }
            Inputs.Add(input);
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

        public RunInput(string name, string prompt, string kind)
        {
            Name = name;
            Prompt = prompt;
            Kind = kind;
        }
    }
}
