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
        private StringBuilder pythonStatement;
        private bool multilinePython = false;
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

                if (item.Dest == Destination.Xmpp)
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
                            helpItems.AppendLine("  :> {code} - Process a python command");
                            Add(new MessageItem("HELPZOR", helpItems.ToString(), Destination.IrcOctgnLobby));
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

                                Add(new MessageItem("", output.ToString(), Destination.IrcOctgnLobby));
                            }
                            catch (Exception e)
                            {
                                Add(new MessageItem("ERRZOR", e.Message, Destination.IrcOctgnLobby));
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
                                if (res != null)
                                    output.AppendLine("= " + res);

                                Add(new MessageItem("", output.ToString(), Destination.IrcOctgnLobby));
                            }
                            catch (Exception e)
                            {
                                Add(new MessageItem("ERRZOR", e.Message, Destination.IrcOctgnLobby));
                            }
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
                else
                {
                    var channel = "";
                    switch (item.Dest)
                    {
                        case Destination.IrcOctgn:
                            channel = "#octgn";
                            break;
                        case Destination.IrcOctgnLobby:
                            channel = "#octgn-lobby";
                            break;
                        case Destination.IrcOctgnDev:
                            channel = "#octgn-dev";
                            break;
                        default:
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
                    if (item.Message.ToLower().Contains("test"))
                    {
                        var form = string.Format(_getTestReplyMessage(), item.From);
                        form = "Gap: " + form;
                        var to = new Jid(XmppConfig.MucFullRoom);
                        var j = new Jid(to.Bare);
                        var m = new Message(j, MessageType.groupchat, form);
                        m.GenerateId();
                        Program.XmppBot.Con.Send(m);
                        Program.IrcBot.IrcClient.Message(channel, form);
                    }
                    else if (item.Message.Trim().ToLower().StartsWith("lmgtfy"))
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
            }
            finally
            {
                if (!realEnd)
                    Timer.Enabled = true;
            }
        }

        private string[] lastList = {"TEST SUCCESSFULL {0}"};
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

    public enum Destination
    {
        IrcOctgn, IrcOctgnLobby, IrcOctgnDev, Xmpp
    }
}