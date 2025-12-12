using StatsDirect.Templates;

namespace StatsDirect.TemplateProcessing
{
    public abstract class ReportRenderer
    {
        public abstract string Render(/* TODO: IPreferences */ ITemplateHost host, string content, ParameterBag substitutions);
    }
}
