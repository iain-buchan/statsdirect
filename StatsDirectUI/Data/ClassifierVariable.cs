using System.Xml.Serialization;
using System.Text;
using System;
using System.Collections.Generic;
using StatsDirect.Numerics;

namespace StatsDirect.Data
{
    [Serializable]
    public class ClassifierVariable : GenericVariable<double>
    {
        public ClassifierVariable()
        {
            Groups = new List<Group>();
        }

        public override void StealDataFrom(IVariable victim)
        {
            if (!(victim is ClassifierVariable))
                throw new InvalidCastException("Victim must be of the same type when stealing variables");
            base.StealDataFrom(victim);
            ClassifierVariable cVictim = victim as ClassifierVariable;
            Groups = cVictim.Groups;
        }

        [XmlIgnore]
        public IList<Group> Groups { get; set; }

        public int GroupCount => Groups.Count;

        public void EnsureGroups(int minimumSize)
        {
            while (Groups.Count < minimumSize)
                Groups.Add(null);
        }

        public string CommaSeparatedCategoryNames
        {
            get
            {
                StringBuilder sb = new();
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
                List<string> n = new();
                foreach (Group g in Groups)
                    n.Add(g.Label);
                n.Sort();
                return n.ToArray();
            }
        }

        protected override bool HasData => base.HasData && Groups != null;

        public Group GroupWithId(double id)
        {
            foreach (Group group in Groups)
                if (group.Id == id)
                    return group;
            return null;
        }

        public override void Accept(IVariableVisitor visitor)
        {
            visitor.Visit(this);
        }

        /// <summary>
        /// Lengthens the variable with missing values, the code a blank or "*" cell is read as, as the unequal-length warning of a
        /// selection says the jagged ends are padded. The inherited padding filled them with 0, the id of the first category, so that
        /// the padded rows of a shorter column of ratings counted as ratings of that category.
        /// </summary>
        public override void EnsureLengthAndPadWithMissing(int minimumLength)
        {
            EnsureLength(minimumLength, Constant.MISSING);
        }
    }
}
