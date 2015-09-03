namespace StatsDirect.Data
{
    ///  <summary>
    ///  Objects that may have content that should be discarded after performing an operation and re-acquired if the operation is redone should implement this interface.
    ///  A typical example would be a data frame, where the data should be discarded and re-acquired based on its provenance information if the operation is re-done.
    ///  </summary>
    ///  <remarks>Strictly, this should be part of the template system.  Unfortunately template requires data, and this would require a circular dependency.</remarks>
    public interface IStripForRedo
    {
        object CopyAndStripForRedo(bool shouldKeepData);

        void RefillForRedo(IRefillSource source);
    }
}
