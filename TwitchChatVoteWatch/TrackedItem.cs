using System;

namespace TwitchChatVoteWatch
{
    public class TrackedItem : IEquatable<TrackedItem>, IComparable<TrackedItem>
    {
        private int nCount = 0;
        public int Count
        {
            get { return nCount; }
            set
            {
                nCount = value;
            }
        }

        private readonly string sMessage = "";
        public string Message => sMessage;

        public TrackedItem(string sMessage, int nCount)
        {
            this.sMessage = sMessage;
            this.nCount = nCount;
        }

        public int CompareTo(TrackedItem other)
        {
            if (other == null)
            {
                return 1;
            }

            if (Count.Equals(other.Count))
            {
                return Message.CompareTo(other.Message);
            }

            return other.Count.CompareTo(Count);
        }

        public bool Equals(TrackedItem other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.Count == this.Count)
            {
                return true;
            }

            return false;
        }
    }
}
