using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using log4net;
using Octgn.Site.Api.Models;
using System.Configuration;
using Microsoft.ServiceBus.Messaging;

namespace Gap
{
    public class WebhookQueueProcessor : IDisposable
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private QueueClient _client;

        public WebhookQueueProcessor() {
        }

        public void Start() {
            Log.Info("Starting WebhookQueueProcessor");
            var constring = ConfigurationManager.AppSettings["AzureServiceBusWebhookQueue"];
            Log.Info("Connection String: " + constring);
            _client = QueueClient.CreateFromConnectionString(constring);
            var opts = new OnMessageOptions() {
                AutoComplete = false,
            };
            _client.OnMessage(ProcessMessage, opts);
        }

        private void ProcessMessage(BrokeredMessage bmess) {
            try {
                using (bmess) {
                    var message = bmess.GetBody<WebhookQueueMessage>();
                    var endmessage = WebhookParser.Parse(message);
                    var parsed = true;
                    if (endmessage == null) {
                        var parser = WebhookParser.Get(message);
                        if (parser == null) {
                            Log.Error("Could not find parser for message\n" + message.Body);
                            endmessage = "Could not find parser for message";
                        } else {
                            Log.Error(parser.GetType().Name + " failed to parse\n" + message.Body);
                            if (parser.GetType() == typeof(GithubWebhookParser)) {
                                endmessage = parser.GetType().Name + " failed to parse message of event type: " +
                                             message.Headers["X-GitHub-Event"].First();
                            } else {
                                endmessage = parser.GetType().Name + " failed to parse message";
                            }
                        }
                        parsed = false;
                    }
                    if (endmessage != "IGNORE") {
                        switch (message.Endpoint) {
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
                            throw new ArgumentOutOfRangeException(message.Endpoint.ToString());
                        }
                    }

                    if (Program.IrcBot.IrcClient.IsConnected && parsed) {
                        bmess.Complete();
                    }
                }
            } catch (Exception e) {
                Log.Error("ProcessHooksTimerOnElapsed", e);
            }
        }

        public void Dispose() {
            try {
                _client?.Close();
                _client = null;
            } catch { }
        }
    }
}