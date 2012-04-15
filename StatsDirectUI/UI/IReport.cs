using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public interface IReport: IForm
    {
        string RtfText
        {
            get;
            set;
        }

        void AppendRtfText(string rtf, int helpContextId, Operation operation, string redoInformation);
    }
}
