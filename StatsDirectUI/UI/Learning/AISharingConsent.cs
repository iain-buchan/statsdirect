using System.Collections.Generic;

namespace StatsDirect.UI.Learning;

/// <summary>App-session acceptance, separate from each question's selected documents.</summary>
internal sealed class AISharingConsent
{
    internal string DataDocumentId { get; private set; }
    internal int Revision { get; private set; }
    private readonly HashSet<string> approvedDocumentIds = new();
    internal bool SelectDataDocument(string id)
    {
        if (DataDocumentId == id) return false;
        DataDocumentId = id; Revoke(); return true;
    }
    internal bool Accepts(IEnumerable<string> documentIds)
    {
        bool hasDocuments = false;
        foreach (string id in documentIds)
        {
            if (!approvedDocumentIds.Contains(id)) return false;
            hasDocuments = true;
        }
        return hasDocuments;
    }

    internal void Approve(IEnumerable<string> documentIds) => approvedDocumentIds.UnionWith(documentIds);
    internal void Revoke() { approvedDocumentIds.Clear(); Revision++; }
}
