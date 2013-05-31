namespace Gap
{
    using System;
    using System.Collections.Concurrent;
    using System.Timers;

    using agsXMPP;
    using agsXMPP.protocol.client;
    using agsXMPP.protocol.x.muc;

    public class MessageQueue
    {
        #region Singleton

        internal static MessageQueue SingletonContext { get; set; }

        private static readonly object MessageQueueSingletonLocker = new object();

        public static MessageQueue Get()
        {
            lock (MessageQueueSingletonLocker) return SingletonContext ?? (SingletonContext = new MessageQueue());
        }

        internal MessageQueue()
        {
            Queue = new ConcurrentQueue<MessageItem>();
            Timer = new Timer(1000);
            Timer.Elapsed += TimerOnElapsed;
            Timer.Start();
        }

        #endregion Singleton

        internal Timer Timer { get; set; }
        internal ConcurrentQueue<MessageItem> Queue { get; set; } 

        public void Add(MessageItem item)
        {
            Queue.Enqueue(item);
        }

        public MessageItem Next()
        {
            MessageItem ret = null;
            Queue.TryDequeue(out ret);
            return ret;
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            var item = this.Next();
            if (item == null) return;
            if (item.From.Equals("octgn-gap", StringComparison.InvariantCultureIgnoreCase)) return;
            switch (item.Dest)
            {
                case Destination.Irc:
                    //Program.IrcBot.IrcClient.ChangeName("O8G-" + item.From);
                    Program.IrcBot.IrcClient.Message("#octgn",item.From + ": " + item.Message);
                    break;
                case Destination.Xmpp:
                    var to = new Jid("lobby@conference.of.octgn.net");
                    var j = new Jid(to.Bare);
                    var m = new Message(j, MessageType.groupchat, item.From + ": " + item.Message);
                    m.GenerateId();
                    //m.XEvent = new Event { Delivered = true, Displayed = true };  
                    Program.XmppBot.Con.Send(m);
                    break;
            }
        }
    }

    public class MessageItem
    {
        public MessageItem(string from, string message, Destination dest)
        {
            From = from;
            Message = message;
            Dest = dest;
        }
        public string From { get; set; }
        public string Message { get; set; }
        public Destination Dest { get; set; }
    }

    public enum Destination
    {
        Irc,Xmpp
    }
}