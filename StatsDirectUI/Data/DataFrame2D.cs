using System;
using System.Collections.Generic;

namespace StatsDirect.Data
{
    [Serializable]
    public class DataFrame2D
    {
        ///  <summary>
        ///  A name that can be used to identify the frame by the user
        ///  </summary>
        private string _name;
        private IList<IList<Variable>> _variables;

        public DataFrame2D()
        {
            _variables = new List<IList<Variable>>();
        }

        public DataFrame2D(Variable v)
        {
            _variables = new List<IList<Variable>>();
            IList<Variable> subV = new List<Variable>();
            subV.Add(v);
            _variables.Add(subV);
        }

        public DataFrame2D(Variable v, string name)
        {
            _variables = new List<IList<Variable>>();
            IList<Variable> subV = new List<Variable>();
            subV.Add(v);
            _variables.Add(subV);
            _name = name;
        }

        ///  <summary>
        ///  A name that can be used to identify the frame by the user
        ///  </summary>
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }

        public IList<IList<Variable>> Variables
        {
            get
            {
                return _variables;
            }
            set
            {
                _variables = value;
            }
        }

        public int VariableCount => _variables.Count;

        public int VariableCountTheOtherWay
        {
            get
            {
                int maxCount = 0;
                foreach (IList<Variable> vl in _variables)
                    maxCount = Math.Max(maxCount, vl.Count);
                return maxCount;
            }
        }

        ///  <summary>
        ///  Ensure there are at least MinimumSize1 variable sets, with all able to hold at least MinimumSize2 variables.
        ///  </summary>
        ///  <param name="minimumSize1"></param>
        ///  <param name="minimumSize2"></param>
        ///  <remarks></remarks>
        public void EnsureVariablesSquare(int minimumSize1, int minimumSize2)
        {
            //  Ensure MinimumSize1 variable lists
            while (_variables.Count < minimumSize1)
            {
                IList<Variable> vbls = new List<Variable>(minimumSize2);
                _variables.Add(vbls);
            }
            //  Ensure Minimum1 is at least MinimumSize2 in size
            foreach (IList<Variable> vbls in _variables)
                while (vbls.Count < minimumSize2)
                    vbls.Add(null);
        }

        ///  <summary>
        ///  Ensure there are at least MinimumSize1 variable sets, with the Minimum1th able to hold at least MinimumSize2 variables.
        ///  </summary>
        ///  <param name="minimumSize1"></param>
        ///  <param name="minimumSize2"></param>
        ///  <remarks></remarks>
        public void EnsureVariablesJagged(int minimumSize1, int minimumSize2)
        {
            //  Ensure MinimumSize1 variable lists
            while (_variables.Count < minimumSize1)
                _variables.Add(new List<Variable>(minimumSize2));

            //  Ensure Minimum1 is at least MinimumSize2 in size
            IList<Variable> vbls1 = _variables[minimumSize1 - 1];
            while (vbls1.Count < minimumSize2)
                vbls1.Add(null);
        }

        public int MaxRows
        {
            get
            {
                int maxLength = 0;
                foreach (IList<Variable> vl in _variables)
                    foreach (Variable v in vl)
                        if (v != null)
                            maxLength = Math.Max(maxLength, v.Length);
                return maxLength;
            }
        }

        public int MinRows
        {
            get
            {
                if (_variables.Count == 0)
                    return 0;

                int minLength = int.MaxValue;
                foreach (IList<Variable> vl in _variables)
                    foreach (Variable v in vl)
                        if (v != null)
                            minLength = Math.Min(minLength, v.Length);
                return minLength;
            }
        }

        /// ' <summary>
        /// ' Return a new DataFrame with the same number of variables, each of the same size, as this frame.
        /// ' The new frame is not otherwise initialised - no names, no values.
        /// ' </summary>
        /// ' <returns>a new DataFrame with the same number of variables, each of the same size, as this frame</returns>
        // Public Function SameSizeForResults() As DataFrame2D
        //     Dim newFrame As DataFrame2D = New DataFrame2D()
        //     newFrame.EnsureVariables(VariableCount, VariableCountTheOtherWay)
        //     For i As Integer = 0 To VariableCount - 1
        //         Dim vl As IList(Of Variable) = _variables(i)
        //         For Each v As Variable In vl
        //             newFrame.Variables(i).Add(v.SameSizeForResults())
        //         Next
        //     Next
        //     Return newFrame
        // End Function

        public void TruncateToLength(int maximumLength)
        {
            while (_variables.Count > maximumLength)
            {
                _variables.RemoveAt(maximumLength);
            }
        }
    }
}
