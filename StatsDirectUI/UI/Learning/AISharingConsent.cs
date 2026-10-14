using System.Collections.Generic;
using System.Linq;

namespace StatsDirect.UI.Learning;

/// <summary>App-session acceptance, separate from each question's selected documents.</summary>
internal sealed class AISharingConsent
{
    internal string DataDocumentId { get; private set; }
    internal bool Accepted { get; private set; }
    internal int Revision { get; private set; }
    private readonly HashSet<string> approvedDataIds = new();
    internal bool SelectDataDocument(string id)
    {
        if (DataDocumentId == id) return false;
        DataDocumentId = id; Revoke(); return true;
    }
    internal bool Accepts(IEnumerable<string> dataIds) => Accepted && dataIds.All(approvedDataIds.Contains);
    internal void Approve(IEnumerable<string> dataIds)
    {
        Accepted = true; approvedDataIds.UnionWith(dataIds);
        if (DataDocumentId != null) approvedDataIds.Add(DataDocumentId);
    }
    internal void Revoke() { Accepted = false; approvedDataIds.Clear(); Revision++; }
}
