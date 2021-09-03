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
    // The source of most of this code
    public class IRCbot
    {
        // server to connect to (edit at will)
        private readonly string _server;
        // server port (6667 by default)
        private readonly int _port;
        // user information defined in RFC 2812 (IRC: Client Protocol) is sent to the IRC server 
        private readonly string _user;

        // the bot's nickname
        private readonly string _nick;
        // channel to join
        private readonly string _channel;

        private readonly string _pass;

        private readonly int _maxRetries;

        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);

        public event MessageReceivedEventHandler MessageReceived;

        public IRCbot(string server, int port, string user, string nick, string channel, string pass, int maxRetries = 3)
        {
            _server = server;
            _port = port;
            _user = user;
            _nick = nick;
            _channel = channel;
            _maxRetries = maxRetries;
        }

        public IRCbot(ConnectToServer cts, int maxRetries = 3)
        {
            _server = cts.Server;
            int port = 80;
            int.TryParse(cts.Port, out port);
            _port = port;
            _user = cts.Nickname;
            _nick = cts.Nickname;
            _channel = cts.Channel;
            _pass = cts.Password;
            _maxRetries = maxRetries;
        }

        public void Start()
        {
            var retry = false;
            var retryCount = 0;
            do
            {
                try
                {
                    using (var irc = new TcpClient(_server, _port))
                    using (var stream = irc.GetStream())
                    using (var reader = new StreamReader(stream))
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.WriteLine("PASS " + _pass);
                        writer.Flush();
                        writer.WriteLine("NICK " + _nick);
                        writer.Flush();
                        writer.WriteLine("USER " + _user + " 0 * :" + _user);
                        writer.Flush();

                        Regex rxUser = new Regex(@"@(\w+)\.tmi\.twitch.tv", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        Regex rxMessage = new Regex(_channel + @" :(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                        while (true)
                        {
                            string inputLine;
                            while ((inputLine = reader.ReadLine()) != null)
                            {
                                Console.WriteLine("<- " + inputLine);
                                
                                Match mUser = rxUser.Match(inputLine);
                                Match mMessage = rxMessage.Match(inputLine);

                                // If there are no messages then just dump the text
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
                                        writer.WriteLine("JOIN " + _channel);
                                        writer.Flush();
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // shows the exception, sleeps for a little while and then tries to establish a new connection to the IRC server
                    Console.WriteLine(e.ToString());
                    Thread.Sleep(5000);
                    retry = ++retryCount <= _maxRetries;
                }
            } while (retry);
        }
    }
}
