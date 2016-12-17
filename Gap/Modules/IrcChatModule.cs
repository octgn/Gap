using NetIrc2;
using System;
using System.Net;
using NetIrc2.Events;
using System.IO;

namespace Gap.Modules
{
    public class IrcChatModule : Module, IRunnableModule
    {
        private static log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string Username { get; set; }
        public string Password { get; set; }
        public string EndPoint { get; set; }

        private readonly IrcClient _client;

        public IrcChatModule() {
            _client = new IrcClient();
            _client.GotWelcomeMessage += IrcClientOnGotWelcomeMessage;
            _client.GotNotice += IrcClientOnGotNotice;
            _client.GotMessage += IrcClientOnGotMessage;
        }

        public override void Configure() {
            base.Configure();
            Outputs["IrcChannelOctgn"].OnMessage += IrcChatModule_OnMessage;
        }

        private void IrcChatModule_OnMessage( object sender, MessageEventArgs e ) {
            var item = (MessageItem)e.Message;
            MessageChannel( "octgn", item );
        }

        private void MessageChannel(string channel, MessageItem item ) {
            using( StringReader sr = new StringReader( item.Message ) ) {
                string line = sr.ReadLine();
                while( line != null ) {
                    Program.IrcBot.IrcClient.Message( channel, item.From + ": " + line );
                    line = sr.ReadLine();
                    if( line != null )
                        System.Threading.Thread.Sleep( 1000 );
                }
            }
        }

        public void Start() {
            var parts = EndPoint.Split( ':' );
            _client.Connect( parts[0], int.Parse(parts[1]) );
        }

        public void Stop() {
            _client.Close();
        }

        private void IrcClientOnGotWelcomeMessage( object sender, SimpleMessageEventArgs e ) {
            _client.Join("#octgn");
            _client.Join("#octgn-dev");
            _client.Join("#octgn-lobby");
        }

        private void IrcClientOnGotMessage( object sender, ChatMessageEventArgs args ) {
            Log.Info( args.Sender.Username.ToString() + ":" + args.Message.ToString() );
            if( args.Message.StartsWith( "@" ) ) return;
            if( args.Recipient.Equals( "#octgn" ) ) {
                Inputs["IrcChannelOctgn"].Push( this, new MessageItem( args.Sender.Nickname, args.Message ) );
            }
        }

        private void IrcClientOnGotNotice(object sender, ChatMessageEventArgs chatMessageEventArgs)
        {
            Log.InfoFormat("Notice::{0}", chatMessageEventArgs.Message.ToString());
        }

        protected override void Dispose( bool disposing ) {
            base.Dispose( disposing );
            if( !disposing ) return;

            _client.GotWelcomeMessage -= IrcClientOnGotWelcomeMessage;
            _client.GotNotice -= IrcClientOnGotNotice;
            _client.GotMessage -= IrcClientOnGotMessage;
            Stop();
        }
    }
}
