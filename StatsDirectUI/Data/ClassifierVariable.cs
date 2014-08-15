using System.Xml.Serialization;
using System.Text;
using System;
using System.Collections.Generic;

namespace StatsDirect.Data
{
    [Serializable]
    public class ClassifierVariable : DoubleVariable
    {

        private IList<Group> _groups;

        public ClassifierVariable()
        {
            _groups = new List<Group>();
        }

        public override bool IsClassifier
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
            if (!(victim.IsClassifier))
            {
                throw new InvalidCastException("Victim must be of the same type when stealing variables");
            }
            base.StealDataFrom(victim);
            ClassifierVariable cVictim = victim.AsClassifierVariable;
            _groups = cVictim._groups;
        }

        [XmlIgnore]
        public IList<Group> Groups
        {
            get
            {
                return _groups;
            }
            set
            {
                _groups = value;
            }
        }

        public Group get_Group(int Index)
        {
            return _groups[Index];
        }

        public void set_Group(int Index, Group value)
        {
            EnsureGroups(Index + 1);
            _groups[Index] = value;
        }

        public int GroupCount
        {
            get
            {
                return _groups.Count;
            }
        }

        public void EnsureGroups(int MinimumSize)
        {
            while (_groups.Count < MinimumSize)
            {
                _groups.Add(null);
            }
        }

        public override ClassifierVariable AsClassifierVariable
        {
            get
            {
                return this;
            }
        }

        public override VariableType VariableType
        {
            get
            {
                return StatsDirect.Data.VariableType.ClassifierType;
            }
        }

        public override object CopyAndStripForRedo(bool shouldKeepData)
        {
            ClassifierVariable copy = new ClassifierVariable();
            CopyAndStripForRedoInto(copy, shouldKeepData);
            if (Origin == null || shouldKeepData)
            {
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy._groups = _groups;
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
                {
                    n.Add(g.Label);
                }
                n.Sort();
                return n.ToArray();
            }
        }

        protected override bool HasData
        {
            get
            {
                return base.HasData && _groups != null;
            }
        }

        public Group GroupWithId(double id)
        {
            foreach (Group group in _groups)
            {
                if (group.Id == id)
                    return group;
            }
            return null;
        }
    }


}
