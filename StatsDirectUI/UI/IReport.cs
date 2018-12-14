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

        void AppendRenderable(IRenderable renderable, int helpContextId, Operation operation, string redoInformation);
    }
}
