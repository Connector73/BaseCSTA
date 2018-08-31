using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Core;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Security.Cryptography.Certificates;
using System.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.System.Threading;
using Windows.UI.Core;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Web;

namespace BaseCSTA
{
    public enum ConnectType { Plain, Secure, WebSocket, WebSocketSecure };

    public class Messenger : ICSTAEvent
    {
        private byte[] readBuffer;
        private ThreadPoolTimer _timer = null;

        private Dictionary<string, object> commands = new Dictionary<string, object>()
        {
            { "login", new LoginCommand() }
        };

        event EventHandler cstaEvent;
        private object objectLock = new Object();

        event EventHandler ICSTAEvent.OnEvent
        {
            add
            {
                lock (objectLock)
                {
                    cstaEvent += value;
                }
            }
            remove
            {
                lock (objectLock)
                {
                    cstaEvent -= value;
                }
            }
        }

        public void AddHandler(CSTACommand handler)
        {
            lock(commands)
            {
                commands.Add(handler.commandName, handler);
            }
        }

        public void RemoveHandler(string cmdName)
        {
            lock(commands)
            {
                if (commands.ContainsKey(cmdName))
                {
                    commands.Remove(cmdName);
                }
            }
        }

        public async Task<int> ExecuteHandler(string cmdName, Dictionary<string, string> parameters)
        {
            object cmd;
            if (commands.TryGetValue(cmdName, out cmd))
            {
                CSTACommand command = (CSTACommand)cmd;
                command.parameters = parameters;
                int sequence = await sendText(command.cmdBody());
                return sequence;
            }
            return -1;
        }

        /// <summary>
        /// Connect to MX server
        /// </summary>
        /// <param name="host">The MX server host address</param>
        /// <param name="port">The MX server CSTA port number 7777 or 7778 for TLS</param>
        /// <param name="useTls">Use TLS for connection to server</param>
        /// <returns>False if the connection is not set</returns>
        public async Task<bool> Connect(string host, string port, ConnectType type)
        {
            if (CoreApplication.Properties.ContainsKey("clientSocket"))
            {
                return false;
            }

            if (type == ConnectType.WebSocketSecure)
            {
                return false;
            }

            HostName hostName;
            try
            {
                hostName = new HostName(host);
            }
            catch (ArgumentException)
            {
                return false;
            }

            StreamSocket socket = null; // = new StreamSocket();
            StreamWebSocket streamWebSocket = null;

            switch (type)
            {
                case ConnectType.Plain:
                    try
                    {
                        socket = new StreamSocket();
                        socket.Control.KeepAlive = false;

                        // Connect to the server (by default, the listener we created in the previous step).
                        await socket.ConnectAsync(hostName, port);

                        // Mark the socket as connected. Set the value to null, as we care only about the fact that the 
                        // property is set.
                    }
                    catch (Exception exception)
                    {
                        // If this is an unknown status it means that the error is fatal and retry will likely fail.
                        if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                        {
                            throw;
                        }
                        return false;
                    }
                    break;
                case ConnectType.Secure:
                    socket = new StreamSocket();
                    socket.Control.KeepAlive = false;
                    socket.Control.ClientCertificate = null;
                    bool shouldRetry = await TryConnectSocketWithRetryAsync(socket, hostName, port);
                    if (shouldRetry)
                    {
                        // Retry if the first attempt failed because of SSL errors.
                        if (await TryConnectSocketWithRetryAsync(socket, hostName, port))
                            return false;
                    }
                    break;

                case ConnectType.WebSocket:
                    try
                    {
                        streamWebSocket = new StreamWebSocket();

                        Uri server = new Uri(string.Format("ws://{0}:{1}", host, port));
                        // Dispatch close event on UI thread. This allows us to avoid synchronizing access to streamWebSocket.
                        streamWebSocket.Closed += async (senderSocket, args) =>
                        {
                            var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
                            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Closed(senderSocket, args));
                        };

                        await streamWebSocket.ConnectAsync(server);

                    }
                    catch (Exception ex)
                    {
                        WebErrorStatus status = WebSocketError.GetStatus(ex.GetBaseException().HResult);

                        switch (status)
                        {
                            case WebErrorStatus.CannotConnect:
                            case WebErrorStatus.NotFound:
                            case WebErrorStatus.RequestTimeout:
                                break;

                            case WebErrorStatus.Unknown:
                                throw;

                            default:
                                break;
                        }
                    }
                    break;
            }

