using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public class DefaultCursor : IDisposable
    {
        private readonly Cursor m_cursorOld;

        public DefaultCursor()
        {
            m_cursorOld = Cursor.Current;
            Cursor.Current = Cursors.Default;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposeManaged)
        {
            Cursor.Current = m_cursorOld;
        }
    }
}