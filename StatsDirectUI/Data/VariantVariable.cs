using System;
using StatsDirect.Utilities;

namespace StatsDirect.Data
{
    ///  <summary>
    ///  Represents a single variant variable/factor/column/field.
    ///  </summary>
    [Serializable]
    public class VariantVariable : Variable
    {
        ///  <summary>
        ///  Manage the entire data array at one time
        ///  </summary>
        ///  <value>The new data array to set</value>
        ///  <returns>The current data array</returns>
        ///  <remarks></remarks>
        public object[] Data { get; set; }

        public VariantVariable(int length, string title)
        {
            EnsureLength(length);
            Title = title;
        }

        public VariantVariable(object[] data)
        {
            Data = data;
        }

        public VariantVariable(object[] data, string title)
        {
            Data = data;
            Title = title;
        }

        public VariantVariable()
        {
            //  Nothing else required
        }

        public override int Length
        {
            get
            {
                return (Data == null) ? 0 : Data.Length;
            }
        }

        public override void EnsureLength(int minimumLength)
        {
            if ((Data == null))
            {
                Data = new object[minimumLength];
            }
            else
            {
                if (Data.Length < minimumLength)
                {
                    object[] transTemp0 = new object[minimumLength];
                    Array.Copy(Data, transTemp0, Data.Length);
                    Data = transTemp0;
                }
            }
        }

        public override void EnsureLength(int minimumLength, bool useMissing)
        {
            int currentLength;
            if ((Data == null))
            {
                currentLength = 0;
                Data = new object[minimumLength];
            }
            else
            {
                currentLength = Data.Length;
                if (Data.Length < minimumLength)
                {
                    object[] transTemp1 = new object[minimumLength];
                    Array.Copy(Data, transTemp1, Data.Length);
                    Data = transTemp1;
                }
            }
            if (useMissing)
                for (int i = currentLength; i < Data.Length; i++)
                    Data[i] = Formatting.ASTERISK;
        }

        public override void TruncateDataToLength(int maximumLength)
        {
            if ((Data.Length > maximumLength))
            {
                object[] transTemp2 = new object[maximumLength];
                Array.Copy(Data, transTemp2, maximumLength);
                Data = transTemp2;
            }
        }

        public override Variable SameSizeForResults()
        {
            Variable newVariable = new VariantVariable();
            newVariable.EnsureLength(Length);
            return newVariable;
        }

        public override VariantVariable AsVariantVariable
        {
            get
            {
                return this;
            }
        }

        public override object CopyAndStripForRedo(bool shouldKeepData)
        {
            VariantVariable copy = new VariantVariable();
            CopyAndStripForRedoInto(copy, shouldKeepData);
            return copy;
        }

        protected void CopyAndStripForRedoInto(VariantVariable copy, bool shouldKeepData)
        {
            base.CopyAndStripForRedoInto(copy, shouldKeepData);
            if (Origin == null || shouldKeepData)
            {
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy.Data = Data;
            }
        }

        public override void StealDataFrom(Variable victim)
        {
            if (!(victim.IsVariantVariable))
            {
                throw new InvalidCastException("Victim must be of the same type when stealing variables");
            }
            Data = victim.AsVariantVariable.Data;
        }

        public override bool IsVariantVariable
        {
            get
            {
                return true;
            }
        }

        protected override bool HasData
        {
            get
            {
                return Data != null;
            }
        }
    }
}
