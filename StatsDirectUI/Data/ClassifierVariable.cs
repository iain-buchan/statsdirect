using System.Xml.Serialization;
using System.Text;
using System;
using System.Collections.Generic;

namespace StatsDirect.Data
{
    [Serializable]
    public class ClassifierVariable : DoubleVariable
    {
        public ClassifierVariable()
        {
            Groups = new List<Group>();
        }

        public override bool IsClassifierVariable
        {
            get
            {
                return true;
            }
        }

        public override Variable SameSizeForResults()
        {
            Variable newVariable = new DoubleVariable();
            newVariable.EnsureLength(Length);
            return newVariable;
        }

        public override void StealDataFrom(Variable victim)
        {
            if (!(victim.IsClassifierVariable))
                throw new InvalidCastException("Victim must be of the same type when stealing variables");
            base.StealDataFrom(victim);
            ClassifierVariable cVictim = victim.AsClassifierVariable;
            Groups = cVictim.Groups;
        }

        [XmlIgnore]
        public IList<Group> Groups { get; set; }

        public int GroupCount
        {
            get
            {
                return Groups.Count;
            }
        }

        public void EnsureGroups(int minimumSize)
        {
            while (Groups.Count < minimumSize)
            {
                Groups.Add(null);
            }
        }

        public override ClassifierVariable AsClassifierVariable
        {
            get
            {
                return this;
            }
        }

        public override object CopyAndStripForRedo(bool shouldKeepData)
        {
            ClassifierVariable copy = new ClassifierVariable();
            CopyAndStripForRedoInto(copy, shouldKeepData);
            if (Origin == null || shouldKeepData)
            {
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy.Groups = Groups;
            }
            return copy;
        }

        public string CommaSeparatedCategoryNames
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                bool first = true;
                foreach (Group g in Groups)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else { sb.Append(", "); }
                    sb.Append(g.Label);
                }
                return sb.ToString();
            }
        }

        public string[] SortedCategoryNames
        {
            get
            {
                List<string> n = new List<string>();
                foreach (Group g in Groups)
                    n.Add(g.Label);
                n.Sort();
                return n.ToArray();
            }
        }

        protected override bool HasData
        {
            get
            {
                return base.HasData && Groups != null;
            }
        }

        public Group GroupWithId(double id)
        {
            foreach (Group group in Groups)
                if (group.Id == id)
                    return group;
            return null;
        }
    }
}
