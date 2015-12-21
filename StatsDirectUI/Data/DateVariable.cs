using System;
namespace StatsDirect.Data
{
    ///  <summary>
    ///  Represents a single non-classifier variable/factor/column/field.
    ///  Use ClassiferVariable to represent a classifier variable.
    ///  </summary>
    [Serializable]
    public class DateVariable : Variable
    {
        public DateVariable()
        {
            //  Do nothing
        }

        public DateVariable(DateTime[] data)
        {
            Data = data;
        }

        public DateVariable(DateTime[] data, string title)
        {
            Data = data;
            Title = title;
        }

        public DateVariable(int length, string title)
        {
            EnsureLength(length);
            Title = title;
        }

        ///  <summary>
        ///  Manage the entire data array at one time
        ///  </summary>
        ///  <value>The new data array to set</value>
        ///  <returns>The current data array</returns>
        ///  <remarks></remarks>
        public DateTime[] Data { get; set; }

        public override int Length
        {
            get { return (Data == null) ? 0 : Data.Length; }
        }

        public override void EnsureLength(int minimumLength)
        {
            if (Data == null)
                Data = new DateTime[minimumLength];
            else
            {
                if (Data.Length < minimumLength)
                {
                    DateTime[] temp = new DateTime[minimumLength];
                    Array.Copy(Data, temp, Data.Length);
                    Data = temp;
                }
            }
        }

        public void EnsureLength(int minimumLength, DateTime fillValue)
        {
            if (Data == null)
            {
                Data = new DateTime[minimumLength];
                for (int i = 0; i < minimumLength; i++)
                    Data[i] = fillValue;
            }
            else
            {
                if (Data.Length < minimumLength)
                {
                    int oldLength = Data.Length;
                    DateTime[] temp = new DateTime[minimumLength];
                    Array.Copy(Data, temp, Data.Length);
                    Data = temp;
                    for (int i = oldLength; i < minimumLength; i++)
                        Data[i] = fillValue;
                }
            }
        }

        public override void EnsureLength(int minimumLength, bool useMissing)
        {
            if (useMissing)
                EnsureLength(minimumLength, DateTime.MinValue);
            else
                EnsureLength(minimumLength);
        }

        public override void TruncateDataToLength(int maximumLength)
        {
            if ((Data.Length > maximumLength))
            {
                DateTime[] temp = new DateTime[maximumLength];
                Array.Copy(Data, temp, Math.Min(Data.Length, temp.Length));
                Data = temp;
            }
        }

        public override Variable SameSizeForResults()
        {
            Variable newVariable = new DoubleVariable();
            newVariable.EnsureLength(Length);
            return newVariable;
        }

        public override DateVariable AsDateVariable
        {
            get
            {
                return this;
            }
        }

        ///  <summary>
        ///  True if this Variable is a date variable; false if it is not
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public override bool IsDateVariable
        {
            get
            {
                return true;
            }
        }

        public override object CopyAndStripForRedo(bool shouldKeepData)
        {
            DateVariable copy = new DateVariable();
            CopyAndStripForRedoInto(copy, shouldKeepData);
            return copy;
        }

        protected void CopyAndStripForRedoInto(DateVariable copy, bool ShouldKeepData)
        {
            base.CopyAndStripForRedoInto(copy, ShouldKeepData);
            if (Origin == null || ShouldKeepData)
            {
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy.Data = Data;
            }
        }

        public override void StealDataFrom(Variable victim)
        {
            if (!(victim.IsDateVariable))
            {
                throw new InvalidCastException("Victim must be of the same type when stealing variables");
            }
            Data = victim.AsDateVariable.Data;
        }

        public override object DataAsObject(int i)
        {
            return Data[i];
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
