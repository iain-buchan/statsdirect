namespace StatsDirect.UI
{
    public interface IScriptWindow: IForm
    {
        string RtfText
        {
            get;
            set;
        }

        string TextInRtfBox
        {
            get;
            set;
        }

        void AppendRtfText(string rtf);
    }
}
