using System.Xml.Serialization;
using System;
using System.Collections.Generic;
namespace StatsDirect.Data
{
    [Serializable]
    public class DataFrame : IStripForRedo
    {
        public DataFrame()
        {
            Variables = new List<Variable>();
        }

        public DataFrame(Variable v)
        {
            Variables = new List<Variable> { v };
        }

        public DataFrame(Variable v, string name)
            : this(v)
        {
            Name = name;
        }

        ///  <summary>
        ///  A name that can be used to identify the frame by the user
        ///  </summary>
        [XmlElement("name")]
        public string Name { get; set; }

        [XmlIgnore]
        public IList<Variable> Variables { get; set; }

        [XmlArray("variables")]
        [XmlArrayItem("classifier-variable", typeof(ClassifierVariable))]
        [XmlArrayItem("date-variable", typeof(DateVariable))]
        [XmlArrayItem("double-variable", typeof(DoubleVariable))]
        [XmlArrayItem("string-variable", typeof(StringVariable))]
        public Variable[] VariablesForXml
        {
            get
            {
                Variable[] retval = new Variable[Variables.Count];
                int i = 0;
                foreach (Variable v in Variables)
                    retval[i++] = v;
                return retval;
            }
            set
            {
                Variables.Clear();
                foreach (Variable v in value)
                    Variables.Add(v);
            }
        }

        public int VariableCount => Variables.Count;

        public void EnsureVariables(int MinimumSize)
        {
            while (Variables.Count < MinimumSize)
                Variables.Add(null);
        }

        public int MaxRows
        {
            get
            {
                int maxLength = 0;
                foreach (Variable v in Variables)
                    maxLength = Math.Max(maxLength, v.Length);
                return maxLength;
            }
        }

        public int MinRows
        {
            get
            {
                if (Variables.Count == 0)
                    return 0;

                int minLength = int.MaxValue;
                foreach (Variable v in Variables)
                    minLength = Math.Min(minLength, v.Length);
                return minLength;
            }
        }

        ///  <summary>
        ///  Fill the specified target array as (row, column) from our own variables.
        ///  </summary>
        ///  <param name="target"></param>
        ///  <remarks>The array is assumed to be large enough to hold all of our contents.  Non-double values in the source are represented as Double.NaN.</remarks>
        public void FillArray(double[,] target)
        {
            for (int c = 0; c < Variables.Count; c++)
            {
                DoubleVariable v = (DoubleVariable)Variables[c];
                for (int r = 0; r < v.Length; r++)
                    target[r, c] = v.Data[r];
            }
        }

        ///  <summary>
        ///  Return a new DataFrame with the same number of variables, each of the same size, as this frame.
        ///  The new frame is not otherwise initialised - no names, no values.
        ///  </summary>
        ///  <returns>a new DataFrame with the same number of variables, each of the same size, as this frame</returns>
        public DataFrame SameSizeForResults()
        {
            DataFrame newFrame = new DataFrame();
            newFrame.EnsureVariables(VariableCount);
            foreach (Variable v in Variables)
                newFrame.Variables.Add(v.SameSizeForResults());
            return newFrame;
        }

        public Variable FindVariable(string title)
        {
            foreach (Variable v in Variables)
                if (v.Title != null && v.Title.Equals(title))
                    return v;
            return null;
        }

        public object CopyAndStripForRedo(bool shouldKeepData)
        {
            DataFrame copy = new DataFrame { Name = Name };
            foreach (Variable v in Variables)
                copy.Variables.Add((Variable)v.CopyAndStripForRedo(shouldKeepData));
            return copy;
        }

        public void RefillForRedo(IRefillSource refillSource)
        {
            refillSource.Refill(Variables);
        }
    }
}
