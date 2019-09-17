using StatsDirect.CsvParser;
using StatsDirect.Data;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StatsDirect.TemplateProcessing
{
    /// <summary>
    /// Turn a ParameterBag into a list of OperationTestInputParameters suitable for using as inputs to a test.
    /// </summary>
    public static class OperationTestRenderer
    {
        public static OperationTest Render(ParameterBag parameters)
        {
            return new OperationTest
            {
                Inputs = RenderAsInputs(parameters),
                Outputs = RenderAsOutputs(parameters)
            };
        }

        private static IList<OperationTestInputParameter> RenderAsInputs(ParameterBag parameters)
        {
            return parameters
                .Where(parameter => null != parameter.Value && (parameter.Value.Direction == FilledParameterDirection.Default || parameter.Value.Direction == FilledParameterDirection.Input))
                .SelectMany(parameter => InputVisitor.Render(parameter.Key, parameter.Value))
                .ToList();
        }

        private static IList<OperationTestOutputParameter> RenderAsOutputs(ParameterBag parameters)
        {
            return parameters
                .Where(parameter => null != parameter.Value && parameter.Value.Direction == FilledParameterDirection.Output)
                .SelectMany(parameter => OutputVisitor.Render(parameter.Key, parameter.Value))
                .ToList();
        }

        private class InputVisitor : IFilledParameterVisitor
        {
            private readonly IList<OperationTestInputParameter> inputs = new List<OperationTestInputParameter>();
            private string NameOrPrefix { get; }

            public static IList<OperationTestInputParameter> Render(string nameOrPrefix, FilledParameter victim)
            {
                InputVisitor inputVisitor = new InputVisitor(nameOrPrefix);
                if (null != victim && victim.HasData)
                    victim.Accept(inputVisitor);
                return inputVisitor.inputs;
            }

            private InputVisitor(string nameOrPrefix)
            {
                NameOrPrefix = nameOrPrefix;
            }

            void IFilledParameterVisitor.Visit(FilledBooleanParameter victim)
            {
                inputs.Add(new OperationTestInputParameter { Name = NameOrPrefix, Value = victim.Data.ToString() });
            }

            void IFilledParameterVisitor.Visit(FilledChartDefinitionParameter victim)
            {
                // Do nothing - we're never interested in this
            }

            void IFilledParameterVisitor.Visit(FilledDataFrameParameter victim)
            {
                if (!victim.HasData)
                    return;

                DataFrame frame = victim.Data;
                List<string> header = frame.Variables
                    .Select(variable => string.IsNullOrWhiteSpace(variable.Title) ? string.Empty : variable.Title)
                    .ToList();
                List<IList<string>> rows = new List<IList<string>>();
                for (int rowIndex = 0; rowIndex < frame.MaxRows; rowIndex++)
                    rows.Add(frame.Variables
                        .Select(variable => rowIndex < variable.Length ? variable.DataAsObject(rowIndex).ToString() : string.Empty)
                        .ToList());

                inputs.Add(new OperationTestInputParameter { Name = NameOrPrefix, Value = new CsvRenderer().Render(header, rows) });
            }

            void IFilledParameterVisitor.Visit(FilledDoubleParameter victim)
            {
                // Microsoft recommends using G17 for double rather than R ("Round-trip") as bugs in R can prevent a successful round-trip.
                // See https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#RFormatString retrieved 2019-09-17.
                inputs.Add(new OperationTestInputParameter { Name = NameOrPrefix, Value = victim.Data.ToString("G17", CultureInfo.InvariantCulture) });
            }

            void IFilledParameterVisitor.Visit(FilledInt32Parameter victim)
            {
                inputs.Add(new OperationTestInputParameter { Name = NameOrPrefix, Value = victim.Data.ToString(CultureInfo.InvariantCulture) });
            }

            void IFilledParameterVisitor.Visit(FilledObjectParameter victim)
            {
                throw new NotImplementedException();
            }

            void IFilledParameterVisitor.Visit(FilledPaneParameter victim)
            {
                // Do nothing - we're never interested in this
            }

            void IFilledParameterVisitor.Visit(FilledPaneAndPositionParameter victim)
            {
                // Do nothing - we're never interested in this
            }

            void IFilledParameterVisitor.Visit(FilledParameterBagParameter victim)
            {
                foreach (KeyValuePair<string, FilledParameter> innerPair in victim.Data)
                {
                    string innerPrefix = $"{NameOrPrefix}${innerPair.Key}";
                    foreach (var x in Render(innerPrefix, innerPair.Value))
                        inputs.Add(x);
                }
            }

            void IFilledParameterVisitor.Visit(FilledParameterBagListParameter victim)
            {
                IList<ParameterBag> data = victim.Data;
                for (int i = 0; i < data.Count; i++)
                {
                    ParameterBag innerBag = data[i];
                    string innerPrefix = $"{NameOrPrefix}${i + 1}";
                    foreach (OperationTestInputParameter x in Render(innerPrefix, FilledParameterFactory.Input(innerBag)))
                        inputs.Add(x);
                }
            }

            void IFilledParameterVisitor.Visit(FilledStringParameter victim)
            {
                inputs.Add(new OperationTestInputParameter { Name = NameOrPrefix, Value = string.IsNullOrEmpty(victim.Data) ? string.Empty : victim.Data });
            }

            void IFilledParameterVisitor.Visit(FilledStringListParameter victim)
            {
                if (victim.HasData)
                    inputs.Add(new OperationTestInputParameter { Name = NameOrPrefix, Value = new CsvRenderer().Render(string.Empty, victim.Data) });
            }
        }

        private class OutputVisitor : IFilledParameterVisitor
        {
            private readonly IList<OperationTestOutputParameter> outputs = new List<OperationTestOutputParameter>();
            private string NameOrPrefix { get; }

            public static IList<OperationTestOutputParameter> Render(string nameOrPrefix, FilledParameter victim)
            {
                OutputVisitor outputVisitor = new OutputVisitor(nameOrPrefix);
                if (null != victim && victim.HasData)
                    victim.Accept(outputVisitor);
                return outputVisitor.outputs;
            }

            private OutputVisitor(string nameOrPrefix)
            {
                NameOrPrefix = nameOrPrefix;
            }

            void IFilledParameterVisitor.Visit(FilledBooleanParameter victim)
            {
                outputs.Add(new OperationTestOutputParameter { Name = NameOrPrefix, Value = victim.Data.ToString() });
            }

            void IFilledParameterVisitor.Visit(FilledChartDefinitionParameter victim)
            {
                // Do nothing - we're never interested in this
            }

            void IFilledParameterVisitor.Visit(FilledDataFrameParameter victim)
            {
                if (!victim.HasData)
                    return;

                DataFrame frame = victim.Data;
                List<string> header = frame.Variables
                    .Select(variable => string.IsNullOrWhiteSpace(variable.Title) ? string.Empty : variable.Title)
                    .ToList();
                List<IList<string>> rows = new List<IList<string>>();
                for (int rowIndex = 0; rowIndex < frame.MaxRows; rowIndex++)
                    rows.Add(frame.Variables
                        .Select(variable => rowIndex < variable.Length ? variable.DataAsObject(rowIndex).ToString() : string.Empty)
                        .ToList());
                outputs.Add(new OperationTestOutputParameter { Name = NameOrPrefix, Value = new CsvRenderer().Render(header, rows) });
            }

            void IFilledParameterVisitor.Visit(FilledDoubleParameter victim)
            {
                // Microsoft recommends using G17 for double rather than R ("Round-trip") as bugs in R can prevent a successful round-trip.
                // See https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#RFormatString retrieved 2019-09-17.
                outputs.Add(new OperationTestOutputParameter { Name = NameOrPrefix, Value = victim.Data.ToString("G17", CultureInfo.InvariantCulture) });
            }

            void IFilledParameterVisitor.Visit(FilledInt32Parameter victim)
            {
                outputs.Add(new OperationTestOutputParameter { Name = NameOrPrefix, Value = victim.Data.ToString(CultureInfo.InvariantCulture) });
            }

            void IFilledParameterVisitor.Visit(FilledObjectParameter victim)
            {
                throw new NotImplementedException();
            }

            void IFilledParameterVisitor.Visit(FilledPaneParameter victim)
            {
                // Do nothing - we're never interested in this
            }

            void IFilledParameterVisitor.Visit(FilledPaneAndPositionParameter victim)
            {
                // Do nothing - we're never interested in this
            }

            void IFilledParameterVisitor.Visit(FilledParameterBagParameter victim)
            {
                foreach (KeyValuePair<string, FilledParameter> innerPair in victim.Data)
                {
                    string innerPrefix = $"{NameOrPrefix}${innerPair.Key}";
                    foreach (var x in Render(innerPrefix, innerPair.Value))
                        outputs.Add(x);
                }
            }

            void IFilledParameterVisitor.Visit(FilledParameterBagListParameter victim)
            {
                IList<ParameterBag> data = victim.Data;
                for (int i = 0; i < data.Count; i++)
                {
                    ParameterBag innerBag = data[i];
                    string innerPrefix = $"{NameOrPrefix}${i + 1}";
                    foreach (OperationTestOutputParameter x in Render(innerPrefix, FilledParameterFactory.Input(innerBag)))
                        outputs.Add(x);
                }
            }

            void IFilledParameterVisitor.Visit(FilledStringParameter victim)
            {
                outputs.Add(new OperationTestOutputParameter { Name = NameOrPrefix, Value = string.IsNullOrEmpty(victim.Data) ? string.Empty : victim.Data });
            }

            void IFilledParameterVisitor.Visit(FilledStringListParameter victim)
            {
                if (victim.HasData)
                    outputs.Add(new OperationTestOutputParameter { Name = NameOrPrefix, Value = new CsvRenderer().Render(string.Empty, victim.Data) });
            }
        }
    }
}
