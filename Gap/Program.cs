using System;
using System.Reflection;
using System.Threading;
using log4net;

namespace Gap
{
    public class Program
    {
        internal static ILog Log = LogManager.GetLogger( MethodBase.GetCurrentMethod().DeclaringType );

        private static bool KeepRunning = true;
        static void Main( string[] args ) {
            try {
                Run();
            } catch (Exception ex ) {
                Log.Fatal( "Unhandled Exception", ex );
            }
        }

        static void Run() {
            using( Configuration config = Configuration.FromFile( "Configuration.xaml" ) ) {

                config.Configure();

                config.Start();

                while( !Console.KeyAvailable ) {
                    Thread.Sleep( 1000 );
                }
            }
        }
    }
}
