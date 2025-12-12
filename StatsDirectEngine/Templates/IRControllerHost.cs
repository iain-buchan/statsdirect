namespace StatsDirect.Templates
{
    /// <summary>
    /// Data needed by R scripting that can only be known by the host.
    /// </summary>
    public interface IRControllerHost
    {
        /// <summary>
        /// If possible, prompt the user to install R.  Return true if we think the user might have installed R successfully, or false if there's no chance (for example, there's no UI or the user's told us that they're not going to).
        /// </summary>
        bool RequestRInstallation();

        /// <summary>
        /// Path to where StatsDirect communicates files to R.
        /// </summary>
        string MyStatsDirectRFolder { get; }
    }
}
