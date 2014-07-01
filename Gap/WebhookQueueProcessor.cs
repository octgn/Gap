using System;
using System.Reflection;
using System.Timers;
using Amazon.SQS;
using Amazon.SQS.Model;
using log4net;
using Newtonsoft.Json;
using Octgn.Site.Api.Models;

namespace Gap
{
    public class WebhookQueueProcessor : IDisposable
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Timer _processHooksTimer;

        public WebhookQueueProcessor()
        {
            _processHooksTimer = new Timer(2000);
            _processHooksTimer.Elapsed += ProcessHooksTimerOnElapsed;
        }

        public void Start()
        {
            Log.Info("Starting WebhookQueueProcessor");
            _processHooksTimer.Start();
        }

        private void ProcessHooksTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            try
            {
                _processHooksTimer.Enabled = false;
                using (var client = new AmazonSQSClient())
                {
                    var req = new ReceiveMessageRequest();
                    req.MaxNumberOfMessages = 5;
                    req.QueueUrl = "https://sqs.us-east-1.amazonaws.com/194315037322/WebhookQueue";

                    var resp = client.ReceiveMessage(req);

                    foreach (var m in resp.Messages)
                    {
                        var mess = JsonConvert.DeserializeObject<WebhookQueueMessage>(m.Body);

                        var endmessage = WebhookParser.Parse(mess);

                        if (endmessage == null)
                        {
                            var parser = WebhookParser.Get(mess);
                            if (parser == null)
                            {
                                Log.Error("Could not find parser for message\n" + mess.Body);
                                mess.Body = "Could not find parser for message";
                            }
                            else
                            {
                                Log.Error(parser.GetType().Name + " failed to parse\n" + mess.Body);
                                mess.Body = parser.GetType().Name + " failed to parse message";
                            }
                        }

                        switch (mess.Endpoint)
                        {
                            case WebhookEndpoint.Octgn:
                                MessageQueue.Get().Add(new MessageItem("Cpt. Hook",mess.Body,Destination.IrcOctgn));
                                break;
                            case WebhookEndpoint.OctgnDev:
                                MessageQueue.Get().Add(new MessageItem("Cpt. Hook", mess.Body, Destination.IrcOctgnDev));
                                break;
                            case WebhookEndpoint.OctgnLobby:
                                MessageQueue.Get().Add(new MessageItem("Cpt. Hook", mess.Body, Destination.IrcOctgnLobby));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(mess.Endpoint.ToString());
                        }
                        if (Program.IrcBot.IrcClient.IsConnected)
                        {
                            var req2 = new DeleteMessageRequest();
                            req2.QueueUrl = req.QueueUrl;
                            req2.ReceiptHandle = m.ReceiptHandle;
                            client.DeleteMessage(req2);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("ProcessHooksTimerOnElapsed",e);
            }
            finally
            {
                _processHooksTimer.Enabled = true;
            }
        }

        public void Dispose()
        {
            _processHooksTimer.Elapsed -= ProcessHooksTimerOnElapsed;
        }
    }
}