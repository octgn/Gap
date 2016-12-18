using System;
using System.Reflection;
using System.Threading;
using log4net;
using System.Configuration;

namespace Gap
{

    public class Program
    {
        internal static ILog Log = LogManager.GetLogger( MethodBase.GetCurrentMethod().DeclaringType );

        public static XmppBot XmppBot { get; set; }
        public static WebhookQueueProcessor WebhookQueueProcessor { get; set; }

        public static string Taco = "asdf";
        private static bool KeepRunning = true;
        static void Main( string[] args ) {
            Run();
        }

        static void Run() {
            //SlackBot = new SlackBot();
            //SlackBot.Start();
            //await TenSeconds();
            //IrcBot = new IrcBot();
            //IrcBot.Start();
            //await TenSeconds();
            //XmppBot = new XmppBot( IrcBot );
            //XmppBot.Start();
            //await TenSeconds();
            //WebhookQueueProcessor = new WebhookQueueProcessor();
            //WebhookQueueProcessor.Start();

            //if( IrcBot.IrcClient.IsConnected ) {
            //    MessageQueue.Get().Add( new MessageItem( "SYSTEM", string.Format( "OCTGN Gap v{0} Reporting for Duty", typeof( Program ).Assembly.GetName().Version ) ) );
            //    MessageQueue.Get().Add( new MessageItem( "SYSTEM", string.Format( "OCTGN Gap v{0} Reporting for Duty", typeof( Program ).Assembly.GetName().Version ) ) );
            //    MessageQueue.Get().Add( new MessageItem( "SYSTEM", string.Format( "OCTGN Gap v{0} Reporting for Duty", typeof( Program ).Assembly.GetName().Version ) ) );
            //    MessageQueue.Get().Add( new MessageItem( "SYSTEM", string.Format( "OCTGN Gap v{0} Reporting for Duty", typeof( Program ).Assembly.GetName().Version ) ) );
            //    MessageQueue.Get().Add( new MessageItem( "SYSTEM", string.Format( "OCTGN Gap v{0} Reporting for Duty", typeof( Program ).Assembly.GetName().Version ) ) );
            //    MessageQueue.Get().Add( new MessageItem( "SYSTEM", string.Format( "OCTGN Gap v{0} Reporting for Duty", typeof( Program ).Assembly.GetName().Version ) ) );
            //}

            using( Configuration config = Configuration.FromFile( "Configuration.xaml" ) ) {

                config.Configure();

                config.Start();

                while( !Console.KeyAvailable && KeepRunning ) {
                    Thread.Sleep( 1000 );
                }
            }
        }

        public static void Close() {
            try {
                WebhookQueueProcessor.Dispose();
                MessageQueue.Get().Stop();
                XmppBot.Stop();
            } catch( Exception e ) {
                Log.Error( "Close", e );
            }
            KeepRunning = false;
        }
    }

    public class AppConfig
    {
        public static string IrcUsername { get; } = ConfigurationManager.AppSettings[nameof( IrcUsername )];
        public static string IrcPassword { get; } = ConfigurationManager.AppSettings[nameof( IrcPassword )];
        public static string IrcEndpoint { get; } = ConfigurationManager.AppSettings[nameof( IrcEndpoint )];

        public static string SlackBotName { get; } = ConfigurationManager.AppSettings[nameof( SlackBotName )];
        public static string SlackAuthToken { get; } = ConfigurationManager.AppSettings[nameof( SlackAuthToken )];
    }
}
