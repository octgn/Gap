using System;
using System.Configuration;
using System.Reflection;
using System.Threading;

using agsXMPP;
using agsXMPP.Xml.Dom;
using agsXMPP.protocol.client;
using agsXMPP.protocol.iq.agent;
using agsXMPP.protocol.iq.roster;
using agsXMPP.protocol.x.muc;

using log4net;
namespace Gap
{

    public class XmppBot
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public IrcBot IrcBot { get; set; }
        public XmppClientConnection Con { get; set; }
        public MucManager Muc { get; set; }
        public XmppBot(IrcBot ircBot)
        {
            IrcBot = ircBot;
        }

        public void Start()
        {
            this.Rebuild();
            Con.Open(XmppConfig.Username, XmppConfig.Password, XmppConfig.Resource);
        }

        public void Stop()
        {
            Con.Close();
        }

        internal void Rebuild()
        {
            if (this.Con != null)
            {
                this.Con.Close();
            }

            if (this.Con == null)
            {
                this.Con = new XmppClientConnection(XmppConfig.Server);
                this.Con.OnXmppConnectionStateChanged += this.XmppOnOnXmppConnectionStateChanged;
                this.Con.OnLogin += this.XmppOnOnLogin;
                this.Con.OnRosterItem += this.XmppOnOnRosterItem;
                this.Con.OnRosterEnd += this.XmppOnOnRosterEnd;
                this.Con.OnRosterStart += this.XmppOnOnRosterStart;
                this.Con.OnMessage += this.XmppOnOnMessage;
                this.Con.OnPresence += this.XmppOnPresence;
                this.Con.OnAgentItem += this.XmppOnOnAgentItem;
                this.Con.OnIq += this.XmppOnOnIq;
                this.Con.OnClose += this.XmppOnOnClose;
                this.Con.OnError += this.XmppOnOnError;
                this.Con.OnSocketError += this.XmppOnOnSocketError;
                this.Con.OnStreamError += this.XmppOnOnStreamError;
                Muc = new MucManager(Con);
            }
        }

        private void XmppOnOnStreamError(object sender, Element e)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnOnSocketError(object sender, Exception ex)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnOnError(object sender, Exception ex)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnOnClose(object sender)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnOnIq(object sender, IQ iq)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnOnAgentItem(object sender, Agent agent)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnPresence(object sender, Presence pres)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnOnMessage(object sender, Message msg)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
            if (msg.From.User.Equals(XmppConfig.MucRoom, StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(msg.Body) || String.IsNullOrWhiteSpace(msg.From.Resource)) return;
                MessageQueue.Get().Add(new MessageItem(msg.From.Resource, msg.Body, Destination.IrcOctgnLobby));
            }
        }

        private void XmppOnOnRosterStart(object sender)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnOnRosterEnd(object sender)
        {
            Muc.JoinRoom(new Jid(XmppConfig.MucFullRoom), XmppConfig.Username);
        }

        private void XmppOnOnRosterItem(object sender, RosterItem item)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnOnLogin(object sender)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }

        private void XmppOnOnXmppConnectionStateChanged(object sender, XmppConnectionState state)
        {
            Log.Info(MethodBase.GetCurrentMethod().Name);
        }
    }
    public static class XmppConfig
    {
        public static string Username
        {
            get
            {
                return ConfigurationManager.AppSettings["XmppUsername"];
            }
        }
        public static string Password
        {
            get
            {
                return ConfigurationManager.AppSettings["XmppPassword"];
            }
        }
        public static string Resource
        {
            get
            {
                return ConfigurationManager.AppSettings["XmppResource"];
            }
        }
        public static string Server
        {
            get
            {
                return ConfigurationManager.AppSettings["XmppServer"];
            }
        }
        public static string MucFullRoom
        {
            get
            {
                return ConfigurationManager.AppSettings["MucFullRoom"];
            }
        }
        public static string MucRoom
        {
            get
            {
                return ConfigurationManager.AppSettings["MucRoom"];
            }
        }
    }
}