            // Save the socket, so subsequent steps can use it.
            CoreApplication.Properties.Add("connected", null);

            readBuffer = new byte[64 * 1024];

            if (type == ConnectType.WebSocket)
            {
                CoreApplication.Properties.Add("clientSocket", streamWebSocket);
                // Start a background task to continuously read for incoming data
                Task receiving = Task.Factory.StartNew(ReceiveDataLoop,
                    streamWebSocket.InputStream.AsStreamForRead(), TaskCreationOptions.LongRunning);

            }
            else
            {
                CoreApplication.Properties.Add("clientSocket", socket);
                // Start a background task to continuously read for incoming data
                Task receiving = Task.Factory.StartNew(ReceiveDataLoop,
                    socket.InputStream.AsStreamForRead(), TaskCreationOptions.LongRunning);
            }

            this._timer = ThreadPoolTimer.CreatePeriodicTimer(_timerEvent, TimeSpan.FromSeconds(KeepAliveTimeout));
            return true;
        }

        /// <summary>
        /// Internal function for setup TLS connection
        /// </summary>
        /// <param name="socket">Connection socket</param>
        /// <param name="hostName">Host name</param>
        /// <param name="port">Port number</param>
        /// <returns>False if the connection should be repeated</returns>
        private async Task<bool> TryConnectSocketWithRetryAsync(StreamSocket socket, HostName hostName, string port)
        {
            try
            {
                // Establish a secure connection to the server (by default, the local IIS server).
                await socket.ConnectAsync(hostName, port, SocketProtectionLevel.Tls12);

                string certInformation = GetCertificateInformation(
                    socket.Information.ServerCertificate,
                    socket.Information.ServerIntermediateCertificates);

                return false;
            }
            catch (Exception exception)
            {
                // If this is an unknown status it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }

                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.HostNotFound)
                {
                    return true;
                }

                // If the exception was caused by an SSL error that is ignorable we are going to prompt the user
                // with an enumeration of the errors and ask for permission to ignore.
                if (socket.Information.ServerCertificateErrorSeverity != SocketSslErrorSeverity.Ignorable)
                {
                    return true;
                }
            }

