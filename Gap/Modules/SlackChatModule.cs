using SlackAPI;
using System;
using System.Linq;
using System.Threading;
using System.Timers;

namespace Gap.Modules
{
    public class SlackChatModule : Module, IRunnableModule
    {
        private static log4net.ILog Log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

        public string BotName { get; set; }
        public string AuthToken { get; set; }

        private SlackSocketClient _client;
        private System.Timers.Timer _timer;

        public override void Configure() {
            base.Configure();
            _client = new SlackSocketClient( AuthToken );
            _client.OnMessageReceived += Client_OnMessageReceived;
            _timer = new System.Timers.Timer( 60000 * 10 );

            Outputs["SlackChannelGeneral"].OnMessage += ChannelGeneral_OnMessage;
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

        private void ChannelGeneral_OnMessage( object sender, MessageEventArgs e ) {
            var item = (MessageItem)e.Message;
            SendMessageToChannel( "general", item );
        }

        private void SendMessageToChannel( string channel, MessageItem message ) {
            while( _client.Channels == null ) {
                RefreshChannelsTimer_Elapsed( null, null );
            }
            var c = _client.Channels.Find( x => x.name.Equals( channel ) );
            if( c == null ) return;

            _client.PostMessage( x => { }, c.id, message.Message, botName: message.From );
        }

        private void Client_OnMessageReceived( SlackAPI.WebSocketMessages.NewMessage obj ) {
            Log.Info( $"#{obj.channel} {obj.user}: {obj.text}" );
            if(obj.channel == "general" ) {
                Inputs["SlackChannelGeneral"].Push( this, new MessageItem( obj.user, obj.text ) );
            }
        }

        private void Client_OnConnected( LoginResponse obj ) {
            Log.Info( nameof( SlackChatModule ) + " Connected" );
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

        protected override void Dispose( bool disposing ) {
            base.Dispose( disposing );
            if( !disposing ) return;

            if( _client != null ) {
                _client.OnMessageReceived -= Client_OnMessageReceived;
            }

            Stop();
        }
    }
}
