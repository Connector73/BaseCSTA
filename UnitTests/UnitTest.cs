
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using BaseCSTA;

namespace UnitTests
{
    [TestClass]
    public class BaseTests
    {
        Messenger csta;
        string myJid;

        [TestInitialize]
        public void init()
        {
            csta = new Messenger();
            ICSTAEvent e = (ICSTAEvent)csta;
            e.OnEvent += E_OnEvent;
            myJid = null;
        }

        private string ListValues(Dictionary<string, object> args, string tab)
        {
            string outValue = "";
            foreach (var pair in args)
            {
                if (pair.Value is List<object>)
                {
                    List<object> list = (List<object>)pair.Value;
                    foreach (Dictionary<string, object> item in list)
                    {
                        outValue += "\n" + pair.Key + ":";
                        outValue += "\n" + ListValues(item, tab + "  ");
                    }
                }
                else
                {
                    outValue += String.Format("{0}{1} <- {2}\n", tab, pair.Key, pair.Value);
                }
            }
            return outValue;
        }

        private async void E_OnEvent(object sender, EventArgs e)
        {
            Debug.WriteLine("Event", ((CSTAEventArgs)e).eventName);
            if (((CSTAEventArgs)e).parameters != null)
            {
                Dictionary<string, object> args = (Dictionary<string, object>)((CSTAEventArgs)e).parameters;
                string outValue = ListValues(args, "  ");
                Debug.WriteLine(outValue);

                if (((CSTAEventArgs)e).eventName == "message")
                {
                    if (args["delivered"].ToString() == "false")
                    {
                        await csta.ExecuteHandler("messageAck", new Dictionary<string, string>()
                        {
                            {"userId", myJid },
                            {"msgId", args["msgId"].ToString() },
                            {"reqId", args["reqId"].ToString() }
                        });
                        Debug.WriteLine("Send ACK for Message");
                    }
                } else if (((CSTAEventArgs)e).eventName == "loginResponce")
                {
                    myJid = args["userId"].ToString();
                }
            }
        }

        [TestMethod]
        public void TestLogin()
        {
            Task v = csta.Connect("631hc.connector73.net", "7778", ConnectType.Secure);
            v.Wait();
            csta.AddHandler(new Presence());
            csta.Login("maximd", "ihZ6nW62").Wait();
            Task.Delay(TimeSpan.FromSeconds(20)).Wait();
            csta.Disconnect();
        }

        [TestMethod]
        public void TestLoginFail()
        {
            Task v = csta.Connect("631hc.connector73.net", "7778", ConnectType.Secure);
            v.Wait();
            csta.AddHandler(new Presence());
            csta.Login("maximd", "12345").Wait();
            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            csta.Disconnect();
        }

        [TestMethod]
        public void TestAddressBook()
        {
            Task v = csta.Connect("631hc.connector73.net", "7778", ConnectType.Secure);
            v.Wait();
            csta.AddHandler(new GetAddressBook());
            csta.AddHandler(new Presence());
            csta.Login("maximd", "ihZ6nW62").Wait();
            Task.Delay(TimeSpan.FromSeconds(10)).Wait();

            v = csta.ExecuteHandler("ablist", null);
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            csta.Disconnect();
        }

        [TestMethod]
        public void TestBuddyList()
        {
            Task v = csta.Connect("631hc.connector73.net", "7778", ConnectType.Secure);
            v.Wait();
            csta.AddHandler(new GetFavorites());
            csta.AddHandler(new AddFavority());
            csta.AddHandler(new RemoveFavority());
            csta.AddHandler(new Presence());
            csta.Login("maximd", "ihZ6nW62").Wait();
            Task.Delay(TimeSpan.FromSeconds(5)).Wait();

            Debug.WriteLine("Initial:");

            v = csta.ExecuteHandler("contact", null);
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            Debug.WriteLine("Add Favority");

            v = csta.ExecuteHandler("addContact", new Dictionary<string, string>() { { "userId", "43884632217388952" } });
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            Debug.WriteLine("After Add:");

            v = csta.ExecuteHandler("contact", null);
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            Debug.WriteLine("Remove Favority");

            v = csta.ExecuteHandler("removeContact", new Dictionary<string, string>() { { "userId", "43884632217388952" } });
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            Debug.WriteLine("Final:");

            v = csta.ExecuteHandler("contact", null);
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            Debug.WriteLine("Change status to BeRightBack");

            v = csta.ExecuteHandler("presence", new Dictionary<string, string>() { { "status", "BeRightBack" } });
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            Debug.WriteLine("Add Note");

            v = csta.ExecuteHandler("presence", new Dictionary<string, string>() { { "status", "BeRightBack" }, { "note", "Test note" } });
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            Debug.WriteLine("Remove Note");

            v = csta.ExecuteHandler("presence", new Dictionary<string, string>() { { "status", "Available" } });
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            csta.Disconnect();
        }

        [TestMethod]
        public void TestIntantMessaging()
        {
            Task v = csta.Connect("631hc.connector73.net", "7778", ConnectType.Secure);
            v.Wait();
            csta.AddHandler(new MessageHistory());
            csta.AddHandler(new MessageAck());
            csta.AddHandler(new SendMessage());
            csta.Login("maximd", "ihZ6nW62").Wait();
            Task.Delay(TimeSpan.FromSeconds(5)).Wait();

            Debug.WriteLine("Get IM History");
            DateTime dt = DateTime.Now.AddMinutes(-10).ToUniversalTime();
            v = csta.ExecuteHandler("messageHist", new Dictionary<string, string>() { { "timestamp", dt.ToString() } });
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(20)).Wait();
            Debug.WriteLine("Send Message");

            v = csta.ExecuteHandler("message", new Dictionary<string, string>()
            {
                {"userId", myJid }, // Send to Test User
                {"ext", "" },
                {"text", "This is a test messages via new CSTA library..." }
            });
            v.Wait();

            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            csta.Disconnect();
        }
    }
}

