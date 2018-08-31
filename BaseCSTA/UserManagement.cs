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

using System.Collections.Generic;

namespace BaseCSTA
{
    public class GetAddressBook : CSTACommand
    {
        private int index = 0;

        public GetAddressBook()
        {
            commandName = "ablist";
            events = new Dictionary<string, object>()
            {
                {"ablist", null}
            };
            parameters = null;
        }

        public override string cmdBody()
        {
            return string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><iq type=\"get\" id=\"addressbook\" index=\"{0}\"></iq>", index);
        }
    }

    public class GetFavorites : CSTACommand
    {
        public GetFavorites()
        {
            commandName = "contact";
            events = new Dictionary<string, object>()
            {
                {"contact", null }
            };
            parameters = null;
        }

        public override string cmdBody()
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?><iq type=\"get\" id=\"roster\"></iq>";
        }
    }

    public class AddFavority : CSTACommand
    {
        public AddFavority()
        {
            commandName = "addContact";
            events = new Dictionary<string, object>();
            parameters = new Dictionary<string, string>()
            {
                {"userId", null }
            };
        }

        public override string cmdBody()
        {
            return string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><iq type=\"set\" id=\"buddy\" jid=\"{0}\"></iq>", parameters["userId"]);
        }
    }

    public class RemoveFavority : CSTACommand
    {
        public RemoveFavority()
        {
            commandName = "removeContact";
            events = new Dictionary<string, object>()
            {
                {"removeContact", null }
            };
            parameters = new Dictionary<string, string>()
            {
                {"userId", null }
            };
        }

        public override string cmdBody()
        {
            return string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><iq type=\"remove\" id=\"buddy\" jid=\"{0}\"></iq>", parameters["userId"]);
        }
    }

    public class Presence : CSTACommand
    {
        public Presence()
        {
            commandName = "presence";
            events = new Dictionary<string, object>()
            {
                {"presence", null }
            };
            parameters = new Dictionary<string, string>()
            {
                {"status", null },
                {"note", null }
            };
        }

        public override string cmdBody()
        {
            string note;

            if (parameters.ContainsKey("note"))
            {
                if (parameters["note"] != null)
                {
                    note = parameters["note"].ToString();
                    if (note.Length > 0)
                    {
                        note = string.Format("<presenceNote>{0}</presenceNote>", stringByEscapingCriticalXMLEntities(note));
                    }
                    else
                    {
                        note = "";
                    }
                }
                else
                {
                    note = "";
                }
            }
            else
            {
                note = "<presenceNote/>";
            }
            return string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><presence status=\"{0}\">{1}</presence>", parameters["status"], note);
        }
    }
}
