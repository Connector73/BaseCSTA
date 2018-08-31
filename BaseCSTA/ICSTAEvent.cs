//*********************************************************
//
// Copyright (c) Connector73. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;

namespace BaseCSTA
{
    public interface ICSTAEvent
    {
        event EventHandler OnEvent;
    }

    public interface ICSTAErrorEvent
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
