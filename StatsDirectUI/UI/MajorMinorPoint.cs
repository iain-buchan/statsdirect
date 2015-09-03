namespace StatsDirect.UI
{
    class MajorMinorPoint
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
            int major;
            int minor;
            int point;
            string[] parts = s.Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor) && int.TryParse(parts[2], out point))
            {
                Major = major;
                Minor = minor;
                Point = point;
                IsValid = true;
            }
        }

        public override string ToString()
        {
            return IsValid ? (Major.ToString() + "." + Minor.ToString() + "." + Point.ToString()) : "(invalid)";
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
