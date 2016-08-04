using System;
using System.Xml.Serialization;

using StatsDirect.Utilities;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class GridParameter: Parameter
    {
        private bool columnsAreSameLength;
        private List<string> sameLengthAsParameter;
        private Expression length;
        private Expression minimumColumns;
        private Expression maximumColumns;
        private DataAcquisitionMode dataAcquisitionMode;
        private bool shouldClearSelectionFirst;
        private bool shouldAskForGroupId;
        private GroupIdentifierMode groupIdentifierMode;
        private string appendToFrame;
        private bool canSelect = true;

        /// <summary>
        /// If true, the grid can be selected from existing data (default).
        /// If false, the grid must be entered interactively.
        /// </summary>
        [XmlElement(ElementName="can-select")]
        public bool CanSelect
        {
            get { return canSelect; }
            set { canSelect = value; }
        }

        /// <summary>
        /// If true, all columns selected must be of the same length.
        /// If false, columns may be of mixed lengths.
        /// </summary>
        [XmlElement(ElementName="same-length")]
        public bool ColumnsAreSameLength
        {
            get { return columnsAreSameLength; }
            set { columnsAreSameLength = value; }
        }

        /// <summary>
        /// The mode in which the data should be loaded into the frame
        /// </summary>
        [XmlElement(ElementName = "mode")]
        public DataAcquisitionMode DataAcquisitionMode
        {
            get { return dataAcquisitionMode; }
            set { dataAcquisitionMode = value; }
        }

        /// <summary>
        /// If non-null and non-blank, all columns selected must be of the same length as the first column in the specified parameter.
        /// If null or blank, columns are not restricted.
        /// If multiple names are specified, they are checked in order and the first variable that is found by name is used for the length test.
        /// </summary>
        [XmlElement(ElementName = "same-length-as")]
        public List<string> SameLengthAsParameter
        {
            get { return sameLengthAsParameter; }
            set { sameLengthAsParameter = value; }
        }

        /// <summary>
        /// If non-null and non-blank, variables are appended to the existing frame with this name.
        /// If null or blank, variables are added to a new frame.
        /// </summary>
        [XmlElement(ElementName = "append-to-frame")]
        public string AppendToFrame
        {
            get { return appendToFrame; }
            set { appendToFrame = value; }
        }

        /// <summary>
        /// The exact number of rows that must be selected with this operation, or null for any number.
        /// </summary>
        public int Length(ITemplateProcessor processor, ParameterBag parameters)
        {
            return (int)processor.Evaluate(length, parameters);
        }

        /// <summary>
        /// true iff the parameter defines the exact number of rows that must be selected with this operation.
        /// </summary>
        public bool HasLength
        {
            get { return null != length; }
        }

        /// <summary>
        /// The exact number of rows that must be selected with this operation, or null for any number.
        /// </summary>
        [XmlElement(ElementName = "length")]
        public Expression LengthExpression
        {
            get { return length; }
            set
            {
                length = value;
            }
        }

        /// <summary>
        /// The smallest number of columns that may be selected with this operation.
        /// Must be less than or equal to MaximumColumns.
        /// </summary>
        public int MinimumColumns(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == minimumColumns)
                return 1;
            return (int)processor.Evaluate(minimumColumns, parameters);
        }

        /// <summary>
        /// The smallest number of columns that may be selected with this operation.
        /// Must be less than or equal to MaximumColumns.
        /// </summary>
        [XmlElement(ElementName = "min-columns")]
        public Expression MinimumColumnsExpression
        {
            get { return minimumColumns; }
            set
            {
                minimumColumns = value;
            }
        }

        /// <summary>
        /// The largest number of columns that may be selected with this operation.
        /// Must be greater than or equal to MinimumColumns.
        /// </summary>
        public int MaximumColumns(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == maximumColumns)
                return int.MaxValue;
            return (int)processor.Evaluate(maximumColumns, parameters);
        }

        /// <summary>
        /// The expression for the largest number of columns that may be selected with this operation.
        /// </summary>
        [XmlElement(ElementName = "max-columns")]
        public Expression MaximumColumnsExpression
        {
            get { return maximumColumns; }
            set
            {
                maximumColumns = value;
            }
        }

        /// <summary>
        /// If true, UIs should clear the user's selection before trying to obtain this parameter.
        /// If false, pre-existing selections should be honoured.
        /// </summary>
        [XmlElement(ElementName = "clear-selection-first")]
        public bool ShouldClearSelectionFirst
        {
            get { return shouldClearSelectionFirst; }
            set { shouldClearSelectionFirst = value; }
        }

        /// <summary>
        /// If true, UIs should allow selection by group ID (if they permit this).
        /// If false, UIs should only allow selection by value.
        /// </summary>
        [XmlElement(ElementName = "ask-for-group-id")]
        public bool ShouldAskForGroupId
        {
            get { return shouldAskForGroupId; }
            set { shouldAskForGroupId = value; }
        }

        /// <summary>
        /// If ShouldAskForGroupId is true, how should groups be selected?
        /// </summary>
        [XmlElement(ElementName = "group-id-mode")]
        public GroupIdentifierMode GroupIdentifierMode
        {
            get { return groupIdentifierMode; }
            set { groupIdentifierMode = value; }
        }

        public override bool RequiresGrid
        {
            get
            {
                return canSelect;
            }
        }

        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return (MustRequest || null != Name && null != parameters && !parameters.ContainsKey(Name)) ? InputDuringStep.Always : InputDuringStep.Never;
        }

        public override void Accept(IParameterVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override ParameterBag AllDefaults(ITemplateProcessor processor, ParameterBag context)
        {
            return new ParameterBag();
        }
    }
}
