using System;
using log4net;
using SlackAPI;
using System.Reflection;
using System.Configuration;
using System.Timers;
using System.Linq;
using System.Threading;

namespace Gap
{
    public class SlackBot
    {
        internal static ILog Log = LogManager.GetLogger( MethodBase.GetCurrentMethod().DeclaringType );

        private readonly SlackSocketClient _client;
        private readonly System.Timers.Timer _timer;

        public SlackBot() {
            _client = new SlackSocketClient( Config.SlackAuthToken );
            _client.OnMessageReceived += Client_OnMessageReceived;
            _timer = new System.Timers.Timer( 60000 * 10 );
        }

        public void Start() {
            _timer.Elapsed += RefreshChannelsTimer_Elapsed;
            _timer.Start();
            _client.Connect( Client_OnConnected );
        }

        public void Stop() {
            _timer.Elapsed -= RefreshChannelsTimer_Elapsed;
            _timer.Stop();
            _client.CloseSocket();
        }

        public void SendMessage( string channel, string from, string message ) {
            while (_client.Channels == null ) {
                RefreshChannelsTimer_Elapsed( null, null );
            }
            var c = _client.Channels.Find( x => x.name.Equals( channel ) );
            if( c == null ) return;

            _client.PostMessage( x => { }, c.id, message, botName: from );
        }

        private void Client_OnMessageReceived( SlackAPI.WebSocketMessages.NewMessage obj ) {
            if( obj.channel != "lobby" ) return; // Only process these right now.

            Log.Info( $"#{obj.channel} {obj.user}: {obj.text}" );
            if( obj.text.StartsWith( "@" ) ) return;
            MessageQueue.Get().Add( new MessageItem( obj.user, obj.text, Destination.Xmpp | Destination.IrcOctgnLobby ) );
        }

        private void Client_OnConnected( LoginResponse obj ) {
            Log.Info( nameof( SlackBot ) + " Connected" );
        }

        private void RefreshChannelsTimer_Elapsed( object sender, ElapsedEventArgs e ) {
            try {
                using( var w1 = new ManualResetEventSlim() ) {
                    _client.GetChannelList( ( clr ) => {
                        try {
                            Log.Info( "Got Channels" );
                            _client.Channels = clr.channels.ToList();
                        } finally {
                            w1.Set();
                        }
                    } );

                    w1.Wait();
                }
            } catch( Exception ex ) {
                Log.Error( nameof( RefreshChannelsTimer_Elapsed ), ex );
            }
        }

        public static class Config
        {
            public static string SlackBotName =>
                ConfigurationManager.AppSettings[nameof( SlackBotName )];
            public static string SlackAuthToken =>
                ConfigurationManager.AppSettings[nameof( SlackAuthToken )];
        }
    }
}
