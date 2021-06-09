// Written by: AkyrosXD (https://github.com/AkyrosXD)
// Last modification: 09/06/2021

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Akyros.Asterisk.AMI
{
    public class AmiClient
    {
        private const string ASTERISK_PACKET_END_LINE = "\r\n\r\n";

        private readonly Socket socket;

        private string amiUsername;

        private string amiSecret;

        private string amiUrl;

        private int amiPort;

        private bool loggedin;

        // the thread in which the socket is reading data
        private Thread readingThread;

        // the thread context in which this instance was created
        // this is used to make the class thread-safe
        // it is recommended that you create the instance on the main thread
        private SynchronizationContext mainThreadContext;

        public event Action<Dictionary<string, string>> OnEvent;

        public event Action OnLoginFailed;

        public event Action OnLoginSuccess;

        public event Action OnLogoff;

        public bool IsLoggedIn => loggedin;

        public AmiClient(string username, string secret, string url, int port)
        {
            amiUsername = username;
            amiSecret = secret;
            amiUrl = url;
            amiPort = port;
            loggedin = false;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            mainThreadContext = SynchronizationContext.Current;
        }

        ~AmiClient()
        {
            if (socket != null)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Disconnect(false);
                socket.Close();
            }
        }

        public void Reset(string username, string secret, string url, int port)
        {
            if (!loggedin)
            {
                amiUsername = username;
                amiSecret = secret;
                amiUrl = url;
                amiPort = port;
            }
        }

        public bool Connect()
        {
            if (!socket.Connected)
            {
                try
                {
                    var addresses = Dns.GetHostAddresses(amiUrl);

                    if (addresses.Length == 0)
                        return false;

                    IPEndPoint endPoint = new(addresses[0], amiPort);

                    socket.Connect(endPoint);
                }
                catch { return false; }
            }
            return true;
        }

        public bool Login(string username, string secret)
        {
            if (!socket.Connected)
                return false;

            SendString($"Action: Login\nUsername: {username}\nSecret: {secret}\n\n");
            if (readingThread == null)
            {
                readingThread = new(ReadDataThread)
                {
                    IsBackground = true
                };
            }
            if (!readingThread.IsAlive)
            {
                readingThread.Start();
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendString(string data)
        {
            socket.Send(Encoding.UTF8.GetBytes(data));
        }

        public void Logoff()
        {
            if (socket.Connected && loggedin)
            {
                SendString("Action: Logoff\n\n");
                loggedin = false;
            }
        }

        private static Dictionary<string, string> GetEventData(string data)
        {
            Dictionary<string, string> result = new();
            string[] lines = data.Split("\n");
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    int seperatorIndex = line.IndexOf(':');
                    if (seperatorIndex > 0)
                    {
                        string key = line[..seperatorIndex].Trim();
                        if (!string.IsNullOrEmpty(key))
                        {
                            if (!result.ContainsKey(key))
                            {
                                string value = line[(seperatorIndex + 1)..].Trim();
                                result.Add(key, value);
                            }
                        }
                    }
                }
            }
            return result;
        }

        private void ReadDataThread()
        {
            const int MAX = 4096;
            byte[] buffer = new byte[MAX];
            while (true)
            {
                if (!socket.Connected)
                {
                    continue;
                }

                int offset = 0;
                string str = string.Empty;
                while (!str.EndsWith(ASTERISK_PACKET_END_LINE))
                {
                    int bytesReceived = socket.Receive(buffer, offset, 1, SocketFlags.None);
                    if (bytesReceived <= 0)
                    {
                        continue;
                    }
                    str = Encoding.UTF8.GetString(buffer);
                    offset++;
                }
                Dictionary<string, string> eventData = GetEventData(str);
                if (eventData.Count > 0)
                {
                    if (eventData.TryGetValue("Response", out string response))
                    {
                        if (response == "Goodbye")
                        {
                            loggedin = false;
                            mainThreadContext.Post(delegate (object sate) { OnLogoff(); }, null);
                        }
                        else if (eventData.TryGetValue("Message", out string message))
                        {
                            if (response == "Error" && message == "Authentication failed")
                            {
                                loggedin = false;
                                mainThreadContext.Post(delegate (object sate) { OnLoginFailed(); }, null);
                            }
                            else if (response == "Success" && message == "Authentication accepted")
                            {
                                loggedin = true;
                                mainThreadContext.Post(delegate(object sate) { OnLoginSuccess(); }, null);
                            }
                        }
                    }
                    mainThreadContext.Post(delegate (object sate) { OnEvent(eventData); }, null);
                }
                buffer = new byte[MAX]; // zero everything
                GC.Collect(0, GCCollectionMode.Forced); // just in case
                Thread.Sleep(50);
            }
        }
    }
}
