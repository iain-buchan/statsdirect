using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.Numerics
{
    public class Utilities
    {
        /// <summary>
        /// Returns an array of (potentially re-based) double[] in the same order as inputs, but with rows removed where any of the input values on a row are missing.
        /// </summary>
        /// <param name="inputs">Array of double[] that are to be checked for missing data.  These arrays are unchanged at end of method.</param>
        /// <param name="inputBase">The lowest index of each array in inputs.  Typically 0 or 1.</param>
        /// <param name="inputLength">The number of valid rows in each array in inputs.  Precondition: inputLength + inputBase <= inputs[i].Length for all valid i</param>
        /// <param name="outputBase">The index at which the first row with non-missing data will be emitted in the returns.  Typically 0 or 1; often used for re-basing 0-based inputs from the UI to 1-based outputs for SD functions.</param>
        /// <returns></returns>
        public static double[][] RemoveMissingRows(double[][] inputs, int inputBase, int inputLength, int outputBase)
        {
            // Implemented in two passes, so that we know how large to make the output arrays rather than blindly copying then having to re-copy.

            // Pass 1: Accumulate the total number of valid rows, plus an array of which ones are valid.
            bool[] shouldCopyRow = new bool[inputs[0].Length];
            int validRows = 0;
            for (int inputRow = inputBase; inputRow < inputBase + inputLength; inputRow++)
            {
                bool shouldCopy = true;
                foreach (double[] ary in inputs)
                {
                    if (ary[inputRow] == Constant.MISSING)
                    {
                        shouldCopy = false;
                        break;
                    }
                }
                if (shouldCopy)
                    validRows++;
                shouldCopyRow[inputRow] = shouldCopy;
            }

            // Pass 2: Allocate the output arrays and copy the valid rows.
            double[][] outputs = new double[inputs.Length][];
            for (int ary = 0; ary < inputs.Length; ary++)
                outputs[ary] = new double[validRows + outputBase];
            int outputRow = outputBase;
            for (int inputRow = inputBase; inputRow < inputBase + inputLength; inputRow++)
            {
                if (shouldCopyRow[inputRow])
                {
                    for (int ary = 0; ary < inputs.Length; ary++)
                        outputs[ary][outputRow] = inputs[ary][inputRow];
                    outputRow++;
                }
            }
            return outputs;
        }
    }
}
