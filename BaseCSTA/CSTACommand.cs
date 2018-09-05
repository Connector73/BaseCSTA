﻿//*********************************************************
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
using System.Security;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace BaseCSTA
{
    public class CSTACommand
    {
        public string commandName
        {
            get; set;
        }
        public string eventName
        {
            get; set;
        }
        public Dictionary<string, string> parameters
        {
            get; set;
        }
        public Dictionary<string, object> events
        {
            get; set;
        }
        virtual public string cmdBody()
        {
            return null;
        }

        public string stringByEscapingCriticalXMLEntities(string text)
        {
            return SecurityElement.Escape(text);
        }

        public string stringByUnescapingCrititcalXMLEntities(string text)
        {
            string mutable = text.Replace("&amp;", "&");
            mutable = mutable.Replace("&lt;", "<");
            mutable = mutable.Replace("&gt;", ">");
            mutable = mutable.Replace("&#x27;", "'");
            mutable = mutable.Replace("&quot;", "\"");
            return mutable;
        }

        public override string ToString()
        {
            string outValue = "\nDump:" + commandName + "\n";
            if (eventName != null)
            {
                foreach(var pair in (Dictionary<string, object>)events[eventName])
                {
                    outValue += String.Format("  {0} <- {1}\n", pair.Key, pair.Value);
                }
            }
            return outValue;
        }
    }

    public class KeepAliveCommand : CSTACommand
    {
        public KeepAliveCommand()
        {
            commandName = "keepalive";
        }

        public override string cmdBody()
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?><keepalive/>";
        }
    }

    public class LoginCommand : CSTACommand
    {
        private bool _clearTextPassword = false;
        public bool clearTextPassword
        {
            get
            {
                return _clearTextPassword;
            }

            set
            {
                _clearTextPassword = value;
            }
        }

        public LoginCommand()
        {
            commandName = "loginRequest";
            events = new Dictionary<string, object>()
            {
                {"loginResponce", null},
                {"loginFailed", null},
                {"Logout", null}
            };
            parameters = new Dictionary<string, string>()
            {
                {"type", null },
                {"platform", null },
                {"version", null },
                {"userName", null },
                {"pwd", null }
            };
        }

        private string encodePassword(string password)
        {
            IBuffer buffUtf8Msg = CryptographicBuffer.ConvertStringToBinary(password, BinaryStringEncoding.Utf8);

            // Create a HashAlgorithmProvider object.
            HashAlgorithmProvider objAlgProv = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);

            // Demonstrate how to retrieve the name of the hashing algorithm.
            String strAlgNameUsed = objAlgProv.AlgorithmName;

            // Hash the message.
            IBuffer buffHash = objAlgProv.HashData(buffUtf8Msg);

            // Verify that the hash length equals the length specified for the algorithm.
            if (buffHash.Length != objAlgProv.HashLength)
            {
                throw new Exception("There was an error creating the hash");
            }

            // Convert the hash to a string.
            String strHashBase64 = CryptographicBuffer.EncodeToBase64String(buffHash);

            // Return the encoded string
            return String.Format("{0}\n", strHashBase64);
        }

        public override string cmdBody()
        {
            string password = _clearTextPassword ? parameters["pwd"] : encodePassword(parameters["pwd"]);
            string cmdText = string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><loginRequest type=\"{0}\" platform=\"{1}\" version=\"{2}\" push_ntf=\"false\" push_token=\"\" push_clean=\"flase\" push_bundle_id=\"\"><userName>{3}</userName><pwd>{4}</pwd></loginRequest>",
                parameters["type"], parameters["platform"], parameters["version"], stringByEscapingCriticalXMLEntities(parameters["userName"]), password);
            return cmdText;
        }
    }
}
