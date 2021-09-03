using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace TwitchChatVoteWatch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Properties
        private bool bConnected = false;
        public bool Connected
        {
            get { return bConnected; }
            set
            {
                bConnected = value;
                NotifyPropertyChanged("Connected");
            }
        }

        private int nCheckLastSeconds = 30;
        public string CheckLastSeconds
        {
            get { return nCheckLastSeconds.ToString(); }
            set
            {
                int nValue = 0;
                if (int.TryParse(value, out nValue))
                {
                    nCheckLastSeconds = nValue;
                }

                NotifyPropertyChanged("CheckLastSeconds");
            }
        }

        private ObservableCollection<MessageItem> oChatBox;
        public ObservableCollection<MessageItem> ChatBox
        {
            get { return oChatBox; }
            set
            {
                oChatBox = value;
                NotifyPropertyChanged("ChatBox");
            }
        }

        private ObservableCollection<TrackedItem> oTrackedItems;
        public ObservableCollection<TrackedItem> TrackedItems
        {
            get { return oTrackedItems; }
            set
            {
                oTrackedItems = value;
                NotifyPropertyChanged("TrackedItems");
            }
        }

        private string sMessageFilter = @"\w+";
        public string MessageFilter
        {
            get { return sMessageFilter; }
            set
            {
                try
                {
                    Regex.Match("", value);
                    DoThePoll = true;
                    sMessageFilter = value;
                }
                catch (ArgumentException)
                {
                    DoThePoll = false;
                }
                NotifyPropertyChanged("MessageFilter");
            }
        }

        private bool DoThePoll = false;

        public IRCbot irc = null;
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        BackgroundWorker ircWorker = new BackgroundWorker();
        BackgroundWorker pollWorker = new BackgroundWorker();

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;
            ChatBox = new ObservableCollection<MessageItem>();
            ircWorker.DoWork += ircWorker_DoWork;
            ircWorker.WorkerSupportsCancellation = true;
            pollWorker.DoWork += pollWorker_DoWork;
            pollWorker.WorkerSupportsCancellation = true;
        }

        #region Events
        private void ircWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            ConnectToServer connectToServer = (ConnectToServer)e.Argument;
            Log("Connecting to " + connectToServer.Server + ":" + connectToServer.Port + "...");
            irc = new IRCbot(connectToServer);
            irc.MessageReceived += Irc_MessageReceived;
            Connected = true;
            irc.Start();
        }

        private void pollWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                if (DoThePoll)
                {
                    DoVote();
                }
                Thread.Sleep(500);
            }
        }

        private void File_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        private void Irc_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (e != null)
            {
                Log(e.MessageItem);
                DoVote();
            }
        }

        private void File_Disconnect_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void File_Connect_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Connected)
            {
                Disconnect();
            }
        }

        private void tbLookBack_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            int nValue = 0;
            if (!int.TryParse(tbLookBack.Text, out nValue))
            {
                tbLookBack.Text = CheckLastSeconds;
            }
            else
            {
                if (nValue <= 0)
                {
                    nValue = 1;
                }
                else if (nValue > 600)
                {
                    nValue = 600;
                }

                CheckLastSeconds = nValue.ToString();
            }
        }

        // https://stackoverflow.com/a/46548292
        // Simple solution to have the chatbox automatically scroll down as new items are added
        private void lbChat_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is ScrollViewer scrollviewer && Math.Abs(e.ExtentHeightChange) > 0.0)
            {
                scrollviewer.ScrollToBottom();
            }
        }
        #endregion

        #region Methods
        private void DoVote()
        {
            List<TrackedItem> lTrackedItems = new List<TrackedItem>();

            // Start at the the most recent items and work backwards
            // As soon as we hit an item outside of our time we stop looping because all subsequent items will also be outside the time window
            for (int i = ChatBox.Count - 1; i >= 0; i--)
            {
                MessageItem messageItem = ChatBox[i];

                if (messageItem.MessageType != MessageItem.MessageTypes.USER) { continue; }
                if (String.IsNullOrEmpty(messageItem.Message)) { continue; }

                if ((DateTime.UtcNow - messageItem.Received).TotalSeconds <= nCheckLastSeconds)
                {
                    string sTrackedMessage = messageItem.Message;

                    if (!String.IsNullOrEmpty(MessageFilter))
                    {
                        Match match = Regex.Match(messageItem.Message, MessageFilter, RegexOptions.IgnoreCase);

                        if (match == null) { continue; }
                        if (!match.Success) { continue; }
                        sTrackedMessage = match.Value;
                    }

                    int foundIndex = lTrackedItems.FindIndex(x => x.Message == sTrackedMessage);

                    if (foundIndex < 0)
                    {
                        lTrackedItems.Add(new TrackedItem(sTrackedMessage, 1));
                    }
                    else
                    {
                        lTrackedItems[foundIndex].Count++;
                    }
                }
                else
                {
                    break;
                }
            }

            if (lTrackedItems.Count > 0)
            {
                // Sort the items, with the largest count being top. If count is the same, sort by alphabetical order
                lTrackedItems.Sort(delegate (TrackedItem x, TrackedItem y)
                {
                    if (x == null && y == null) { return 0; }
                    if (x == null) { return 1; }
                    if (y == null) { return -1; }
                    if (x.Count.Equals(y.Count)) { return x.Message.CompareTo(y.Message); }
                    return y.Count.CompareTo(x.Count);
                });

                TrackedItems = new ObservableCollection<TrackedItem>(lTrackedItems);
            }
            else
            {
                TrackedItems = null;
            }
        }

        public void Log(MessageItem messageItem)
        {
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                ChatBox.Add(messageItem);
            });
        }

        public void Log(string sMessage, string sUser = "[system]")
        {
            Log(new MessageItem(MessageItem.MessageTypes.SYSTEM, sUser, sMessage));
        }

        private void Connect()
        {
            ConnectToServer connectToServer = new ConnectToServer();
            if ((bool)connectToServer.ShowDialog())
            {
                ircWorker.RunWorkerAsync(connectToServer);
                pollWorker.RunWorkerAsync();
            }

        }

        private void Disconnect()
        {
            Connected = false;
            if (ircWorker != null)
            {
                ircWorker.CancelAsync();
            }

            if (pollWorker != null)
            {
                pollWorker.CancelAsync();
            }

            Log("Disconnected from server.");
        }
        #endregion
    }
}
