using System.Collections.Generic;

namespace StatsDirect.Data
{
    ///  <summary>
    ///  Data providers that can refill freeze-dried data in IStripForRedo should implement this interface.
    ///  </summary>
    ///  <remarks>Strictly, this should be part of the template system.  Unfortunately template requires data, and this would require a circular dependency.</remarks>
    public interface IRefillSource
    {
        ///  <summary>
        ///  Request that each variable in variables is filled from its origin, taking into account that some (but not all) may have been acquired together and hence need a common length.
        ///  </summary>
        ///  <param name="v">The variable to be refilled</param>
        void Refill(IList<Variable> variables);
    }
}
