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
            Outputs["SlackChannelOctgnLobby"].OnMessage += ChannelOctgnLobby_OnMessage;
            Outputs["SlackChannelOctgnDev"].OnMessage += ChannelOctgnDev_OnMessage;
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
            var cid = _client.Channels.First( x => x.name == "general" ).id;
            SendMessageToChannel( cid, item );
        }
        private void ChannelOctgnLobby_OnMessage( object sender, MessageEventArgs e ) {
            var item = (MessageItem)e.Message;
            var cid = _client.Channels.First( x => x.name == "lobby" ).id;
            SendMessageToChannel( cid, item );
        }
        private void ChannelOctgnDev_OnMessage( object sender, MessageEventArgs e ) {
            var item = (MessageItem)e.Message;
            var cid = _client.Groups.First( x => x.name == "octgn-dev" ).id;
            SendMessageToChannel( cid, item );
        }

        private void SendMessageToChannel( string channelId, MessageItem message ) {
            _client.PostMessage( x => { }, channelId, message.Message, botName: message.From );
        }

        private void Client_OnMessageReceived( SlackAPI.WebSocketMessages.NewMessage obj ) {
            // Ignore bot messages
            if( obj.subtype == "bot_message" ) return;

            User from = _client.UserLookup[obj.user];
            string channelName = "";
            if( _client.ChannelLookup.ContainsKey( obj.channel ) ) {
                channelName = _client.ChannelLookup[obj.channel].name;
            } else if( _client.GroupLookup.ContainsKey( obj.channel ) ) {
                channelName = _client.GroupLookup[obj.channel].name;
            }

            Log.Info( $"#{channelName} {from.name}: {obj.text}" );
            if(channelName == "general" ) {
                Inputs["SlackChannelGeneral"].Push( this, new MessageItem( from.name, obj.text ) );
            } else if(channelName == "lobby" ) {
                Inputs["SlackChannelOctgnLobby"].Push( this, new MessageItem( from.name, obj.text ) );
            } else if(channelName == "octgn-dev" ) {
                Inputs["SlackChannelOctgnDev"].Push( this, new MessageItem( from.name, obj.text ) );
            }
        }

        private void Client_OnConnected( LoginResponse obj ) {
            Log.Info( nameof( SlackChatModule ) + " Connected" );
            RefreshChannelsTimer_Elapsed( null, null );
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

            Outputs["SlackChannelGeneral"].OnMessage -= ChannelGeneral_OnMessage;
            Outputs["SlackChannelOctgnLobby"].OnMessage -= ChannelOctgnLobby_OnMessage;
            Outputs["SlackChannelOctgnDev"].OnMessage -= ChannelOctgnDev_OnMessage;

            if( _client != null ) {
                _client.OnMessageReceived -= Client_OnMessageReceived;
            }

            Stop();
        }
    }
}
