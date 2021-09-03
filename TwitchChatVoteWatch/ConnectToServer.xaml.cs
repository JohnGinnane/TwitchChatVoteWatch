using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TwitchChatVoteWatch
{
    /// <summary>
    /// Interaction logic for ConnectToServer.xaml
    /// </summary>
    public partial class ConnectToServer : Window, INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;
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

        private string sNickname = "TwitchChatVoteWatch";
        public string Nickname
        {
            get { return sNickname; }
            set
            {
                sNickname = value;
                NotifyPropertyChanged("Nickname");
            }
        }

        private string sServer = "irc.chat.twitch.tv";
        public string Server
        {
            get { return sServer; }
            set
            {
                sServer = value;
                NotifyPropertyChanged("Server");
            }
        }

        private string sPort = "80";
        public string Port
        {
            get { return sPort; }
            set
            {
                sPort = value;
                NotifyPropertyChanged("Port");
            }
        }

        private string sChannel = "#twitchnamehere";
        public string Channel
        {
            get { return sChannel; }
            set
            {
                sChannel = value;
                NotifyPropertyChanged("Channel");
            }
        }

        private string sPassword = "Please visit: https://twitchapps.com/tmi/";
        public string Password
        {
            get { return sPassword; }
            set
            {
                sPassword = value;
                NotifyPropertyChanged("Password");
            }
        }

        public ConnectToServer()
        {
            InitializeComponent();

            DataContext = this;

            IniFile iniFile = new IniFile("config.ini");
            string sServer = iniFile.Read("Server", "ConnectionDetails");
            string sPort = iniFile.Read("Port", "ConnectionDetails");
            string sNickname = iniFile.Read("Nickname", "ConnectionDetails");
            string sChannel = iniFile.Read("Channel", "ConnectionDetails");
            string sPassword = iniFile.Read("Password", "ConnectionDetails");

            if (!String.IsNullOrEmpty(sServer)) { Server = sServer; }
            if (!String.IsNullOrEmpty(sPort)) { Port = sPort; }
            if (!String.IsNullOrEmpty(sNickname)) { Nickname = sNickname; }
            if (!String.IsNullOrEmpty(sChannel)) { Channel = sChannel; }
            if (!String.IsNullOrEmpty(sPassword)) { Password = sPassword; }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            // Save our choices for next time
            IniFile iniFile = new IniFile("config.ini");
            iniFile.Write("Server", Server, "ConnectionDetails");
            iniFile.Write("Port", Port, "ConnectionDetails");
            iniFile.Write("Nickname", Nickname, "ConnectionDetails");
            iniFile.Write("Channel", Channel, "ConnectionDetails");
            iniFile.Write("Password", Password, "ConnectionDetails");
            this.DialogResult = true;
        }
    }
}
