namespace StatsDirect.Utilities
{
    public sealed class Utilities
    {
        public static void Swap(ref double x, ref double y)
        {
            double t = x;
            x = y;
            y = t;
        }
    }
}
