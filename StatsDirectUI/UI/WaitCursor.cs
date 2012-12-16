using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public class WaitCursor : IDisposable
    {
        private readonly Cursor m_cursorOld;

        public WaitCursor()
        {
            m_cursorOld = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
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