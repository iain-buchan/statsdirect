using System;
using System.Collections.Generic;
using System.Text;
using StatsDirect.Templates;

namespace StatsDirect.TemplateProcessing
{
    public class RtfReportRenderer : ReportRenderer
    {
        public string Template { get; set; }

        public override string Render(ITemplateHost host, ParameterBag substitutions)
        {
            string rtfWithInclusions = ResolveTemplates(Template, new List<string>());
            string rtfWithPossibleDeadBlocks = SubstituteInternal(rtfWithInclusions, substitutions);
            return RemoveBlocks(rtfWithPossibleDeadBlocks);
        }

        /// <summary>
        /// Get rid of any /bs xxx/.../bf/ pairs that have not already been substituted
        /// </summary>
        /// <param name="rtfWithPossibleDeadBlocks"></param>
        /// <returns></returns>
        private static string RemoveBlocks(string rtfWithPossibleDeadBlocks)
        {
            StringBuilder sb = new StringBuilder();
            int sourcePosition = 0;
            do
            {
                int startOfTemplate = rtfWithPossibleDeadBlocks.IndexOf("/bs", sourcePosition, StringComparison.Ordinal);
                if (startOfTemplate < 0)
                {
                    sb.Append(rtfWithPossibleDeadBlocks.Substring(sourcePosition));
                    return sb.ToString();
                }

                sb.Append(rtfWithPossibleDeadBlocks.Substring(sourcePosition, startOfTemplate - sourcePosition));
                int t = startOfTemplate;
                int l = t;
                do
                {
                    t = rtfWithPossibleDeadBlocks.IndexOf("/bs", t + 1, StringComparison.Ordinal); // Note lack of trailing slash as we want to match any template name
                    l = rtfWithPossibleDeadBlocks.IndexOf("/bf/", l + 1, StringComparison.Ordinal);
                } while (t >= 0 && t <= l);
                if (-1 == l)
                {
                    throw new Exception("Mismatched start and end blocks in template");
                }
                sourcePosition = l + 4;
            } while (true);
        }

        /// <summary>
        /// Search for strings of the form /include "name"/ in the template; where they exist, replace them with the named template
        /// </summary>
        /// <param name="rawRtf">The RTF to examine for inclusions</param>
        /// <param name="knownInclusions"></param>
        /// <returns>The RTF with inclusions replaced</returns>
        private static string ResolveTemplates(string rawRtf, List<string> knownInclusions)
        {
            StringBuilder sb = new StringBuilder();
            int sourcePosition = 0;
            do
            {
                int startOfInclusion = rawRtf.IndexOf("/include", sourcePosition, StringComparison.Ordinal);
                if (startOfInclusion < 0)
                {
                    sb.Append(rawRtf.Substring(sourcePosition));
                    return sb.ToString();
                }

                int endOfInclusion = rawRtf.IndexOf("/", startOfInclusion + 1, StringComparison.Ordinal);
                if (-1 == endOfInclusion)
                    throw new Exception("No terminating / after a /include");

                sb.Append(rawRtf.Substring(sourcePosition, startOfInclusion - sourcePosition));
                string includedName = rawRtf.Substring(startOfInclusion + 8, endOfInclusion - (startOfInclusion + 8));
                includedName = includedName.Trim();
                if (includedName.StartsWith("\""))
                    includedName = includedName.Substring(1);
                if (includedName.EndsWith("\""))
                    includedName = includedName.Substring(0, includedName.Length - 1);
                if (!knownInclusions.Contains(includedName))
                {
                    knownInclusions.Add(includedName);
                    string includedRtf = GetContent(includedName);
                    sb.Append("/split/");
                    sb.Append(ResolveTemplates(includedRtf, knownInclusions));
                    knownInclusions.Remove(includedName);
                }
                sourcePosition = endOfInclusion + 1;
            } while (true);

        }

