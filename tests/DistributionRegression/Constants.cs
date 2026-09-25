// The two constants the beta routines take from Numerics.cs, so that the tests compile the routines' files alone, without the
// rest of the program.
namespace StatsDirect.Numerics;
public static class Constant
{
    public const double DBL_MIN = 2.2250738585072014e-308;
    public const double MISSING = double.MinValue;
}
