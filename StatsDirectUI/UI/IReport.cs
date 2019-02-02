using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public interface IReport: IForm
    {
        void AppendRenderable(IRenderable renderable, int helpContextId, Operation operation, string redoInformation);
    }
}