        /// <summary>
        /// Recursively fill in any values in this block, examining the template for nested blocks.
        /// </summary>
        /// <param name="template"></param>
        /// <param name="substitutions"></param>
        /// <returns></returns>
        private string SubstituteInternal(string template, ParameterBag substitutions)
        {
            if (null != substitutions)
            {
                // TODO: Horribly inefficient, but will suffice for now
                bool foundNestedKey = false;
                foreach (KeyValuePair<string, FilledParameter> pair in substitutions.Pairs)
                {
                    if (pair.Key.StartsWith("*"))
                    {
                        foundNestedKey = true;
                        // A nested value.  Look for a nested template with the given name and fill it in.
                        string nestedTemplateName = pair.Key.Substring(1);
                        string nestedTemplate = FindNestedTemplate(template, nestedTemplateName);
                        while (null != nestedTemplate)
                        {
                            IList<ParameterBag> value = null;
                            if (null != pair.Value)
                                value = pair.Value.AsParameterBagList;
                            string nestedResult = SubstituteInternal(nestedTemplate, value);
                            template = template.Replace(nestedTemplate, nestedResult);
                            nestedTemplate = FindNestedTemplate(template, nestedTemplateName);
                        }
                    }
                    else
                    {
                        string value = string.Empty;
                        if (null != pair.Value && pair.Value.HasData)
                            value = pair.Value.Data.ToString();
                        template = template.Replace("/*" + pair.Key + "/", value);
                    }
                }
                // We may have a nested template, but nothing to put in it.  If this is the case, delete the template.
                if (!foundNestedKey)
                {
                    string nestedTemplate = FindNestedTemplate(template, null);
                    if (null != nestedTemplate)
                        template = template.Replace(nestedTemplate, string.Empty);
                }
            }
            return template;
        }

        /// <summary>
        /// Recursively fill in a template.  We've found a nested template; clone it as many times as we have values, and fill it in.
        /// If there are no values, remove the template entirely.
        /// </summary>
        /// <param name="template"></param>
        /// <param name="substitutions"></param>
        /// <returns></returns>
        private string SubstituteInternal(string template, IEnumerable<ParameterBag> substitutions)
        {
            // If the template still has a /bs templatename/.../bf/ pair, strip them.
            if (template.StartsWith("/bs"))
            {
                template = template.Substring(template.IndexOf('/', 1) + 1);
                if (template.EndsWith("/bf/"))
                {
                    template = template.Substring(0, template.Length - 4);
                }
            }

            // We now have a naked template.  Replicate it the appropriate number of times, and fill it in.
            string filledValue = string.Empty;
            if (null != substitutions)
            {
                foreach (ParameterBag substitutionDictionary in substitutions)
                {
                    filledValue += SubstituteInternal(template, substitutionDictionary);
                }
            }
            return filledValue;
        }

        /// <summary>
        /// Find the first template (starting with /bs templateName/) in the string, and return the substring up to a matching /bf/.
        /// </summary>
        /// <param name="template"></param>
        /// <param name="templateName">The name of the template to find.  This may be blank, in which case /bs/ is searched for; or null, in which case any template name will do.</param>
        /// <returns></returns>
        private static string FindNestedTemplate(string template, string templateName)
        {
            string searchSuffix = string.Empty;
            if (null != templateName)
                searchSuffix = (0 == templateName.Length ? templateName : " " + templateName) + "/";

            int startOfTemplate = template.IndexOf("/bs" + searchSuffix, StringComparison.Ordinal);
            if (startOfTemplate >= 0)
            {
                int t = startOfTemplate;
                int l = t;
                do
                {
                    t = template.IndexOf("/bs", t + 1, StringComparison.Ordinal); // Note lack of trailing slash as we want to match any template name
                    l = template.IndexOf("/bf/", l + 1, StringComparison.Ordinal);
                } while (t >= 0 && t <= l);
                if (-1 == l)
                {
                    throw new Exception("Mismatched start and end blocks in template: there is no end block for \"" + templateName + "\"");
                }
                return template.Substring(startOfTemplate, l - startOfTemplate + 4);
            }

            // If we get here, there's no nested template with the given name.
            return null;
        }
    }
}
