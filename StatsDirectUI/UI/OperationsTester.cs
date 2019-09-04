using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    /// <summary>
    /// Test runner for operations with tests
    /// </summary>
    [TestClass]
    internal class OperationsTester
    {
        [TestMethod]
        public static void TestAll()
        {
            SdApplication.InitialiseFunctionRegistry();
            foreach (Operation operation in TemplateFactory.Operations.Values)
                TestOperation(operation);
        }

        private static void TestOperation(Operation operation)
        {
            if (0 == operation.Tests.Count)
                return;
            foreach (OperationTest test in operation.Tests)
                TestOperation(operation, test);
        }

        private static void TestOperation(Operation operation, OperationTest test)
        {
            // Dispose of empty tests
            if (test.Inputs.Count == 0 && test.Outputs.Count == 0)
                return;
            ITemplateProcessor templateProcessor = new TemplateProcessor(new OperationTestHost(test.Inputs));
            ParameterBag outputParameters = templateProcessor.Execute(operation, new ParameterBag(), false);
            VerifyOutputs(operation, outputParameters, test.Outputs);
        }

        private static void VerifyOutputs(Operation operation, ParameterBag outputParameters, IList<OperationTestOutputParameter> outputs)
        {
            if (null == outputParameters)
                throw new Exception($"Operation {operation.Name} failed: no output parameter bag");
            throw new NotImplementedException();
        }
    }
}