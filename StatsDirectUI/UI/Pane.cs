using System;
using StatsDirect.Data;

namespace StatsDirect.UI
{
    /// <summary>
    /// Represents one user-identifiable object within a StatsDirectForm.
    /// This may be a report (there's only one object in it), a script (ditto) or a grid (which may have sheets within it)
    /// </summary>
    [Serializable]
    public class Pane : IStripForRedo
    {
        public string Name { get; private set; }
        [NonSerialized]
        private WindowInformation windowInformation; // Can't be converted to an auto-property, as NonSerialized can only apply to fields.
        public object Tag { get; private set; }

        internal Pane(string name, WindowInformation info, object tag)
        {
            Name = name;
            windowInformation = info;
            Tag = tag;
        }

        public WindowInformation WindowInformation
        {
            get { return windowInformation; }
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            if (null == obj)
                return false;
            if (GetType() != obj.GetType())
                return false;
            Pane rhs = (Pane)obj;

            // Name
            bool bothNull = null == Name && null == rhs.Name;
            if ((!bothNull) && null == Name || null == rhs.Name)
                return false;
            if ((!bothNull) && !Name.Equals(rhs.Name))
                return false;

            // Window information
            bothNull = null == WindowInformation && null == rhs.WindowInformation;
            if ((!bothNull) && null == WindowInformation || null == rhs.WindowInformation)
                return false;
            if ((!bothNull) && !WindowInformation.Equals(rhs.WindowInformation))
                return false;

            // Tag
            if (null == Tag && null == rhs.Tag)
                return true;
            if (null == Tag || null == rhs.Tag)
                return false;
            return Tag.Equals(rhs.Tag);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ WindowInformation.GetHashCode() ^ Tag.GetHashCode();
        }

        public object CopyAndStripForRedo(bool shouldKeepData)
        {
            // Indicate that this should not be kept
            return null;
        }

        public void RefillForRedo(IRefillSource source)
        {
            // Should never refill, as should never have been present in the first place!
            throw new NotImplementedException();
        }
    }

    [Serializable]
    public enum RelativePosition
    {
        FirstColumn,
        BeforeSelection,
        AfterSelection,
        LastColumn
    }

    [Serializable]
    public class PaneAndPosition : IStripForRedo
    {
        public Pane Pane { get; set; }
        public RelativePosition WritePosition { get; set; }

        internal PaneAndPosition(Pane pane, RelativePosition writePosition)
        {
            Pane = pane;
            WritePosition = writePosition;
        }

        public override string ToString()
        {
            string renderedPosition = "";
            switch (WritePosition)
            {
                case RelativePosition.FirstColumn:
                    renderedPosition = "first column";
                    break;
                case RelativePosition.BeforeSelection:
                    renderedPosition = "before current position";
                    break;
                case RelativePosition.AfterSelection:
                    renderedPosition = "after current position";
                    break;
                case RelativePosition.LastColumn:
                    renderedPosition = "last column";
                    break;
                // default: No effect
            }
            if (string.IsNullOrEmpty(renderedPosition))
                return Pane.ToString();

            return Pane + " (" + renderedPosition + ")";
        }

        public override bool Equals(object obj)
        {
            if (null == obj)
                return false;
            if (GetType() != obj.GetType())
                return false;
            PaneAndPosition rhs = (PaneAndPosition)obj;

            // Pane
            bool bothNull = null == Pane && null == rhs.Pane;
            if ((!bothNull) && null == Pane || null == rhs.Pane)
                return false;
            if ((!bothNull) && !Pane.Equals(rhs.Pane))
                return false;

            // WritePosition
            return WritePosition == rhs.WritePosition;
        }

        public override int GetHashCode()
        {
            return Pane.GetHashCode() ^ ((int)WritePosition);
        }

        public object CopyAndStripForRedo(bool shouldKeepData)
        {
            // Indicate that this should not be kept
            return null;
        }

        public void RefillForRedo(IRefillSource source)
        {
            // Should never refill, as should never have been present in the first place!
            throw new NotImplementedException();
        }
    }
}
