// The constants the regression routines take from Numerics.cs, so that the tests compile the routines' file alone, without the rest of the program.
namespace StatsDirect.Numerics;
public static class Constant
{
    public const double MISSING = double.MinValue;
    public const double EPSNEG = 1.11022302462515654042E-016;
    public const double EPSILON = 2.22044604925031308085E-016;
    public const double SPREAL = 2.2250738585072014E-308;
}
