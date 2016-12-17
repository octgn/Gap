using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Timers;

using agsXMPP;
using agsXMPP.protocol.client;
using log4net;

namespace Gap
{

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

        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        public void Stop()
        {
            realEnd = true;
            Timer.Stop();
            Timer.Dispose();
        }

        private bool pauseIrc = false;
        private bool pauseXmpp = false;
        private bool realEnd = false;

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
                if (item.From.Equals(SlackBot.Config.SlackBotName, StringComparison.InvariantCultureIgnoreCase)) return;

                if (item.Dest.HasFlag(Destination.Xmpp))
                {
                    if (item.Message.StartsWith(":"))
                    {
                        if (item.From.Equals("kellyelton", StringComparison.InvariantCultureIgnoreCase) ||
                            item.From.Equals("kellyelton_", StringComparison.InvariantCultureIgnoreCase)
                             || item.From.Equals("brine", StringComparison.InvariantCultureIgnoreCase)
                            || item.From.Equals("brinelog", StringComparison.InvariantCultureIgnoreCase))
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
                            else if (item.Message.Equals(":kill", StringComparison.InvariantCultureIgnoreCase))
                            {
                                Task.Factory.StartNew(Program.Close);
                                realEnd = true;
                                Timer.Enabled = false;
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
                            helpItems.AppendLine("  :kill - Kills gap");
                            Add(new MessageItem("HELPZOR", helpItems.ToString(), Destination.IrcOctgnLobby));
                            return;
                        }
                        Add(new MessageItem("ERRZOR", "Unknown Command: " + item.Message, Destination.IrcOctgnLobby));
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
                }
                if(item.Dest.HasFlag(Destination.IrcOctgn) || item.Dest.HasFlag(Destination.IrcOctgnDev) || item.Dest.HasFlag(Destination.IrcOctgnLobby))
                {
                    var channel = "";
                    if( item.Dest.HasFlag( Destination.IrcOctgn ) ) {
                        channel = "#octgn";
                    } else if( item.Dest.HasFlag( Destination.IrcOctgnLobby ) ) {
                        channel = "#octgn-lobby";
                    } else if( item.Dest.HasFlag( Destination.IrcOctgnDev ) ) {
                        channel = "#octgn-dev";
                    } else {
                        throw new ArgumentOutOfRangeException(item.Dest.ToString());
                    }
                    if (pauseXmpp && (item.From != "HELPZOR" || item.From != "PYZOR" || item.From != "ERRZOR"))
                        return;
                    using (var sr = new StringReader(item.Message))
                    {
                        var line = sr.ReadLine();
                        while (line != null)
                        {
                            Program.IrcBot.IrcClient.Message(channel, item.From + ": " + line);
                            line = sr.ReadLine();
                            if (line != null)
                                System.Threading.Thread.Sleep(1000);
                        }
                    }
                    if (item.Message.Trim().ToLower().StartsWith("lmgtfy"))
                    {
                        const string furl = "http://www.google.com/search?q={0}&btnI";
                        var query = item.Message.Substring(6).Trim();
                        var cq = System.Uri.EscapeUriString(query);
                        var u = string.Format(furl, cq);

                        var form = string.Format("Here ya go -> {0}", u);
                        form = "Gap: " + form;
                        var to = new Jid(XmppConfig.MucFullRoom);
                        var j = new Jid(to.Bare);
                        var m = new Message(j, MessageType.groupchat, form);
                        m.GenerateId();
                        Program.XmppBot.Con.Send(m);
                        Program.IrcBot.IrcClient.Message(channel, form);
                    }
                }
                if( item.Dest.HasFlag( Destination.SlackGeneral ) ) {
                    Program.SlackBot.SendMessage( "general", item.From, item.Message );
                }
                if( item.Dest.HasFlag( Destination.SlackLobby ) ) {
                    Program.SlackBot.SendMessage( "lobby", item.From, item.Message );
                }
                if( item.Dest.HasFlag( Destination.SlackDev ) ) {
                    Program.SlackBot.SendMessage( "octgn-dev", item.From, item.Message );
                }
            }
            finally
            {
                if (!realEnd)
                    Timer.Enabled = true;
            }
        }

        private string[] lastList = { "TEST SUCCESSFULL {0}" };
        private static readonly Random rand = new Random();

        private string _getTestReplyMessage()
        {
            lock (lastList)
            {
                try
                {
                    using (var wc = new WebClient())
                    {
                        var listString =
                            wc.DownloadString(
                                "https://gist.githubusercontent.com/kellyelton/fad46bbd3e98d2d16a15/raw/TestResponse.txt");
                        using (var sr = new StringReader(listString))
                        {
                            var line = sr.ReadLine() ?? "";
                            if (line.Trim() != "!LIST")
                                throw new Exception("Return data was invalid " + listString);

                            var tempList = new List<string>();
                            while (line != null)
                            {
                                if (line.Contains("{0}"))
                                    tempList.Add(line);
                                line = sr.ReadLine();
                            }
                            if (tempList.Count == 0)
                                throw new Exception("TestResponse data had no items " + listString);

                            lastList = tempList.ToArray();
                        }
                    }

                }
                catch (Exception e)
                {
                    Log.Error("Error _getTestReplyMessage", e);
                }

                var pick = rand.Next(0, lastList.Length);
                return lastList[pick];
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

    [Flags]
    public enum Destination
    {
        IrcOctgn, IrcOctgnLobby, IrcOctgnDev, Xmpp, SlackGeneral, SlackDev, SlackLobby
    }
}