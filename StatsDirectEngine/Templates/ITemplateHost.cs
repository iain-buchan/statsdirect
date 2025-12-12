namespace StatsDirect.Templates
{
    /// <summary>
    /// An application capable of hosting the template language.
    /// </summary>
    public interface ITemplateHost
        : IPreferencesAndProgressBar
        , IRControllerHost
        , IReportHost
        , IScriptEngineHost
        , IUserInterface
        , ISession
    {
    }
}
