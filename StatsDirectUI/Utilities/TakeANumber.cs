using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.Utilities
{
    /// <summary>
    /// Hands back numbers that are known to be distinct and monotonically increasing.  No other guarantees are provided.
    /// </summary>
    public sealed class TakeANumber
    {
        private int currentValue;

        public int Next()
        {
            return currentValue++;
        }
    }
}
