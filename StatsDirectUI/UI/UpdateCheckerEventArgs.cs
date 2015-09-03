using System;

namespace StatsDirect.UI
{
    class UpdateCheckerEventArgs : EventArgs
    {
        public bool IsFinal { get; set; }
        public bool Succeeded { get; set; }
        public bool NewerVersionAvailable { get; set; }
        public string Message { get; set; }
    }
}
