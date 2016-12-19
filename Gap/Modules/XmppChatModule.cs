using agsXMPP;
using agsXMPP.protocol.Base;
using agsXMPP.protocol.client;
using agsXMPP.protocol.iq.agent;
using agsXMPP.protocol.x.muc;
using agsXMPP.Xml.Dom;
using System;
using System.Runtime.CompilerServices;

namespace Gap.Modules
{
    public class XmppChatModule : Module, IRunnableModule
    {
        private static log4net.ILog Log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

        public string Username { get; set; }
        public string Password { get; set; }
        public string Resource { get; set; }
        public string Server { get; set; }

        private XmppClientConnection _client;
        private MucManager _muc;

        public XmppChatModule() {
        }

        public void Start() {
            _client.Open( Username, Password, Resource );

        }

        public void Stop() {
            _client.Close();
        }

        public override void Configure() {
            base.Configure();
            _client = new XmppClientConnection( Server );
            _client.OnXmppConnectionStateChanged += this.XmppOnXmppConnectionStateChanged;
            _client.OnLogin += this.XmppOnLogin;
            _client.OnRosterItem += this.XmppOnRosterItem;
            _client.OnRosterEnd += this.XmppOnRosterEnd;
            _client.OnRosterStart += this.XmppOnRosterStart;
            _client.OnMessage += this.XmppOnMessage;
            _client.OnPresence += this.XmppOnPresence;
            _client.OnAgentItem += this.XmppOnAgentItem;
            _client.OnIq += this.XmppOnIq;
            _client.OnClose += this.XmppOnClose;
            _client.OnError += this.XmppOnError;
            _client.OnSocketError += this.XmppOnSocketError;
            _client.OnStreamError += this.XmppOnStreamError;

            _muc = new MucManager( _client );
            Outputs["XmppLobby"].OnMessage += XmppChatModule_OnMessage;
        }

        private void XmppChatModule_OnMessage( object sender, MessageEventArgs e ) {
            MessageItem item = (MessageItem)e.Message;
            SendMessageToChannel( "lobby", item );
        }

        private void SendMessageToChannel( string channel, MessageItem message ) {
            Jid mucFullRoom = new Jid( $"lobby@conference.{Server}" );
            Jid j = new Jid( mucFullRoom.Bare );
            Message m = new Message( j, MessageType.groupchat, message.From + ": " + message.Message );
            m.GenerateId();
            _client.Send( m );
        }

        #region XmppEvents

        private void XmppOnStreamError( object sender, Element e ) => Trace();
        private void XmppOnSocketError( object sender, Exception ex ) => TraceError( ex );
        private void XmppOnError( object sender, Exception ex ) => TraceError( ex );
        private void XmppOnClose( object sender ) => Trace();
        private void XmppOnIq( object sender, IQ iq ) => Trace();
        private void XmppOnAgentItem( object sender, Agent agent ) => Trace();
        private void XmppOnPresence( object sender, Presence pres ) => Trace();
        private void XmppOnRosterStart( object sender ) => Trace();
        private void XmppOnRosterItem( object sender, RosterItem item ) => Trace();
        private void XmppOnLogin( object sender ) => Trace();
        private void XmppOnXmppConnectionStateChanged( object sender, XmppConnectionState state ) => Trace();

        private void XmppOnMessage( object sender, Message msg ) {
            Trace();
            if( !msg.From.User.Equals( "lobby", StringComparison.InvariantCultureIgnoreCase ) ) return;
            if( string.IsNullOrWhiteSpace( msg.Body ) || String.IsNullOrWhiteSpace( msg.From.Resource ) ) return;
            if( msg.From.Resource.Equals( Username, StringComparison.InvariantCultureIgnoreCase ) ) return;

            Inputs["XmppLobby"].Push( this, new MessageItem( msg.From.Resource, msg.Body ) );
        }

        private void XmppOnRosterEnd( object sender ) {
            // Joins the lobby chat once the connection is ready
            Trace();
            Jid mucFullRoom = new Jid( $"lobby@conference.{Server}" );
            _muc.JoinRoom( mucFullRoom, Username );
        }

        #endregion XmppEvents

        private static void Trace( [CallerMemberName] string method = null ) {
            if( method == null ) return;
            Log.Info( method );
        }

        private static void TraceError( Exception ex, [CallerMemberName] string method = null ) {
            if( method == null ) return;
            Log.Error( method, ex );
        }

        protected override void Dispose( bool disposing ) {
            base.Dispose( disposing );
            if( !disposing ) return;

            Outputs["XmppLobby"].OnMessage -= XmppChatModule_OnMessage;

            if( _client != null ) {
                _client.OnXmppConnectionStateChanged -= this.XmppOnXmppConnectionStateChanged;
                _client.OnLogin -= this.XmppOnLogin;
                _client.OnRosterItem -= this.XmppOnRosterItem;
                _client.OnRosterEnd -= this.XmppOnRosterEnd;
                _client.OnRosterStart -= this.XmppOnRosterStart;
                _client.OnMessage -= this.XmppOnMessage;
                _client.OnPresence -= this.XmppOnPresence;
                _client.OnAgentItem -= this.XmppOnAgentItem;
                _client.OnIq -= this.XmppOnIq;
                _client.OnClose -= this.XmppOnClose;
                _client.OnError -= this.XmppOnError;
                _client.OnSocketError -= this.XmppOnSocketError;
                _client.OnStreamError -= this.XmppOnStreamError;
            }
            Stop();
        }
    }
}
