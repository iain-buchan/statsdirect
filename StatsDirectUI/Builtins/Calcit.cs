using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CSharp;
using StatsDirect.Expressions;

namespace StatsDirect.Builtins
{
    public class Calcit  
    {
        private object instance;
        private MethodInfo methodInfo;
        public DataType OutputType { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="equation"></param>
        /// <param name="passedVariableTypes"></param>
        /// <param name="assumeVariants">If true, </param>
        public Calcit(string equation, DataType[] passedVariableTypes, bool assumeVariants)
        {
            OutputType = SetEquation(equation, passedVariableTypes, assumeVariants, out bool _);
        }

        private DataType SetEquation(string equation, DataType[] passedVariableTypes, bool assumeVariants, out bool compiledForVariants)
        {
            const string typeName = "Temp1";
            const string methodName = "DoIt";
            bool allDoubles = passedVariableTypes.Aggregate(true, (okSoFar, dt) => okSoFar && dt == DataType.Double);
            compiledForVariants = assumeVariants || !allDoubles;
            string cSharpExpression = Converter.ConvertToCSharp(equation, passedVariableTypes, compiledForVariants, out DataType retval);

            // By now, cSharpExpression will either be safe (every character has been through the parser) or an exception will have been thrown.  Therefore, it's reasonable to throw the expression at the compiler.
            StringBuilder functionBuilder = new StringBuilder();
            functionBuilder.AppendLine("using System;");
            functionBuilder.AppendLine("using StatsDirect.Expressions;");
            functionBuilder.Append("public class ");
            functionBuilder.AppendLine(typeName);
            functionBuilder.AppendLine("{");
            functionBuilder.Append("public object ");
            functionBuilder.Append(methodName);
            functionBuilder.Append("(");
            functionBuilder.Append(compiledForVariants ? "object" : "double");
            functionBuilder.AppendLine("[] x)");
            functionBuilder.AppendLine("{");
            functionBuilder.Append("return ");
            functionBuilder.Append(cSharpExpression);
            functionBuilder.AppendLine(";");
            functionBuilder.AppendLine("}");
            functionBuilder.AppendLine("}");
            string cSharpFunction = functionBuilder.ToString();
            CompilerParameters compilerParameters = new CompilerParameters();
            string mainModulePath = Process.GetCurrentProcess().MainModule.FileName;
            string assemblyPath = Path.GetDirectoryName(mainModulePath);
            Debug.Assert(null != assemblyPath);
            compilerParameters.ReferencedAssemblies.Add(Path.Combine(assemblyPath, "StatsDirect.exe"));
            compilerParameters.GenerateInMemory = true;
            using (CodeDomProvider codeProvider = new CSharpCodeProvider())
            {
                CompilerResults compilerResults = codeProvider.CompileAssemblyFromSource(compilerParameters, cSharpFunction);
                if (compilerResults.Errors.HasErrors)
                {
                    StringBuilder errorBuilder = new StringBuilder();
                    errorBuilder.AppendLine("Errors in compilation:");
                    foreach (CompilerError error in compilerResults.Errors)
                    {
                        errorBuilder.Append("line ");
                        errorBuilder.Append(error.Line);
                        errorBuilder.Append(": ");
                        errorBuilder.Append(error.IsWarning ? "warning " : "error ");
                        errorBuilder.Append(error.ErrorNumber);
                        errorBuilder.Append(": ");
                        errorBuilder.AppendLine(error.ErrorText);
                    }
                    compilerResults.TempFiles.Delete();
                    throw new Exception("Couldn't translate your expression to valid C# code:" + Environment.NewLine + errorBuilder);
                }
                // No compile errors - save and prepare to run it!
                Assembly assembly = compilerResults.CompiledAssembly;
                instance = assembly.CreateInstance(typeName);
                Debug.Assert(null != instance);
                Type type = instance.GetType();
                methodInfo = type.GetMethod(methodName);
                compilerResults.TempFiles.Delete();
                // By now, compiledScript is non-null or an exception has been thrown
            }
            return retval;
        }

        public T Evaluate<T>(double[] values)
        {
            try
            {
                object[] parameters = { values};
                object output = methodInfo.Invoke(instance, parameters);
                return (T)Convert.ChangeType(output, typeof(T));
            }
            catch (TargetInvocationException tie)
            {
                if (null != tie.InnerException)
                    throw tie.InnerException;
                throw;
            }
        }

        public T EvaluateObject<T>(object[] values)
        {
            try
            {
                object[] parameters = { values };
                return (T)methodInfo.Invoke(instance, parameters);
            }
            catch (TargetInvocationException tie)
            {
                if (null != tie.InnerException)
                    throw tie.InnerException;
                throw;
            }
        }
    }
} 
