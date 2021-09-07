using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchChatVoteWatch
{
    public class MessageReceivedEventArgs
    {
        public MessageReceivedEventArgs(MessageItem messageItem) { MessageItem = messageItem; }
        public MessageItem MessageItem { get; }
    }

    // https://codereview.stackexchange.com/a/142674
    // The basis for this class
    public class IRCbot
    {
        #region Properties
        private bool bCancelled = false;
        public bool Cancelled
        {
            get { return bCancelled; } 
            set
            {
                bCancelled = value;
            }
        }

        private string sServer;
        public string Server => sServer;

        private int nPort = 80;
        public int Port => nPort;

        private string sUser;
        public string User => sUser;

        private string sNickname;
        public string Nickname => sNickname;

        private string sChannel;
        public string Channel => sChannel;

        private string sPassword;
        public string Password => sPassword;

        private int nMaxretries = 3;
        public int MaxRetries => nMaxretries;
        #endregion

        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);

        public event MessageReceivedEventHandler MessageReceived;

        public IRCbot(string server, int port, string user, string nick, string channel, string pass = "", int maxRetries = 3)
        {
            sServer = server;
            nPort = port;
            sUser = user;
            sNickname = nick;
            sChannel = channel;
            sPassword = pass;
            nMaxretries = maxRetries;
        }

        public void Start()
        {
            var retry = false;
            var retryCount = 0;
            do
            {
                try
                {
                    using (var irc = new TcpClient(Server, Port))
                    using (var stream = irc.GetStream())
                    using (var reader = new StreamReader(stream))
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.WriteLine("PASS " + Password);
                        writer.Flush();
                        writer.WriteLine("NICK " + Nickname);
                        writer.Flush();
                        writer.WriteLine("USER " + User + " 0 * :" + User);
                        writer.Flush();

                        Regex rxUser = new Regex(@"@(\w+)\.tmi\.twitch.tv", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        Regex rxMessage = new Regex(sChannel + @" :(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        string inputLine = "";
                        Task<string> tIncoming = null;

                        while (true)
                        {
                            if (Cancelled)
                            {
                                break;
                            }

                            if (tIncoming == null)
                            {
                                tIncoming = reader.ReadLineAsync();
                            }

                            if (tIncoming.IsCompleted)
                            {
                                if (inputLine == tIncoming.Result) {
                                    tIncoming.Dispose();
                                    tIncoming = null;
                                    continue;
                                }
                                if (tIncoming.Result == null) { continue; }

                                inputLine = tIncoming.Result;

                                Match mUser = rxUser.Match(inputLine);
                                Match mMessage = rxMessage.Match(inputLine);

                                // If we couldn't parse the user or message then just post it as a system message
                                if (!mUser.Success || !mMessage.Success)
                                {
                                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs(new MessageItem(MessageItem.MessageTypes.SYSTEM, "[system]", inputLine)));
                                }
                                else
                                {
                                    MessageItem messageItem = new MessageItem(MessageItem.MessageTypes.USER, mUser.Groups[1].Value, mMessage.Groups[1].Value);
                                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs(messageItem));
                                    continue;
                                }

                                string[] splitInput = inputLine.Split(new Char[] { ' ' });
                                switch (splitInput[1])
                                {
                                    case "001":
                                        writer.WriteLine("JOIN " + sChannel);
                                        writer.Flush();
                                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(new MessageItem("Connected to server.")));
                                        break;
                                    default:
                                        break;
                                }

                                tIncoming.Dispose();
                                tIncoming = null;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs(new MessageItem(e.ToString())));
                    Thread.Sleep(5000);
                    retry = ++retryCount <= MaxRetries;
                }
            } while (retry && !Cancelled);

            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(new MessageItem("Disconnected.")));
        }
    }
}
