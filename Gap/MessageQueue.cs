using System.IO;
using System.Text;
using System.Threading;
using IronPython.Hosting;

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

        public MessageItem Peek()
        {
            MessageItem ret = null;
            Queue.TryPeek(out ret);
            return ret;
        }

        private bool pauseIrc = false;
        private bool pauseXmpp = false;
        private StringBuilder pythonStatement;
        private bool multilinePython = false;

        private void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (Timer.Enabled == false)
                return;
            Timer.Enabled = false;
            try
            {
                var item = this.Next();
                if (item == null) return;
                if (item.From.Equals(IrcConfig.BotName, StringComparison.InvariantCultureIgnoreCase)) return;
                if (item.From.Equals(XmppConfig.Username, StringComparison.InvariantCultureIgnoreCase)) return;
                switch (item.Dest)
                {
                    case Destination.Irc:
                        //Program.IrcBot.IrcClient.ChangeName("O8G-" + item.From);
                        if (pauseXmpp && (item.From != "HELPZOR" || item.From != "PYZOR" || item.From != "ERRZOR"))
                            return;
                        using (var sr = new StringReader(item.Message))
                        {
                            var line = sr.ReadLine();
                            while (line != null)
                            {
                                Program.IrcBot.IrcClient.Message(IrcConfig.Channel, item.From + ": " + line);
                                line = sr.ReadLine();
                                if(line != null)
                                    Thread.Sleep(1000);
                            }
                        }

                        break;
                    case Destination.Xmpp:
                        if (item.Message.StartsWith(":"))
                        {
                            if (item.From.Equals("kellyelton", StringComparison.InvariantCultureIgnoreCase) ||
                                item.From.Equals("kellyelton_", StringComparison.InvariantCultureIgnoreCase)
                                 || item.From.Equals("brine",StringComparison.InvariantCultureIgnoreCase)
                                || item.From.Equals("brinelog",StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (item.Message.Equals(":stopirc", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    pauseIrc = true;
                                    return;
                                }
                                else if (item.Message.Equals(":startirc", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    pauseIrc = false;
                                    return;
                                }
                                else if (item.Message.Equals(":stopxmpp", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    pauseXmpp = true;
                                    return;
                                }
                                else if (item.Message.Equals(":startxmpp", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    pauseXmpp = false;
                                    return;
                                }
                            }
                            if (item.Message.Equals(":?"))
                            {
                                StringBuilder helpItems = new StringBuilder();
                                helpItems.AppendLine("-- Commands --");
                                helpItems.AppendLine("  :stopirc - Stop irc from sending messages to Xmpp");
                                helpItems.AppendLine("  :startirc - Enable irc sending messages to Xmpp");
                                helpItems.AppendLine("  :stopxmpp - Stop xmpp from sending messages to irc");
                                helpItems.AppendLine("  :startxmpp - Enable xmpp sending messages to irc");
                                helpItems.AppendLine("  :> {code} - Process a python command");
                                Add(new MessageItem("HELPZOR", helpItems.ToString(), Destination.Irc));
                                return;
                            }
                            if (item.Message.StartsWith(":>{"))
                            {
                                this.multilinePython = true;
                                item.Message = ":>" + item.Message.Substring(3, item.Message.Length - 3);
                                pythonStatement = new StringBuilder();
                            }
                            if (item.Message.StartsWith(":>}"))
                            {
                                this.multilinePython = false;
                                try
                                {
                                    string cout = "";
                                    var res = PythonEngine.Get().RunString(pythonStatement.ToString(), out cout);
                                    var output = new StringBuilder();
                                    using (var sr = new StringReader(cout))
                                    {
                                        var l = sr.ReadLine();
                                        while (l != null)
                                        {
                                            output.AppendLine("> " + l);
                                            l = sr.ReadLine();
                                        }
                                    }
                                    if (res != null)
                                        output.AppendLine("= " + res);

                                    Add(new MessageItem("", output.ToString(), Destination.Irc));
                                }
                                catch (Exception e)
                                {
                                    Add(new MessageItem("ERRZOR", e.Message, Destination.Irc));
                                }
                                finally
                                {
                                    pythonStatement = null;
                                }
                                return;
                            }
                            if (item.Message.StartsWith(":>"))
                            {
                                try
                                {
                                    if (this.multilinePython)
                                    {
                                        this.pythonStatement.AppendLine(item.Message.Substring(2, item.Message.Length - 2));
                                        return;
                                    }
                                    string cout = "";
                                    var res = PythonEngine.Get().RunString(item.Message.Substring(2, item.Message.Length - 2), out cout);
                                    var output = new StringBuilder();
                                    using (var sr = new StringReader(cout))
                                    {
                                        var l = sr.ReadLine();
                                        while (l != null)
                                        {
                                            output.AppendLine("> " + l);
                                            l = sr.ReadLine();
                                        }
                                    }
                                    if(res != null)
                                        output.AppendLine("= " + res);
                                    
                                    Add(new MessageItem("", output.ToString(), Destination.Irc));
                                }
                                catch (Exception e)
                                {
                                    Add(new MessageItem("ERRZOR", e.Message, Destination.Irc));
                                }
                                return;
                            }
                            Add(new MessageItem("ERRZOR", "Unknown Command: " + item.Message, Destination.Irc));
                        }
                        if (pauseIrc)
                        {
                            return;
                        }
                        var to = new Jid(XmppConfig.MucFullRoom);
                        var j = new Jid(to.Bare);
                        var m = new Message(j, MessageType.groupchat, item.From + ": " + item.Message);
                        m.GenerateId();
                        //m.XEvent = new Event { Delivered = true, Displayed = true };  
                        Program.XmppBot.Con.Send(m);
                        break;
                }

            }
            finally
            {
                Timer.Enabled = true;
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