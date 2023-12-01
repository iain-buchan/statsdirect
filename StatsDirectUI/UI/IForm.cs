namespace StatsDirect.UI
{
    /// <summary>
    /// Any form - something that can be shown in the main SD window - should implement this interface.
    /// </summary>
    public interface IForm
    {
        /// <summary>
        /// If the form is not currently the active form, activate it.
        /// If the form is currently the active form, do nothing.
        /// </summary>
        void EnsureActive();

        /// <summary>
        /// A unique identifier for this window that will not change or be duplicated for the lifetime of the application.
        /// The window may be deleted, but the ID must not be re-used by another window.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Ensure that the pane identified by pane is selected.  By convention, a null pane indicates creation of a new pane where possible.
        /// </summary>
        /// <param name="pane">The pane to select</param>
        /// <returns>true if the selection succeeded, false if it failed (generally due to the sheet no longer existing)</returns>
        bool SelectPane(Pane pane);

        /// <summary>
        /// The Pane that is currently selected
        /// </summary>
        Pane SelectedPane { get; }
    }
}
