using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace Gap
{

    public class Program
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static IrcBot IrcBot { get; set; }
        public static XmppBot XmppBot { get; set; }
        public static WebhookQueueProcessor WebhookQueueProcessor { get; set; }
        static void Main(string[] args)
        {
            Run().Wait();
        }

        static async Task Run()
        {
            IrcBot = new IrcBot();
            IrcBot.Start();
            await Task.Factory.StartNew(() => Thread.Sleep(10000));
            XmppBot = new XmppBot(IrcBot);
            XmppBot.Start();
            await Task.Factory.StartNew(() => Thread.Sleep(10000));
            WebhookQueueProcessor = new WebhookQueueProcessor();

            await Task.Factory.StartNew(() =>
            {
                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(1000);
                }
            });
        }
    }
}
