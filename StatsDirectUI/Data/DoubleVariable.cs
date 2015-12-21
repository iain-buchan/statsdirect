using System;
using StatsDirect.Numerics;

namespace StatsDirect.Data
{
    ///  <summary>
    ///  Represents a single non-classifier variable/factor/column/field.
    ///  Use ClassiferVariable to represent a classifier variable.
    ///  </summary>
    [Serializable]
    public class DoubleVariable : Variable
    {
        private double[] data;
        private double sum;
        private double min;
        private double max;
        private bool hasSummaries;

        public DoubleVariable()
        {
            //  Do nothing
        }

        public DoubleVariable(double[] data)
        {
            this.data = data;
        }

        public DoubleVariable(double[] data, string title)
        {
            this.data = data;
            Title = title;
        }

        public DoubleVariable(int length, string title)
        {
            EnsureLength(length);
            Title = title;
        }

        public double Sum
        {
            get
            {
                if (!(hasSummaries))
                    CalculateSummaries();
                return sum;
            }
        }

        public double Min
        {
            get
            {
                if (!(hasSummaries))
                    CalculateSummaries();
                return min;
            }
        }

        public double Max
        {
            get
            {
                if (!(hasSummaries))
                    CalculateSummaries();
                return max;
            }
        }

        private void CalculateSummaries()
        {
            min = double.MaxValue;
            max = double.MinValue;
            sum = 0;
            foreach (double d in data)
            {
                if (d != Constant.MISSING)
                {
                    sum += d;
                    if (d < min)
                        min = d;
                    if (d > max)
                        max = d;
                }
            }
            hasSummaries = true;
        }


        ///  <summary>
        ///  Manage the entire data array at one time
        ///  </summary>
        ///  <value>The new data array to set</value>
        ///  <returns>The current data array</returns>
        ///  <remarks></remarks>
        public double[] Data
        {
            get
            {
                return data;
            }
            set
            {
                data = value;
                hasSummaries = false;
            }
        }

        ///  <summary>
        ///  Access a single element of the data array
        ///  </summary>
        ///  <param name="index">The element to access</param>
        ///  <param name="value">The new value to set. Storage management is done internally, so the array is always sufficently large to hold the value</param>
        ///  <returns>The value at the specified index, or an exception if the index is out of bounds</returns>
        ///  <remarks></remarks>
        public void SetData(int index, double value)
        {
            EnsureLength(index + 1);
            data[index] = value;
            hasSummaries = false;
        }

        public override int Length
        {
            get
            {
                return (data == null) ? 0 : data.Length;
            }
        }

        public override void EnsureLength(int minimumLength)
        {
            if ((data == null))
            {
                data = new double[minimumLength];
            }
            else
            {
                if (data.Length < minimumLength)
                {
                    double[] transTemp0 = new double[minimumLength];
                    Array.Copy(data, transTemp0, data.Length);
                    data = transTemp0;
                }
            }
        }

        public void EnsureLength(int minimumLength, double fillValue)
        {
            if ((data == null))
            {
                data = new double[minimumLength];
                for (int i = 0; i <= minimumLength - 1; i++)
                    data[i] = fillValue;
            }
            else
            {
                if (data.Length < minimumLength)
                {
                    int oldLength = data.Length;
                    double[] transTemp1 = new double[minimumLength];
                    Array.Copy(data, transTemp1, data.Length);
                    data = transTemp1;
                    for (int i = oldLength; i < minimumLength; i++)
                        data[i] = fillValue;
                }
            }
        }

        public override void EnsureLength(int minimumLength, bool useMissing)
        {
            if (useMissing)
                EnsureLength(minimumLength, Constant.MISSING);
            else
                EnsureLength(minimumLength);
        }

        public override void TruncateDataToLength(int maximumLength)
        {
            if ((data.Length > maximumLength))
            {
                double[] transTemp2 = new double[maximumLength];
                Array.Copy(data, transTemp2, Math.Min(data.Length, transTemp2.Length));
                data = transTemp2;
                hasSummaries = false;
            }
        }

        public override Variable SameSizeForResults()
        {
            Variable newVariable = new DoubleVariable();
            newVariable.EnsureLength(Length);
            return newVariable;
        }

        public override DoubleVariable AsDoubleVariable
        {
            get
            {
                return this;
            }
        }

        ///  <summary>
        ///  True if this Variable is a classifier variable; false if it is not
        ///  </summary>
        public override bool IsDoubleVariable
        {
            get
            {
                return true;
            }
        }

        public override void StealDataFrom(Variable victim)
        {
            if (!(victim.IsDoubleVariable))
            {
                throw new InvalidCastException("Victim must be of the same type when stealing variables");
            }
            data = victim.AsDoubleVariable.data;
            hasSummaries = false;
        }

        public override object CopyAndStripForRedo(bool shouldKeepData)
        {
            DoubleVariable copy = new DoubleVariable();
            CopyAndStripForRedoInto(copy, shouldKeepData);
            return copy;
        }

        protected void CopyAndStripForRedoInto(DoubleVariable copy, bool shouldKeepData)
        {
            base.CopyAndStripForRedoInto(copy, shouldKeepData);
            if (Origin == null || shouldKeepData)
            {
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy.data = data;
            }
        }

        public override object DataAsObject(int i)
        {
            return Data[i];
        }

        protected override bool HasData
        {
            get
            {
                return data != null;
            }
        }
    }
}
