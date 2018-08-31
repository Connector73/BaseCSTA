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
    public class SendMessage : CSTACommand
    {
        public SendMessage()
        {
            commandName = "message";
            events = new Dictionary<string, object>()
            {
                {"message", null }
            };
            parameters = new Dictionary<string, string>()
            {
                {"userId", null},
                {"ext", null },
                {"text", null },
                {"messageId", null }
            };
        }

        private string generateMessageId()
        {
            Guid g = Guid.NewGuid();
            int msgId = g.ToString().GetHashCode();
            return msgId.ToString();
        }

        public override string cmdBody()
        {
            return string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><message to=\"{0}\" msgId=\"{1}\" ext=\"{2}\">{3}</message>",
                parameters["userId"], generateMessageId(), parameters["ext"], stringByEscapingCriticalXMLEntities(parameters["text"]));
        }
    }

    public class MessageAck : CSTACommand
    {
        public MessageAck()
        {
            commandName = "messageAck";
            events = new Dictionary<string, object>()
            {
                {"messageAck", null }
            };
            parameters = new Dictionary<string, string>()
            {
                {"userId", null },
                {"msgId", null },
                {"reqId", null }
            };
        }

        public override string cmdBody()
        {
            return string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><messageAck from=\"{0}\" msgId=\"{1}\" reqId=\"{2}\"></messageAck>", 
                parameters["userId"], parameters["msgId"], parameters["reqId"]);
        }
    }

    public class MessageHistory : CSTACommand
    {
        public MessageHistory()
        {
            commandName = "messageHist";
            events = new Dictionary<string, object>()
            {
                {"getImHistoryResponse", null },
                {"messageHist", null }
            };
            parameters = new Dictionary<string, string>()
            {
                {"timestamp", null }
            };
        }
        
        private double ConvertToUnixTimestamp(DateTime date)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0).ToUniversalTime();
            TimeSpan span = (date.ToUniversalTime() - epoch);
            return span.TotalSeconds;
        }

        private string timestampStr()
        {
            DateTime dt;
            try
            {
                dt = Convert.ToDateTime(parameters["timestamp"]);
                return ConvertToUnixTimestamp(dt).ToString();
            }
            catch (Exception ex)
            {
                return parameters["timestamp"];
            }
        }
        
        public override string cmdBody()
        {
            return string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><getImHistory timestamp=\"{0}\"></getImHistory>", timestampStr());
        }
    }
}
