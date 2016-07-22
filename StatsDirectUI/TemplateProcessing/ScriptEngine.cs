using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;
using StatsDirect.R;
using StatsDirect.Utilities;

namespace StatsDirect.Templates
{
    public sealed class ScriptEngine : IScriptEngine
    {
        private const string CSHARP = "CSharp";
        private const string CHASH = "C#";
        private const string CHASHLOWER = "c#";
        private const string R = "R";
        private const string VISUALBASIC = "VB";
        private const string VISUALBASICLOWER = "vb";

        private class CompiledScript
        {
            public object Instance;
            public MethodInfo MethodInfo;
        };

        private static Dictionary<string, Dictionary<string, CompiledScript>> compiledScripts;

        public ScriptEngine()
        {
            if (null == compiledScripts)
                compiledScripts = new Dictionary<string, Dictionary<string, CompiledScript>>();
        }

        /// <summary>
        /// Runs the script's step entry point.  If it doesn't have one, throws an exception.
        /// </summary>
        /// <returns>Whatever the script returned</returns>
        object IScriptEngine.Run(string scriptLanguage, string code, ScriptType scriptType, ITemplateHost host, ParameterBag parameters, Parameter parameter, string entryPoint)
        {
            switch (scriptLanguage)
            {
                case CSHARP:
                case CHASH:
                case CHASHLOWER:
                case VISUALBASIC:
                case VISUALBASICLOWER:
                    return RunDotNet(scriptLanguage, code, scriptType, host, parameters, parameter, entryPoint);
                case R:
                    return RunR(host, code, parameters);
                default:
                    throw new ArgumentOutOfRangeException("scriptLanguage", scriptLanguage, "Must be CSharp, R, VB");
            }
        }

        public static bool CanHandle(string scriptLanguage)
        {
            switch (scriptLanguage)
            {
                case CSHARP:
                case CHASH:
                case CHASHLOWER:
                case VISUALBASIC:
                case VISUALBASICLOWER:
                case R:
                    return true;
                default:
                    return false;
            }
        }

        private object RunR(ITemplateHost host, string code, ParameterBag parameters)
        {
            StringBuilder sb = new StringBuilder();
            if (null != parameters)
            {
                foreach (KeyValuePair<string, FilledParameter> pair in parameters.Pairs)
                {
                    RConvert.ToR(sb, pair.Key, pair.Value, FrameType.Wide);
                }
            }
            sb.AppendLine(code);
            string modifiedCode = sb.ToString();
            host.StartProgress("Running R script", false);
            try
            {
                string codeForEmit;
                Process p = RController.RunScriptAndQuit(host, modifiedCode, out codeForEmit);
                while (true)
                {
                    bool exited = p.WaitForExit(50);
                    if (exited)
                        break;
                    if (host.UpdateProgress(0))
                    {
                        p.Kill();
                        throw new TemplateOperationCancelledException();
                    }
                }
                int exitCode = p.ExitCode;
                if (0 != exitCode)
                    throw new Exception(RController.GetErrorText() ?? "R did not complete successfully and did not save an error message");
                else
                {
                    ParameterBag pb = RController.FilesToParameterBag();
                    pb.AddOutput("formattedRScript", codeForEmit);
                    return pb;
                }
            }
            finally
            {
                host.FinishProgress();
            }
        }

        /// <summary>
        /// Runs the script's step entry point.  If it doesn't have one, throws an exception.
        /// </summary>
        /// <returns></returns>
        private object RunDotNet(string scriptLanguage, string code, ScriptType scriptType, ITemplateHost host, ParameterBag parameters, Parameter parameter, string entryPoint)
        {
            CompiledScript compiledScript = null;
            // Look aside to the cache - do we already have this one?
            if (compiledScripts.ContainsKey(code))
            {
                Dictionary<string, CompiledScript> codeDic = compiledScripts[code];
                if (codeDic.ContainsKey(scriptLanguage))
                    compiledScript = codeDic[scriptLanguage];
            }

