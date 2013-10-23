using Microsoft.Win32;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace StatsDirect.R
{
    public static class RConvert
    {
        public static string ToRVariableName(string rawVariableName)
        {
            StringBuilder sb = new StringBuilder();
            ToRName(sb, rawVariableName);
            return sb.ToString();
        }

        public static void ToRName(StringBuilder sb, string rawVariableName)
        {
            string[] reservedWords = new string[] { "if", "else", "repeat", "while", "function", "for", "in", "next", "break", "TRUE", "FALSE", "NULL", "Inf", "NaN", "NA", "NA_integer_", "NA_real_", "NA_complex_", "NA_character_" };
            List<string> rw = new List<string>(reservedWords);
            string lowerName = rawVariableName.ToLower();
            if (rw.Contains(lowerName))
            {
                sb.Append('.');
                sb.Append(lowerName);
            }
            else
            {
                // Detect probable illegal starts to names and fix them by prefixing a dot
                if (lowerName[0] < 'a' || lowerName[0] > 'z')
                    sb.Append(".");
                if (lowerName[0] >= '0' && lowerName[0] <= '9')
                    sb.Append("."); // A second dot otherwise we run the danger of a name of the form .2way, which is illegal.
                foreach (char ch in lowerName)
                {
                    if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '_')
                        sb.Append(ch);
                    else
                        sb.Append('.');
                }
            }
        }

        public static void ToR(StringBuilder sb, string name, FilledParameter filledParameter)
        {
            if (filledParameter.IsDataFrame)
            {
                DataFrame frame = filledParameter.AsDataFrame;
                ToR(sb, name, frame);
            }
            else
            {
                ToRName(sb, name);
                sb.Append(" <- ");
                if (null == filledParameter || !filledParameter.HasData)
                {
                    sb.Append("NULL");
                }
                else if (filledParameter.IsString)
                {
                    ToR(sb, filledParameter.AsString);
                }
                else if (filledParameter.IsDouble)
                {
                    ToR(sb, filledParameter.AsDouble);
                }
                else if (filledParameter.IsInt32)
                {
                    ToR(sb, filledParameter.AsInt32);
                }
                else if (filledParameter.IsBoolean)
                {
                    ToR(sb, filledParameter.AsBoolean);
                }
            }
            sb.AppendLine();
        }

        public static void ToR(StringBuilder sb, string frameName, DataFrame frame)
        {
            List<string> variableNames = new List<string>();
            foreach (Variable variable in frame.Variables)
            {
                StringBuilder nameBuilder = new StringBuilder();
                RConvert.ToRName(nameBuilder, variable.Title);
                string variableName = nameBuilder.ToString();
                sb.Append(variableName);
                sb.Append(" <- ");
                ToR(sb, variable);
                sb.AppendLine();
                variableNames.Add(variableName);
            }
            ToRName(sb, frameName);
            sb.Append(" <- data.frame(");
            sb.Append(string.Join(", ", variableNames.ToArray()));
            sb.Append(")");
        }

        public static void ToR(StringBuilder sb, Variable variable)
        {
            if (variable.IsDoubleVariable)
                ToR(sb, variable.AsDoubleVariable);
            if (variable.IsStringVariable)
                ToR(sb, variable.AsStringVariable);
        }

        public static void ToR(StringBuilder sb, DoubleVariable variable)
        {
            double[] data = variable.Data;
            sb.Append("c(");
            bool first = true;
            foreach (double value in data)
            {
                if (first)
                    first = false;
                else
                    sb.Append(",");
                ToR(sb, value);
            }
            sb.Append(")");
        }

        public static void ToR(StringBuilder sb, StringVariable variable)
        {
            string[] data = variable.Data;
            sb.Append("c(");
            bool first = true;
            foreach (string value in data)
            {
                if (first)
                    first = false;
                else
                    sb.Append(",");
                ToR(sb, value);
            }
            sb.Append(")");
        }

        public static void ToR(StringBuilder sb, double value)
        {
            if (Constant.MISSING == value)
                sb.Append("NA");
            else
                sb.Append(value.ToString("R"));
        }

        public static void ToR(StringBuilder sb, int value)
        {
            sb.Append(value.ToString());
        }

        public static void ToR(StringBuilder sb, string value)
        {
            sb.Append('"');
            sb.Append(value.Replace("\"", "\\\""));
            sb.Append('"');
        }

        public static void ToR(StringBuilder sb, bool value)
        {
            sb.Append(value ? "TRUE" : "FALSE");
        }

        internal static DataFrame ToFrame(string frameName, string variableName, List<object> data)
        {
            // Check types: use double if all double, else (for now) string.  TODO: Other types.
            bool allDouble = data.All(o => o is double);
            Variable v;
            if (allDouble)
            {
                DoubleVariable dv = new DoubleVariable(data.Count, variableName);
                for (int i = 0; i < data.Count; i++)
                    dv.Data[i] = (double)data[i];
                v = dv;
            }
            else
            {
                StringVariable dv = new StringVariable(data.Count, variableName);
                for (int i = 0; i < data.Count; i++)
                    dv.Data[i] = data[i].ToString();
                v = dv;
            }
            return new DataFrame(v, frameName);
        }
    }
}
