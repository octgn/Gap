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

                        switch (mess.Endpoint)
                        {
                            case WebhookEndpoint.Octgn:
                                MessageQueue.Get().Add(new MessageItem("Cpt. Hook",mess.Body,Destination.Irc));
                                break;
                            case WebhookEndpoint.OctgnDev:
                                MessageQueue.Get().Add(new MessageItem("Cpt. Hook",mess.Body,Destination.Irc));
                                break;
                            case WebhookEndpoint.OctgnLobby:
                                MessageQueue.Get().Add(new MessageItem("Cpt. Hook",mess.Body,Destination.Xmpp));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(mess.Endpoint.ToString());
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