using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.CodeDom.Compiler;
using System.IO;
#if USE_R
using org.rosuda.REngine;
#endif
using System.Reflection;

namespace StatsDirect.Templates
{
    public sealed class ScriptEngine : IScriptEngine
    {
        private const string CSHARP="CSharp";
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
#if USE_R
        private static org.rosuda.REngine.Rserve.RConnection rConnection;
#endif

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
#if USE_R

                    return RunR(code, parameters);
#else
                    throw new Exception("R is not available on this build of StatsDirect");
#endif
                default:
                    throw new ArgumentOutOfRangeException("scriptLanguage", scriptLanguage, "Must be CSharp, R, VB");
            }
        }

#if USE_R

        private object RunR(string code, ParameterBag parameters)
        {
            if (null == rConnection)
            {
                if (!StartRserve.checkLocalRserve())
                {
                    // TODO: Install R
                    throw new Exception("R appears not to be running and I cannot find a way of starting it. Please start Rserve manually.");
                }
                rConnection = new org.rosuda.REngine.Rserve.RConnection();
            }
            if (null != parameters)
            {
                foreach (KeyValuePair<string, FilledParameter> pair in parameters.Pairs)
                {
                    ToR(pair.Key, pair.Value);
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("tryCatch( { ");
            sb.Append(code);
            sb.Append(" }, error=function(e) { capture.output(print(e)) })");
            string modifiedCode = sb.ToString();
            REXP rExp = rConnection.eval(modifiedCode);
            return Flatten(rExp);
        }

        private void ToR(string name, Variable variable)
        {
            REXP rExp;
            switch (variable.VariableType)
            {
                case VariableType.ClassifierType:
                    // TODO: Factor
                    double[] data = variable.AsDoubleVariable.Data;
                    rExp = new REXPDouble(data);
                    break;
                case VariableType.DoubleType:
                    rExp = new REXPDouble(variable.AsDoubleVariable.Data);
                    break;
                case VariableType.StringType:
                    rExp = new REXPString(variable.AsStringVariable.Data);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("variable", variable.VariableType, "Unknown VariableType");
            }
            rConnection.assign(name, rExp);
        }

        private void ToR(string name, FilledParameter filledParameter)
        {
            if (null == filledParameter || !filledParameter.HasData)
            {
                rConnection.assign(name, new REXPNull());
                return;
            }
            if (filledParameter.IsDataFrame)
            {
                DataFrame frame = filledParameter.AsDataFrame;
                List<string> variableNames = new List<string>();
                for (int i = 0; i < frame.VariableCount; i++)
                {
                    string varName = name + "VAR" + i.ToString();
                    ToR(varName, frame.Variables[i]);
                    variableNames.Add(varName);
                }
                string cmd = name + " <- data.frame(";
                cmd += string.Join(", ", variableNames.ToArray());
                cmd += ")";
                rConnection.voidEval(cmd);
            }
            else if (filledParameter.IsString)
            {
                rConnection.assign(name, new REXPString(filledParameter.AsString));
            }
            else if (filledParameter.IsDouble)
            {
                rConnection.assign(name, new REXPDouble(filledParameter.AsDouble));
            }
            else if (filledParameter.IsInt32)
            {
                rConnection.assign(name, new REXPInteger(filledParameter.AsInt32));
            }
            else if (filledParameter.IsBoolean)
            {
                rConnection.assign(name, new REXPLogical(new[] { filledParameter.AsBoolean }, null));
            }
        }

        private ParameterBag Flatten(REXP rExp)
        {
            ParameterBag output = new ParameterBag();
            object flattened;
            if (rExp.String)
            {
                if (rExp.length() > 1)
                {
                    // TODO: HACK: How to represent strings?  Lists?
                    REXPString s = (REXPString)rExp;
                    string[] strings = s.asStrings();
                    flattened = string.Join("\n\\par ", strings);
                }
                else
                {
                    flattened = rExp.asString();
                }
            }
            else if (rExp.Integer)
            {
                flattened = rExp.asInteger();
            }
            else if (rExp.Numeric)
            {
                flattened = rExp.asDouble();
            }
            else
            {
                flattened = rExp.toDebugString();
            }
            output.AddOutput("output", flattened);
            return output;
        }
#endif

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
                {
                    compiledScript = codeDic[scriptLanguage];
                }
            }

            if (null == compiledScript)
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
                        sourceBuilder.AppendLine("using System.Text;");
                        sourceBuilder.AppendLine("using System.Collections;");
                        sourceBuilder.AppendLine("using System.Collections.Generic;");
                        sourceBuilder.AppendLine("using StatsDirect.Templates;");
                        sourceBuilder.AppendLine("using StatsDirect.Data;");
                        sourceBuilder.AppendLine("using StatsDirect.Numerics;");
                        sourceBuilder.AppendLine("using StatsDirect.Builtins;");
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
                        sourceBuilder.AppendLine("Imports System");
                        sourceBuilder.AppendLine("Imports System.Text");
                        sourceBuilder.AppendLine("Imports System.Collections");
                        sourceBuilder.AppendLine("Imports System.Collections.Generic");
                        sourceBuilder.AppendLine("Imports Microsoft.VisualBasic");
                        sourceBuilder.AppendLine("Imports StatsDirect.Templates");
                        sourceBuilder.AppendLine("Imports StatsDirect.Data");
                        sourceBuilder.AppendLine("Imports StatsDirect.Numerics");
                        sourceBuilder.AppendLine("Imports StatsDirect.Builtins");
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
                // No compile errors - save and prepare to run it!
                Assembly assembly = compilerResults.CompiledAssembly;
                object instance = assembly.CreateInstance(typeName);
                Debug.Assert(null != instance);
                Type type = instance.GetType();
                MethodInfo methodInfo = type.GetMethod(entryPoint);
                compiledScript = new CompiledScript {Instance = instance, MethodInfo = methodInfo};
                if (!compiledScripts.ContainsKey(code))
                    compiledScripts.Add(code, new Dictionary<string, CompiledScript>());
                compiledScripts[code][scriptLanguage] = compiledScript;
                compilerResults.TempFiles.Delete();
            }

            // By now, compiledScript is non-null or an exception has been thrown
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
    }
}
