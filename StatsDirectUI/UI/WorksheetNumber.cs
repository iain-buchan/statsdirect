using StatsDirect.Numerics;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace StatsDirect.UI;

internal static class WorksheetNumber
{
    private const NumberStyles GroupedNumber = NumberStyles.Float | NumberStyles.AllowThousands;

    internal static double Read(object value, int row, int column, bool validate = true)
    {
        double number = Parse(value, out bool misplacedSeparator);
        if (misplacedSeparator && validate)
            throw new WorksheetNumberFormatException(row, column);
        return number;
    }

    internal static double Parse(object value, out bool misplacedSeparator)
    {
        misplacedSeparator = false;
        if (value is double number)
            return number;
        if (value is int integer)
            return integer;
        if (value is not string text)
            return Constant.MISSING;

        var format = NumberFormatInfo.CurrentInfo;
        if (double.TryParse(text, NumberStyles.Float, format, out number))
            return number;
        if (text is "*" or "." || text.Equals("MISSING", StringComparison.OrdinalIgnoreCase))
            return Constant.MISSING * 10D;

        if (double.TryParse(text, GroupedNumber, format, out number))
        {
            if (HasValidGrouping(text, format))
                return number;
            misplacedSeparator = true;
        }
        else
        {
            // A number with a foreign separator or a separator in its fractional
            // part or exponent can fail even the permissive .NET parse.
            string trimmed = text.Trim();
            string withoutSeparators = trimmed.Replace(".", "").Replace(",", "").Replace(" ", "")
                .Replace("\u00a0", "").Replace("\u202f", "").Replace(format.NumberDecimalSeparator, "");
            if (format.NumberGroupSeparator.Length > 0)
                withoutSeparators = withoutSeparators.Replace(format.NumberGroupSeparator, "");
            misplacedSeparator = withoutSeparators.Length < trimmed.Length
                && withoutSeparators.AsSpan().IndexOfAnyInRange('0', '9') >= 0
                && double.TryParse(withoutSeparators, NumberStyles.Float, format, out _);
        }
        return Constant.MISSING;
    }

    private static bool HasValidGrouping(string text, NumberFormatInfo format)
    {
        string separator = format.NumberGroupSeparator;
        if (separator.Length == 0)
            return false;

        ReadOnlySpan<char> integral = text.AsSpan().Trim();
        if (integral.StartsWith(format.NegativeSign, StringComparison.Ordinal))
            integral = integral[format.NegativeSign.Length..];
        else if (integral.StartsWith(format.PositiveSign, StringComparison.Ordinal))
            integral = integral[format.PositiveSign.Length..];
        int exponent = integral.IndexOfAny('e', 'E');
        if (exponent >= 0)
            integral = integral[..exponent];
        int decimalPoint = integral.IndexOf(format.NumberDecimalSeparator, StringComparison.Ordinal);
        if (decimalPoint >= 0)
            integral = integral[..decimalPoint];

        // .NET accepts an ordinary space for locales that group with a no-break space.
        string digits = integral.ToString();
        if (separator is "\u00a0" or "\u202f")
            digits = digits.Replace(" ", separator);
        string[] groups = digits.Split(separator, StringSplitOptions.None);
        if (groups.Length < 2)
            return false;

        int[] sizes = format.NumberGroupSizes;
        if (sizes.Length == 0)
            return false;
        for (int offset = 0; offset < groups.Length; offset++)
        {
            int size = sizes[Math.Min(offset, sizes.Length - 1)];
            int length = groups[^(offset + 1)].Length;
            // The leftmost group can be shorter. A final zero means its length is unrestricted.
            if (offset == groups.Length - 1)
                return length > 0 && (size == 0 || length <= size);
            if (size == 0 || length != size)
                return false;
        }
        return false;
    }
}

// Map each misplaced separator from its visible-row index to its physical worksheet row.
internal readonly record struct WorksheetNumericValues(double[] Values, int VisibleRows, Dictionary<int, int> Misplaced = null)
{
    internal bool HasMisplacedSeparator(int index) => Misplaced?.ContainsKey(index) == true;

    internal void RequireValidNumbers(int column, int titleRows = 0, IReadOnlyList<bool> includedRows = null)
    {
        if (Misplaced == null)
            return;
        foreach (var (index, physicalRow) in Misplaced)
        {
            int row = index - titleRows;
            if (row >= 0 && (includedRows == null || row < includedRows.Count && includedRows[row]))
                throw new WorksheetNumberFormatException(physicalRow, column);
        }
    }
}

internal sealed class WorksheetNumberFormatException(int row, int column) : InvalidOperationException(
    $"Cell {Formatting.ToExcelColumnName(column)}{(row + 1).ToString(CultureInfo.InvariantCulture)} has a misplaced number separator. "
    + "Check the decimal and thousands separators for your regional settings, or enter the value as a number.")
{
}
