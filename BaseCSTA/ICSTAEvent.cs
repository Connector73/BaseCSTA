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
        public int seqNumber;
        public Dictionary<string, object> parameters;

        public CSTAEventArgs(string name, int seqence, Dictionary<string, object> args)
        {
            eventName = name;
            seqNumber = seqence;
            parameters = args;
        }
    }
}
