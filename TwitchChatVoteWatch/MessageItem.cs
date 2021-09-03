using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TwitchChatVoteWatch
{
    public class MessageItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public enum MessageTypes { 
            SYSTEM,
            USER
        }

        private MessageTypes eMessageType;
        public MessageTypes MessageType
        {
            get { return eMessageType; }
            set
            {
                eMessageType = value;
                NotifyPropertyChanged("MessageType");
            }
        }


        private DateTime dtReceived;
        public DateTime Received
        {
            get { return dtReceived; }
            set
            {
                dtReceived = value;
                NotifyPropertyChanged("Received");
            }
        }

        private string sUser;
        public string User
        {
            get { return sUser; }
            set
            {
                sUser = value;
                NotifyPropertyChanged("User");
            }
        }

        private string sMessage;
        public string Message
        {
            get { return sMessage; }
            set
            {
                sMessage = value;
                NotifyPropertyChanged("Message");
            }
        }

        public string Print => "[" + Received.ToString("T") + "] " + User + ": " + Message;

        public MessageItem(MessageTypes eMessageType, string sUser, string sMessage)
        {
            Received = DateTime.UtcNow;
            MessageType = eMessageType;
            User = sUser;
            Message = sMessage;
        }

        public MessageItem(string sMessage) : this(MessageTypes.SYSTEM, "[system]", sMessage)
        {
        }
    }
}
