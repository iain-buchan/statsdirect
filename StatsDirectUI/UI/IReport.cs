using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public interface IReport: IForm
    {
        void AppendRenderable(IRenderable renderable, int helpContextId, Operation operation, object run);   // run: the run of the operation; its outputs make one item
    }
}
