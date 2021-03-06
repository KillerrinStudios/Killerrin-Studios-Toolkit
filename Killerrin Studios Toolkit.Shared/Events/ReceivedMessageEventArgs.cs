﻿using KillerrinStudiosToolkit.Datastructures;
using KillerrinStudiosToolkit.Enumerators;
using System;
using System.Collections.Generic;
using System.Text;

namespace KillerrinStudiosToolkit.Events
{
    public delegate void ReceivedMessageEventHandler(object sender, ReceivedMessageEventArgs e);

    public class ReceivedMessageEventArgs : System.ComponentModel.AsyncCompletedEventArgs
    {
        public string Message { get; private set; }
        public NetworkConnectionEndpoint? NetworkConnectionEndpoint { get; private set; }
        public NetworkType NetworkType { get; private set; }

        public ReceivedMessageEventArgs()
            : base(new Exception(), false, null)
        {
            Message = "";
            NetworkConnectionEndpoint = null;
        }

        public ReceivedMessageEventArgs(string message, NetworkConnectionEndpoint? networkConnectionEndpoint, NetworkType networkType)
            : base(new Exception(), false, null)
        {
            Message = message;
            NetworkConnectionEndpoint = networkConnectionEndpoint;
            NetworkType = networkType;
        }

        public ReceivedMessageEventArgs(string message, NetworkConnectionEndpoint? networkConnectionEndpoint, NetworkType networkType, Exception e, bool canceled, Object state)
            : base(e, canceled, state)
        {
            Message = message;
            NetworkConnectionEndpoint = networkConnectionEndpoint;
            NetworkType = networkType;
        }
    }
}
