using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace StatsDirect.Builtins
{
    public class Calcit  
    {
        //private static readonly string THOUSANDS_SEPARATOR = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
        private object instance;
        private MethodInfo methodInfo;

        public Calcit(string equation)
        {
            SetEquation(equation);
        }

        //public static bool ExpressionOk( ref string x ) 
        //{
        //    if (string.IsNullOrEmpty(x))
        //        return false;
        //    return !x.Contains( THOUSANDS_SEPARATOR ); 
        //}


        private void SetEquation(string equation)
        {
            const string typeName = "Temp1";
            const string methodName = "DoIt";
            string cSharpExpression = Expressions.Converter.ConvertToCSharp(equation);
            string cSharpFunction =
                "using System; using StatsDirect.Expressions; public class " + typeName + " { public object " + methodName + "(double[] x) { return " + cSharpExpression + "; } }";
            CompilerParameters compilerParameters = new CompilerParameters();
            string mainModulePath = Process.GetCurrentProcess().MainModule.FileName;
            string assemblyPath = Path.GetDirectoryName(mainModulePath);
            Debug.Assert(null != assemblyPath);
            compilerParameters.ReferencedAssemblies.Add(Path.Combine(assemblyPath, "StatsDirect.exe"));
            compilerParameters.GenerateInMemory = true;
            CodeDomProvider codeProvider = new Microsoft.CSharp.CSharpCodeProvider();
            CompilerResults compilerResults = codeProvider.CompileAssemblyFromSource(compilerParameters, cSharpFunction);
            codeProvider.Dispose();
            if (compilerResults.Errors.HasErrors)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Errors in compilation:");
                foreach (CompilerError error in compilerResults.Errors)
                {
                    sb.Append("line ");
                    sb.Append(error.Line);
                    sb.Append(": ");
                    sb.Append(error.IsWarning ? "warning " : "error ");
                    sb.Append(error.ErrorNumber);
                    sb.Append(": ");
                    sb.AppendLine(error.ErrorText);
                }
                compilerResults.TempFiles.Delete();
                throw new Exception("Couldn't translate your expression to valid C# code:" + Environment.NewLine + sb.ToString());
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

        public double Evaluate(double[] values)
        {
            try
            {
                object[] parameters = new object[] { values};
                object output = methodInfo.Invoke(instance, parameters);
                return Convert.ToDouble(output);
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
