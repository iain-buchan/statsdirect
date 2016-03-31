using System.Xml.Serialization;
using System;

namespace StatsDirect.Data
{
    ///  <summary>
    ///  Represents a single non-classifier variable/factor/column/field.
    ///  Use ClassiferVariable to represent a classifier variable.
    ///  </summary>
    [Serializable]
    public abstract class Variable
    {
        ///  <summary>
        ///  The title (name) of the variable
        ///  </summary>
        [XmlElement("title")]
        public string Title { get; set; }

        ///  <summary>
        ///  Where the variable came from
        ///  </summary>
        [XmlIgnore]
        public IOrigin Origin { get; set; }

        [XmlElement("worksheet-origin", typeof(WorksheetOrigin))]
        public object OriginForXml
        {
            get
            {
                return Origin;
            }
            set
            {
                Origin = ((IOrigin)(value));
            }
        }

        [XmlIgnore]
        public abstract int Length { get; }

        ///  <summary>
        ///  Ensure the data array is allocated and at least MinimumLength items in length.  Any new elements will be filled with the platform default value.
        ///  </summary>
        ///  <param name="minimumLength">The minimum length of the array.  Note this is a length, not a bound.  The array will have items from 0 to MinimumLength - 1.</param>
        public abstract void EnsureLength(int minimumLength);

        ///  <summary>
        ///  Ensure the data array is allocated and at least MinimumLength items in length.  Any new elements will be filled with the platform default value if UseMissing is false, or with Constant.Missing if UseMissing is true.
        ///  </summary>
        ///  <param name="minimumLength">The minimum length of the array.  Note this is a length, not a bound.  The array will have items from 0 to MinimumLength - 1.</param>
        /// <param name="useMissing"> </param>
        public abstract void EnsureLength(int minimumLength, bool useMissing);

        ///  <summary>
        ///  Ensure the data has at most MaximumLength rows
        ///  </summary>
        ///  <param name="maximumLength"></param>
        public abstract void TruncateDataToLength(int maximumLength);

        ///  <summary>
        ///  Return a new numeric variable of the same length as me.  It is not otherwise initialised.
        ///  </summary>
        public abstract Variable SameSizeForResults();

        public abstract object CopyAndStripForRedo(bool shouldKeepData);

        ///  <summary>
        ///  A fast but destructive way of transferring victim's data to this variable.  Victim should not be used after this operation.
        ///  </summary>
        public abstract void StealDataFrom(Variable victim);

        protected virtual void CopyAndStripForRedoInto(Variable copy, bool shouldKeepData)
        {
            copy.Title = Title;
            copy.Origin = Origin;
        }

        ///  <summary>
        ///  If true, this variable contains its data (and implicitly doesn't need refilling from its origin)
        ///  If false, this variable contains no data and may need to be refilled.
        ///  </summary>
        protected abstract bool HasData { get; }

        /// <summary>
        /// Polymorphism: return the ith element of this variable's data encapsulated as an object.
        /// </summary>
        public abstract object DataAsObject(int i);

        public abstract void Accept(IVariableVisitor visitor);
    }
}
