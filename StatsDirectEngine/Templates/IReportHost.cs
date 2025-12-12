namespace StatsDirect.Templates
{
    /// <summary>
    /// Data needed by report processing that can only be known by the host.
    /// </summary>
    public interface IReportHost
    {
        /// <summary>
        /// Returns the content of the report template named reportName, or null if no report with the specified name exists.
        /// </summary>
        string GetReportTemplate(string reportName);
    }
}
