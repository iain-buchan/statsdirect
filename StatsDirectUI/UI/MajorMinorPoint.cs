namespace StatsDirect.UI
{
    internal class MajorMinorPoint
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Point { get; set; }
        public bool IsValid { get; set; }

        public MajorMinorPoint()
        {
        }

        public MajorMinorPoint(string s)
        {
            // Application.ProductVersion is the informational version, to which current .Net SDKs append the source revision ("5.0.0+0123abc...") and which may carry a pre-release label ("5.0.0-beta"); neither is part of the number.
            int suffixPos = s.IndexOfAny(new[] { '+', '-' });
            if (suffixPos >= 0)
                s = s.Substring(0, suffixPos);
            string[] parts = s.Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor) && int.TryParse(parts[2], out int point))
            {
                Major = major;
                Minor = minor;
                Point = point;
                IsValid = true;
            }
        }

        public override string ToString()
        {
            return IsValid ? Major.ToString() + "." + Minor.ToString() + "." + Point.ToString() : "(invalid)";
        }

        public static bool operator <(MajorMinorPoint lhs, MajorMinorPoint rhs)
        {
            if (!(lhs.IsValid && rhs.IsValid))
                return false;
            return lhs.Major != rhs.Major
                ? lhs.Major < rhs.Major
                : lhs.Minor != rhs.Minor
                    ? lhs.Minor < rhs.Minor
                    : lhs.Point < rhs.Point;
        }

        public static bool operator >(MajorMinorPoint lhs, MajorMinorPoint rhs)
        {
            if (!(lhs.IsValid && rhs.IsValid))
                return false;
            return lhs.Major != rhs.Major
                ? lhs.Major > rhs.Major
                : lhs.Minor != rhs.Minor
                    ? lhs.Minor > rhs.Minor
                    : lhs.Point > rhs.Point;
        }
    }
}