            // Present the certificate issues and ask the user if we should continue.
            if (await ShouldIgnoreCertificateErrorsAsync(socket.Information.ServerCertificateErrors))
            {
                // -----------------------------------------------------------------------------------------------
                // WARNING: Only test applications should ignore SSL errors.
                // In real applications, ignoring server certificate errors can lead to Man-In-The-Middle attacks.
                // -----------------------------------------------------------------------------------------------
                socket.Control.IgnorableServerCertificateErrors.Clear();
                foreach (var ignorableError in socket.Information.ServerCertificateErrors)
                {
                    socket.Control.IgnorableServerCertificateErrors.Add(ignorableError);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        // This may be triggered remotely by the server or locally by Close/Dispose()
        private void Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            if (sender != null)
            {
                sender.Dispose();

                // Distroy keepalive timer before close connection
                if (_timer != null)
                {
                    _timer.Cancel();
                    _timer = null;
                }

                CoreApplication.Properties.Remove("connected");
                CoreApplication.Properties.Remove("clientSocket");
            }

       }
   
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        /// <summary>
        /// Allows the user to abort the connection in case of SSL/TLS errors
        /// </summary>
        /// <param name="serverCertificateErrors">The server certificate errors</param>
        /// <returns>False if the connection should be aborted</returns>
        private async Task<bool> ShouldIgnoreCertificateErrorsAsync(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            IReadOnlyList<ChainValidationResult> serverCertificateErrors)
        {
           // Accept self signed certificates
           return true;
        }

        /// <summary>
        /// Gets detailed certificate information
        /// </summary>
        /// <param name="serverCert">The server certificate</param>
        /// <param name="intermediateCertificates">The server certificate chain</param>
        /// <returns>A string containing certificate details</returns>
        private string GetCertificateInformation(
            Certificate serverCert,
            IReadOnlyList<Certificate> intermediateCertificates)
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("\tFriendly Name: " + serverCert.FriendlyName);
            stringBuilder.AppendLine("\tSubject: " + serverCert.Subject);
            stringBuilder.AppendLine("\tIssuer: " + serverCert.Issuer);
            stringBuilder.AppendLine("\tValidity: " + serverCert.ValidFrom + " - " + serverCert.ValidTo);

            // Enumerate the entire certificate chain.
            if (intermediateCertificates.Count > 0)
            {
                stringBuilder.AppendLine("\tCertificate chain: ");
                foreach (var cert in intermediateCertificates)
                {
                    stringBuilder.AppendLine("\t\tIntermediate Certificate Subject: " + cert.Subject);
                }
            }
            else
            {
                stringBuilder.AppendLine("\tNo certificates within the intermediate chain.");
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Find registered CSTA command handler
        /// </summary>
        /// <param name="cmdName">CSTA command name</param>
        /// <returns>A CSTA command handler or null</returns>
        private CSTACommand findCommand(string cmdName)
        {
            foreach(var pair in commands)
            {
                CSTACommand cmd = (CSTACommand)pair.Value;
                if (cmd.events.ContainsKey(cmdName))
                {
                    cmd.eventName = cmdName;
                    cmd.events[cmdName] = new Dictionary<string, object>();
 
                    return cmd;
                }
            }
            return null;
        }

        /// <summary>
        /// Internal function for processing incomming CSTA messages
        /// </summary>
        /// <param name="squenceStr">CSTA command squence number string</param>
        /// <param name="messageStr">CSTA command message string</param>
        /// <returns></returns>
        private async Task parseMessage(string sequnceStr, string messageStr)
        {
            Debug.WriteLine("CSTA", messageStr);
            using (XmlReader reader = XmlReader.Create(new System.IO.StringReader(messageStr)))
            {
                CSTACommand command = null;
                string nodeName = null;
                string closedName = null;
                Dictionary<string, object> elementValues = null;
                List<object> list = new List<object>();
                List<object> curList = null;

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
//                          Debug.WriteLine("Start Element", reader.Name);
                            if (command == null)
                            {
                                command = findCommand(reader.Name);
                                if (command != null)
                                {
                                    elementValues = (Dictionary<string, object>)command.events[command.eventName];
                                }
                                else
                                {
                                    elementValues = null;
                                }
                            }
                            else
                            {
                                if (list.Count == 0 || curList == null)
                                {
                                    curList = new List<object>();
                                    elementValues.Add(reader.Name, curList);
                                    list.Add(elementValues);
                                    closedName = reader.Name;
                                }
                                if (reader.Name == closedName)
                                {
                                    Dictionary<string, object> newValues = new Dictionary<string, object>();
                                    curList.Add(newValues);
                                    elementValues = newValues;
                                    closedName = null;
                                }
                            }
                            if (reader.HasAttributes) {
                                while (reader.MoveToNextAttribute())
                                {
                                    if (elementValues != null)
                                    {
                                       elementValues.Add(reader.Name, reader.Value);
                                    }
//                                    Debug.WriteLine(String.Format("  {0} = {1}", reader.Name, reader.Value));
                                }
                                // Move the reader back to the element node.
                                reader.MoveToElement();
                            }
                            nodeName = reader.Name;
                            break;

                        case XmlNodeType.Text:
                            if (elementValues != null)
                            {
                                elementValues.Add(nodeName, reader.Value);
                            }
//                          Debug.WriteLine("Text Node", reader.Value);
                            break;
                        case XmlNodeType.EndElement:
                            closedName = reader.Name;
                            if (list.Count > 0 && curList[curList.Count - 1] != elementValues )
                            {
                                elementValues = (Dictionary <string, object>)list[list.Count - 1];
                                list.RemoveAt(list.Count - 1);
                            }
//                            Debug.WriteLine("End Element", reader.Name);
                            break;
                        default:
//                            Debug.WriteLine(String.Format("Other node {0} with value {1}",
//                                            reader.NodeType, reader.Value));
                            break;
                    }
                }
                if (command != null)
                {
                    if (command.eventName == "loginFailed") { // Internal processing for special case of loginFailed
                        LoginCommand loginCmd = (LoginCommand)command;
                        elementValues = (Dictionary<string, object>)command.events[command.eventName];
                        if (!loginCmd.clearTextPassword && elementValues.ContainsKey("apilevel") && elementValues.ContainsKey("Code"))
                        {
                            int apiversion = 0;
                            int code = -1;
                            bool parsed = Int32.TryParse(elementValues["apiversion"].ToString(), out apiversion);
                            parsed = parsed & Int32.TryParse(elementValues["Code"].ToString(), out code);
                            if (parsed && apiversion >= 2 && code == 4) // Handle cleartext login for LDAP integration
                            {
                                // clear old results
                                loginCmd.eventName = null;
                                loginCmd.events["loginFailed"] = null;
                                // set cleartext flag
                                loginCmd.clearTextPassword = true;
                                await sendText(loginCmd.cmdBody());
                                return; // do not send envent until second login attempt will be done
                            }
                        }
                    } else if (command.eventName == "loginResponce")
                    {

                    }

                    ///////////////////////////////////////////////////////////////
                    // Best place to notify UI regarding new message is here...
                    EventHandler handler = cstaEvent;
                    if (handler != null && command != null)
                    {
                        int numValue;
                        bool parsed = Int32.TryParse(sequnceStr, out numValue);
                        if (!parsed) numValue = 9999;
                        handler(this, new CSTAEventArgs(command.eventName, numValue, (Dictionary<string, object>)command.events[command.eventName]));
                    }
                }
            }
        }

