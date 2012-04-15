namespace StatsDirect.Templates
{
    public interface IMightRequireInput
    {
        InputDuringStep RequiresInput { get; }
    }
}
