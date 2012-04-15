using System.Windows.Forms;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    /// <summary>
    /// A small interface for controls that spit their results into an existing parameter bag.
    /// Almost always used instead of IOkable.
    /// </summary>
    internal interface IFillParameterBag
    {
        /// <param name="outputParameters">The existing parameter bag into which the control should add its values.</param>
        /// <returns>the failed control to be selected if the output is known to be invalid (to the point that the UI should insist on the user continuing to fill this in), null otherwise</returns>
        Control Fill(ParameterBag outputParameters);
    }
}
