using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsDirect.TemplateProcessing
{
    /// <summary>
    /// Turn a ParameterBag into a list of OperationTestInputParameters suitable for using as inputs to a test.
    /// </summary>
    public static class OperationTestInputsRenderer
    {
        public static IList<OperationTestInputParameter> RenderAsInputs(ParameterBag parameters)
        {
            return parameters
                .Select(parameter => new OperationTestInputParameter { Name = parameter.Key, Value = Render(parameter.Value) })
                .ToList();
        }

        private static string Render(FilledParameter parameter)
        {
            if (null == parameter || !parameter.HasData)
                return string.Empty;
            throw new NotImplementedException();
        }
    }
}
