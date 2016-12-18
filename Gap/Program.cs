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

        public static WebhookQueueProcessor WebhookQueueProcessor { get; set; }

        public static string Taco = "asdf";
        private static bool KeepRunning = true;
        static void Main( string[] args ) {
            Run();
        }

        static void Run() {
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

        public static string XmppUsername { get; } = ConfigurationManager.AppSettings[nameof( XmppUsername )];
        public static string XmppPassword { get; } = ConfigurationManager.AppSettings[nameof( XmppPassword )];
        public static string XmppResource { get; } = ConfigurationManager.AppSettings[nameof( XmppResource )];
        public static string XmppServer { get; } = ConfigurationManager.AppSettings[nameof( XmppServer )];
    }
}