            if (null == compiledScript)
            {
                compiledScript = CompileDotNet(scriptLanguage, code, scriptType, entryPoint);
                if (!compiledScripts.ContainsKey(code))
                    compiledScripts.Add(code, new Dictionary<string, CompiledScript>());
                compiledScripts[code][scriptLanguage] = compiledScript;
            }
            try
            {
                object[] invokeParameters;
                switch (scriptType)
                {
                    case ScriptType.Validator:
                        invokeParameters = new object[] { host, parameters, parameter };
                        break;
                    default:
                        invokeParameters = new object[] { host, parameters };
                        break;
                }
                return compiledScript.MethodInfo.Invoke(compiledScript.Instance, invokeParameters);
            }
            catch (TargetInvocationException tie)
            {
                if (null != tie.InnerException)
                    throw tie.InnerException;
                throw;
            }
        }

        /// <summary>
        /// Compile the script.  Throw an exception if the compile fails.
        /// </summary>
        /// <returns></returns>
        private static CompiledScript CompileDotNet(string scriptLanguage, string code, ScriptType scriptType, string entryPoint)
        {
            // Build up the source in sourceBuilder
            StringBuilder sourceBuilder = new StringBuilder();
            CodeDomProvider codeProvider;
            switch (scriptLanguage)
            {
                case CSHARP:
                case CHASH:
                case CHASHLOWER:
                    sourceBuilder.AppendLine("using System;");
                    sourceBuilder.AppendLine("using System.Collections;");
                    sourceBuilder.AppendLine("using System.Collections.Generic;");
                    sourceBuilder.AppendLine("using System.Globalization;");
                    sourceBuilder.AppendLine("using System.Linq;");
                    sourceBuilder.AppendLine("using System.Text;");
                    sourceBuilder.AppendLine("using StatsDirect.Builtins;");
                    sourceBuilder.AppendLine("using StatsDirect.Data;");
                    sourceBuilder.AppendLine("using StatsDirect.Numerics;");
                    sourceBuilder.AppendLine("using StatsDirect.R;");
                    sourceBuilder.AppendLine("using StatsDirect.Templates;");
                    sourceBuilder.AppendLine("using StatsDirect.Utilities;");
                    sourceBuilder.AppendLine("namespace StatsDirect.Templates {");
                    sourceBuilder.AppendLine("public class Temp1 {");
                    switch (scriptType)
                    {
                        case ScriptType.Expression:
                            sourceBuilder.AppendLine("public object DoIt(ITemplateHost host, ParameterBag parameters) {");
                            sourceBuilder.AppendLine("return " + code + ";");
                            sourceBuilder.AppendLine("} // DoIt");
                            entryPoint = "DoIt";
                            break;
                        case ScriptType.Function:
                            sourceBuilder.AppendLine("public object DoIt(ITemplateHost host, ParameterBag parameters) {");
                            sourceBuilder.AppendLine(code);
                            sourceBuilder.AppendLine("} // DoIt");
                            entryPoint = "DoIt";
                            break;
                        case ScriptType.Method:
                            sourceBuilder.AppendLine("public void DoIt(ITemplateHost host, ParameterBag parameters) {");
                            sourceBuilder.AppendLine(code);
                            sourceBuilder.AppendLine("} // DoIt");
                            entryPoint = "DoIt";
                            break;
                        case ScriptType.MultipleMethods:
                            sourceBuilder.AppendLine(code);
                            break;
                        case ScriptType.Validator:
                            sourceBuilder.AppendLine("public object DoIt(ITemplateHost host, ParameterBag parameters, Parameter parameter) {");
                            sourceBuilder.AppendLine(code);
                            sourceBuilder.AppendLine("} // DoIt");
                            entryPoint = "DoIt";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("scriptType", scriptType, "Cannot construct code for that script type");
                    }
                    sourceBuilder.AppendLine("} // class");
                    sourceBuilder.AppendLine("} // namespace");
                    codeProvider = new Microsoft.CSharp.CSharpCodeProvider();
                    break;
                case VISUALBASIC:
                case VISUALBASICLOWER:
                    sourceBuilder.AppendLine("Imports Microsoft.VisualBasic");
                    sourceBuilder.AppendLine("Imports System");
                    sourceBuilder.AppendLine("Imports System.Collections");
                    sourceBuilder.AppendLine("Imports System.Collections.Generic");
                    sourceBuilder.AppendLine("Imports System.Globalization");
                    sourceBuilder.AppendLine("Imports System.Linq");
                    sourceBuilder.AppendLine("Imports System.Text");
                    sourceBuilder.AppendLine("Imports StatsDirect.Builtins");
                    sourceBuilder.AppendLine("Imports StatsDirect.Data");
                    sourceBuilder.AppendLine("Imports StatsDirect.Expressions");
                    sourceBuilder.AppendLine("Imports StatsDirect.Numerics");
                    sourceBuilder.AppendLine("Imports StatsDirect.R");
                    sourceBuilder.AppendLine("Imports StatsDirect.Templates");
                    sourceBuilder.AppendLine("Imports StatsDirect.Utilities");
                    sourceBuilder.AppendLine("Namespace StatsDirect.Templates");
                    sourceBuilder.AppendLine("Public Class Temp1");
                    switch (scriptType)
                    {
                        case ScriptType.Expression:
                            sourceBuilder.AppendLine("Public Function DoIt(ByVal host As ITemplateHost, ByVal parameters As ParameterBag) As Object");
                            sourceBuilder.AppendLine("Return " + code);
                            sourceBuilder.AppendLine("End Function");
                            entryPoint = "DoIt";
                            break;
                        case ScriptType.Function:
                            sourceBuilder.AppendLine("Public Function DoIt(ByVal host As ITemplateHost, ByVal parameters As ParameterBag) As Object");
                            sourceBuilder.AppendLine(code);
                            sourceBuilder.AppendLine("End Function");
                            entryPoint = "DoIt";
                            break;
                        case ScriptType.Method:
                            sourceBuilder.AppendLine("Public Sub DoIt(ByVal host As ITemplateHost, ByVal parameters As ParameterBag)");
                            sourceBuilder.AppendLine(code);
                            sourceBuilder.AppendLine("End Sub");
                            entryPoint = "DoIt";
                            break;
                        case ScriptType.MultipleMethods:
                            sourceBuilder.AppendLine(code);
                            break;
                        case ScriptType.Validator:
                            sourceBuilder.AppendLine("Public Sub DoIt(ByVal host As ITemplateHost, ByVal parameters As ParameterBag, ByVal parameter as Parameter)");
                            sourceBuilder.AppendLine(code);
                            sourceBuilder.AppendLine("End Sub");
                            entryPoint = "DoIt";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("scriptType", scriptType, "Cannot construct code for that script type");
                    }
                    sourceBuilder.AppendLine("End Class");
                    sourceBuilder.AppendLine("End Namespace");
                    codeProvider = new Microsoft.VisualBasic.VBCodeProvider();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("scriptLanguage", scriptLanguage, "Only CSharp and VB are known");
            }
            const string typeName = "StatsDirect.Templates.Temp1";
            CompilerParameters compilerParameters = new CompilerParameters();
            string mainModulePath = Process.GetCurrentProcess().MainModule.FileName;
            string assemblyPath = Path.GetDirectoryName(mainModulePath);
            Debug.Assert(null != assemblyPath);
            compilerParameters.ReferencedAssemblies.Add(Path.Combine(assemblyPath, "StatsDirect.exe"));
            compilerParameters.ReferencedAssemblies.Add("System.Windows.Forms.dll");
            compilerParameters.ReferencedAssemblies.Add("System.Drawing.dll");
            compilerParameters.ReferencedAssemblies.Add("System.Core.dll");
            compilerParameters.GenerateInMemory = true;
            CompilerResults compilerResults = codeProvider.CompileAssemblyFromSource(compilerParameters, sourceBuilder.ToString());
            codeProvider.Dispose();
            if (compilerResults.Errors.Count > 0)
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
                throw new Exception(sb.ToString());
            }
            // No compile errors - prepare and return it.
            Assembly assembly = compilerResults.CompiledAssembly;
            object instance = assembly.CreateInstance(typeName);
            Debug.Assert(null != instance);
            Type type = instance.GetType();
            MethodInfo methodInfo = type.GetMethod(entryPoint);
            compilerResults.TempFiles.Delete();
            return new CompiledScript { Instance = instance, MethodInfo = methodInfo };
        }

        public string Check(string scriptLanguage, string code, ScriptType scriptType, string entryPoint)
        {
            try
            {
                switch (scriptLanguage)
                {
                    case CSHARP:
                    case CHASH:
                    case CHASHLOWER:
                    case VISUALBASIC:
                    case VISUALBASICLOWER:
                        CompileDotNet(scriptLanguage, code, scriptType, entryPoint);
                        return null;
                    case R:
                        // No way of detecting errors at present
                        return null;
                    default:
                        throw new ArgumentOutOfRangeException("scriptLanguage", scriptLanguage, "Must be CSharp, R, VB");
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
