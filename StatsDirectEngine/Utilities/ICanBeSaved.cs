namespace StatsDirect.Utilities
{
    /// <summary>
    /// A common place to define a save interface. Needed because several interfaces supply Save and have the same implementation class which should only implement this once.
    /// </summary>
    public interface ICanBeSaved
    {
        void Save();
    }
}
