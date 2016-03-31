using System;
using StatsDirect.Utilities;

namespace StatsDirect.Data
{
    ///  <summary>
    ///  Represents a single string-based non-classifier variable/factor/column/field.
    ///  </summary>
    [Serializable]
    public sealed class StringVariable : Variable
    {
        ///  <summary>
        ///  Manage the entire data array at one time
        ///  </summary>
        ///  <value>The new data array to set</value>
        ///  <returns>The current data array</returns>
        ///  <remarks></remarks>
        public string[] Data { get; set; }

        public StringVariable(int length, string title)
        {
            EnsureLength(length);
            Title = title;
        }

        public StringVariable(string[] data)
        {
            Data = data;
        }

        public StringVariable(string[] data, string title)
        {
            Data = data;
            Title = title;
        }

        public StringVariable()
        {
            //  Nothing else required
        }

        ///  <summary>
        ///  Set a single element of the data array, ensuring the variable is long enough to hold the element.
        ///  </summary>
        ///  <param name="index">The element to access</param>
        ///  <param name="value">The new value to set. Storage management is done internally, so the array is always sufficently large to hold the value</param>
        ///  <returns>The value at the specified index, or an exception if the index is out of bounds</returns>
        public void SetData(int index, string value)
        {
            EnsureLength(index + 1);
            Data[index] = value;
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
            if (Data == null)
                Data = new string[minimumLength];
            else
            {
                if (Data.Length < minimumLength)
                {
                    string[] copy = new string[minimumLength];
                    Array.Copy(Data, copy, Data.Length);
                    Data = copy;
                }
            }
        }

        public override void EnsureLength(int minimumLength, bool useMissing)
        {
            int currentLength = (null == Data) ? 0 : Data.Length;
            EnsureLength(minimumLength);
            if (useMissing)
            {
                for (int i = currentLength; i < Data.Length; i++)
                    Data[i] = Formatting.ASTERISK;
            }
        }

        public override void TruncateDataToLength(int maximumLength)
        {
            if (Data.Length > maximumLength)
            {
                string[] copy = new string[maximumLength];
                Array.Copy(Data, copy, maximumLength);
                Data = copy;
            }
        }

        public override Variable SameSizeForResults()
        {
            Variable newVariable = new StringVariable();
            newVariable.EnsureLength(Length);
            return newVariable;
        }

        public override object DataAsObject(int i)
        {
            return Data[i];
        }

        public override object CopyAndStripForRedo(bool shouldKeepData)
        {
            StringVariable copy = new StringVariable();
            CopyAndStripForRedoInto(copy, shouldKeepData);
            return copy;
        }

        private void CopyAndStripForRedoInto(StringVariable copy, bool ShouldKeepData)
        {
            base.CopyAndStripForRedoInto(copy, ShouldKeepData);
            if (Origin == null || ShouldKeepData)
            {
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy.StealDataFrom(this);
            }
        }

        public override void StealDataFrom(Variable victim)
        {
            if (!(victim is StringVariable))
                throw new InvalidCastException("Victim must be of the same type when stealing variables");
            Data = (victim as StringVariable).Data;
        }

        protected override bool HasData
        {
            get
            {
                return Data != null;
            }
        }

        public override void Accept(IVariableVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
