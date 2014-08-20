using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Caching;
using System.Timers;
using Amazon.CloudFront.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using IronPython.Modules;
using log4net;
using Newtonsoft.Json;
using Octgn.Site.Api.Models;

namespace Gap
{
    public class WebhookQueueProcessor : IDisposable
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Timer _processHooksTimer;
        private readonly MemoryCache _processedMessages = new MemoryCache("ProcessedMessages");

        public WebhookQueueProcessor()
        {
            _processHooksTimer = new Timer(500);
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
                        if (_processedMessages.Contains(m.MessageId))
                            continue;
                        _processedMessages.Add(m.MessageId,m.MessageId,DateTimeOffset.Now.AddHours(1));
                        var mess = JsonConvert.DeserializeObject<WebhookQueueMessage>(m.Body);

                        var endmessage = WebhookParser.Parse(mess);
                        var parsed = true;
                        if (endmessage == null)
                        {
                            var parser = WebhookParser.Get(mess);
                            if (parser == null)
                            {
                                Log.Error("Could not find parser for message\n" + mess.Body);
                                endmessage = "Could not find parser for message";
                            }
                            else
                            {
                                Log.Error(parser.GetType().Name + " failed to parse\n" + mess.Body);
                                endmessage = parser.GetType().Name + " failed to parse message";
                            }
                            parsed = false;
                        }

                        switch (mess.Endpoint)
                        {
                            case WebhookEndpoint.Octgn:
                                MessageQueue.Get().Add(new MessageItem("Cpt. Hook", endmessage, Destination.IrcOctgn));
                                break;
                            case WebhookEndpoint.OctgnDev:
                                MessageQueue.Get().Add(new MessageItem("Cpt. Hook", endmessage, Destination.IrcOctgnDev));
                                break;
                            case WebhookEndpoint.OctgnLobby:
                                MessageQueue.Get().Add(new MessageItem("Cpt. Hook", endmessage, Destination.IrcOctgnLobby));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(mess.Endpoint.ToString());
                        }
                        if (Program.IrcBot.IrcClient.IsConnected && parsed)
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