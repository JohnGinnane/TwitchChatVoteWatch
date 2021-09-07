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


        private string sBaseWindowTitle = "Twitch Chat Vote Watch";
        private string sExtraWindowTitle = "";
        public string WindowTitle
        {
            get { return (sBaseWindowTitle + " " + sExtraWindowTitle).Trim(); }
            set
            {
                sExtraWindowTitle = value;
                NotifyPropertyChanged("WindowTitle");
            }
        }

        private bool bMinimised = false;
        public bool Minimised
        {
            get { return bMinimised; }
            set
            {
                bMinimised = value;
                IniFile iniFile = new IniFile("config.ini");
                iniFile.Write("Minimised", bMinimised.ToString(), "Settings");
                NotifyPropertyChanged("Minimised");
                NotifyPropertyChanged("MinMaxStr");
            }
        }
        public string MinMaxStr
        {
            get { return Minimised ? "_Maximise" : "_Minimise"; }
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

        private string sMessageFilter = @"\S+";
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

        private bool DoThePoll = true;

        public IRCbot irc = null;
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        BackgroundWorker ircWorker = null;
        BackgroundWorker pollWorker = null;

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

            LoadSettings();
        }

        private void LoadSettings()
        {
            // Try and restore the window's size and layout
            IniFile iniFile = new IniFile("config.ini");

            int nCheckLastSeconds = 30;
            string sFilter = @"\S+";

            int nWindowWidth = 800;
            int nWindowHeight = 450;

            int nChatWidth = 2;
            GridUnitType gutChatType = GridUnitType.Star;
            int nPollWidth = 1;
            GridUnitType gutPollType = GridUnitType.Star;

            try
            {
                string sWidth = iniFile.Read("Width", "Window");
                if (!String.IsNullOrEmpty(sWidth))
                {
                    int.TryParse(sWidth, out nWindowWidth);
                }
            }
            catch (Exception) { } finally { this.Width = nWindowWidth; }

            try
            {
                string sHeight = iniFile.Read("Height", "Window");
                if (!String.IsNullOrEmpty(sHeight))
                {
                    int.TryParse(sHeight, out nWindowHeight);
                }
            } 
            catch (Exception) { } finally { this.Height = nWindowHeight; }

            try
            {
                string sChatWidth = iniFile.Read("ChatWidth", "Window");

                if (!String.IsNullOrEmpty(sChatWidth))
                {
                    if (sChatWidth.EndsWith("*"))
                    {
                        int.TryParse(sChatWidth.Substring(0, sChatWidth.Length - 1), out nChatWidth);
                    }
                    else
                    {
                        int.TryParse(sChatWidth, out nChatWidth);
                        gutChatType = GridUnitType.Pixel;
                    }
                }
            }
            catch (Exception) { } finally { cdChatBox.Width = new GridLength(nChatWidth, gutChatType); }

            try
            {
                string sPollWidth = iniFile.Read("PollWidth", "Window");

                if (!String.IsNullOrEmpty(sPollWidth))
                {
                    if (sPollWidth.EndsWith("*"))
                    {
                        int.TryParse(sPollWidth.Substring(0, sPollWidth.Length - 1), out nPollWidth);
                    }
                    else
                    {
                        int.TryParse(sPollWidth, out nPollWidth);
                        gutPollType = GridUnitType.Pixel;
                    }
                }

            }
            catch (Exception) { } finally { cdPoll.Width = new GridLength(nPollWidth, gutPollType); }
        
            try
            {
                string sCheckLastSeconds = iniFile.Read("CheckLastSeconds", "Settings");
                if (!String.IsNullOrEmpty(sCheckLastSeconds))
                {
                    int.TryParse(sCheckLastSeconds, out nCheckLastSeconds);
                }
            } catch (Exception) { } finally { CheckLastSeconds = nCheckLastSeconds.ToString(); }

            try
            {
                sFilter = iniFile.Read("Filter", "Settings");
                MessageFilter = sFilter;
            } catch (Exception) { }
            
        }

        private void IrcWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (ircWorker != null)
            {
                ircWorker.Dispose();
                ircWorker = null;
            }
        }

        private void PollWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TrackedItems = null;

            if (pollWorker != null)
            {
                pollWorker.Dispose();
                pollWorker = null;
            }
        }

        #region Events
        private void ircWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            irc.Start();
        }

        private void pollWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                if (pollWorker.CancellationPending) 
                {
                    break;
                }

                DoVote();
                Thread.Sleep(500);
            }
        }

        private void miFileExit_Click(object sender, RoutedEventArgs e)
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

        private void miFileDisconnect_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void miFileConnect_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Disconnect();

            IniFile iniFile = new IniFile("config.ini");

            iniFile.Write("Filter", sMessageFilter, "Settings");
            iniFile.Write("CheckLastSeconds", nCheckLastSeconds.ToString(), "Settings");

            iniFile.Write("Width", this.ActualWidth.ToString(), "Window");
            iniFile.Write("Height", this.ActualHeight.ToString(), "Window");
            iniFile.Write("ChatWidth", cdChatBox.ActualWidth.ToString() + "*", "Window");
            iniFile.Write("PollWidth", cdPoll.ActualWidth.ToString() + "*", "Window");
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
            if (!DoThePoll) { return; }
            if (!Connected) { return; }
            if (ChatBox.Count <= 0) { return; }

            // Polling occurs every time a message is received OR at a fixed interval
            // If a poll is in progress let's not bother doing another
            DoThePoll = false;

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
                lTrackedItems.Sort();
                TrackedItems = new ObservableCollection<TrackedItem>(lTrackedItems);
            }
            else
            {
                TrackedItems = null;
            }

            DoThePoll = true;
        }

        public void Log(MessageItem messageItem)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                ChatBox.Add(messageItem);
            });
        }

        public void Log(string sMessage)
        {
            Log(new MessageItem(sMessage));
        }

        private void Connect()
        {
            ConnectToServer cts = new ConnectToServer();
            if ((bool)cts.ShowDialog())
            {
                ircWorker = new BackgroundWorker();
                pollWorker = new BackgroundWorker();

                ircWorker.DoWork += ircWorker_DoWork;
                ircWorker.WorkerSupportsCancellation = true;
                ircWorker.RunWorkerCompleted += IrcWorker_RunWorkerCompleted;
                pollWorker.DoWork += pollWorker_DoWork;
                pollWorker.WorkerSupportsCancellation = true;
                pollWorker.RunWorkerCompleted += PollWorker_RunWorkerCompleted;

                irc = new IRCbot(cts.Server, int.Parse(cts.Port), cts.Nickname, cts.Nickname, cts.Channel, cts.Password);
                irc.MessageReceived += Irc_MessageReceived;
                Connected = true;
                Log("Connecting to server " + irc.Server + "...");
                ircWorker.RunWorkerAsync();
                pollWorker.RunWorkerAsync();
                WindowTitle = irc.Channel;
            }
        }

        private void Disconnect()
        {
            if (irc != null)
            {
                Log("Disconnecting from server " + irc.Server + "...");
            }
            else
            {
                Log("Disconnecting from server...");
            }

            WindowTitle = "";

            Connected = false;

            if (irc != null)
            {
                irc.Cancelled = true;
            }

            if (ircWorker != null)
            {
                ircWorker.CancelAsync();
            }

            if (pollWorker != null)
            {
                pollWorker.CancelAsync();
            }

            if (TrackedItems != null)
            {
                TrackedItems = null;
            }
        }
        #endregion

        private void miMinMax_Click(object sender, RoutedEventArgs e)
        {
            Minimised = !Minimised;
        }
    }
}
