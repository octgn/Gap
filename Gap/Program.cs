using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gap
{
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using NetIrc2;
    using NetIrc2.Events;

    using log4net;

    public class Program
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static IrcBot IrcBot { get; set; }
        public static XmppBot XmppBot { get; set; }
        static void Main(string[] args)
        {
            Task.Factory.StartNew(() => {
            IrcBot = new IrcBot();
            IrcBot.Start();
            }).ContinueWith(a => { 

            Thread.Sleep(10000);
            
            XmppBot = new XmppBot(IrcBot);
            XmppBot.Start();
            });
            while (!Console.KeyAvailable)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