        /// <summary>
        /// Timer event for send keepalive commands
        /// </summary>
        /// <returns></returns>

        private async void _timerEvent(ThreadPoolTimer timer)
        {
            var dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await KeepAlive();
                Debug.WriteLine("keepalive");
            });

        }

        private int _keepAliveTimeout = 45;
        public int KeepAliveTimeout
        {
            get
            {
                return _keepAliveTimeout;
            }
            set
            {
                _keepAliveTimeout = value;
            }
        }

        // Continuously read incoming data. For reading data we'll show how to use socket.InputStream.AsStream()
        // to get a .NET stream. Alternatively you could call readBuffer.AsBuffer() to use IBuffer with
        // socket.InputStream.ReadAsync.
        private async void ReceiveDataLoop(object state)
        {
            try
            {
                Stream readStream = (Stream)state;

                while (true) // Until closed and ReadAsync fails.
                {
                    int read = await readStream.ReadAsync(readBuffer, 0, 4);
                    if (read != sizeof(Int32))
                    {
                        // The underlying socket was closed before we were able to read the whole data.
                        return;
                    }
                    int messageLength = readBuffer[2] * 256 + readBuffer[3];
                    int actualMessageLength = await readStream.ReadAsync(readBuffer, 0, messageLength - 4);

                    if ((messageLength - 4) != actualMessageLength)
                    {
                        // The underlying socket was closed before we were able to read the whole data.
                        return;
                    }

                    // Do something with the data.
                    string receivedStr = Encoding.UTF8.GetString(readBuffer, 0, actualMessageLength);
                    await parseMessage(receivedStr.Substring(0, 4), receivedStr.Substring(4, receivedStr.Length - 4));
                }
            }
            catch (ObjectDisposedException)
            {
                // Do nothing for now...
            }
            catch (Exception ex)
            {
                SocketErrorStatus status = SocketError.GetStatus(ex.HResult);
                switch (status)
                {
                    case SocketErrorStatus.Unknown:
                        if (CoreApplication.Properties.ContainsKey("connected"))
                        {
                            throw;
                        }
                        break;
                    case SocketErrorStatus.OperationAborted:
                    case SocketErrorStatus.SoftwareCausedConnectionAbort:
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Close stream and disconnect from MX Server
        /// </summary>
        /// <returns></returns>
        public void Disconnect()
        {
            // Distroy keepalive timer before close connection
            if (_timer != null)
            {
                _timer.Cancel();
                _timer = null;
            }

            if (CoreApplication.Properties.ContainsKey("connected"))
            {
                CoreApplication.Properties.Remove("connected");
            }

            object outValue;
            if (CoreApplication.Properties.TryGetValue("clientSocket", out outValue))
            {
                // Remove the socket from the list of application properties as we are about to close it.
                CoreApplication.Properties.Remove("clientSocket");
                StreamSocket socket = (StreamSocket)outValue;

                // StreamSocket.Close() is exposed through the Dispose() method in C#.
                // The call below explicitly closes the socket.
                socket.Dispose();
            }
        }

        private static int sequenceNumber = 0;

        /// <summary>
        /// Internal function provides next number for CSTA command
        /// </summary>
        /// <returns></returns>
        private int nextNumber()
        {
            sequenceNumber++;
            if (sequenceNumber == 9999) {
                sequenceNumber = 1;
            }
            return sequenceNumber;
        }

        private object sendLock = new Object();

        /// <summary>
        /// Internal function for send CSTA command
        /// </summary>
        /// <param name="text">CSTA command message string</param>
        /// <returns></returns>
        private async Task<int> sendText(string text)
        {
            if (!CoreApplication.Properties.ContainsKey("connected"))
            {
                return -1;
            }

            object outValue;
            StreamSocket socket;
            if (!CoreApplication.Properties.TryGetValue("clientSocket", out outValue))
            {
                return -1;
            }

            Debug.WriteLine(text, "CSTA");

            socket = (StreamSocket)outValue;
            IOutputStream writeStream = socket.OutputStream;

            byte[] data;
            int sequence = nextNumber();
            lock (sendLock)
            {
                string strSequence = String.Format("{0,4:D4}", sequence);
                string datastr = strSequence + text;
                byte[] msgdata = Encoding.UTF8.GetBytes(datastr);
                int len = msgdata.Length + 4;
                data = new byte[len];
                data[0] = 0;
                data[1] = 0;
                data[2] = (byte)(len / 256);
                data[3] = (byte)(len % 256);
                System.Buffer.BlockCopy(msgdata, 0, data, 4, msgdata.Length);
            }

            // Write the locally buffered data to the network.
            try
            {
                await writeStream.WriteAsync(data.AsBuffer());
            }
            catch (Exception exception)
            {
                sequence = -1;
                // If this is an unknown status it means that the error if fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }
            }
            return sequence;
        }

        /// <summary>
        /// Login to MX sever
        /// </summary>
        /// <param name="login">Login name</param>
        /// <param name="password">Password</param>
        /// <returns></returns>
        public async Task Login(string login, string password)
        {
            LoginCommand loginCmd = (LoginCommand)commands["login"];
            loginCmd.parameters["type"] = "User";
            loginCmd.parameters["platform"] = "iPhone";
            loginCmd.parameters["userName"] = login;
            loginCmd.parameters["version"] = "7.0";
            loginCmd.parameters["pwd"] = password;
            await sendText(loginCmd.cmdBody());
        }

        private async Task KeepAlive()
        {
            await sendText("<?xml version=\"1.0\" encoding=\"utf-8\"?><keepalive/>");
        }
    }
}
