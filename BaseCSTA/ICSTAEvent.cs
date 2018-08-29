using System;
using System.Collections.Generic;

namespace BaseCSTA
{
    public interface ICSTAEvent
    {
        event EventHandler OnEvent;
    }

    public class CSTAEventArgs : EventArgs
    {
        public string eventName;
        public Dictionary<string, object> parameters;

        public CSTAEventArgs(string name, Dictionary<string, object> args)
        {
            eventName = name;
            parameters = args;
        }
    }
}
