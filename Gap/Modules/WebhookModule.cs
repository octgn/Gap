using Microsoft.ServiceBus.Messaging;
using Octgn.Site.Api.Models;
using System;
using System.Linq;

namespace Gap.Modules
{
    public class WebhookModule : Module, IRunnableModule
    {
        private static log4net.ILog Log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

        public string AzureServiceBusWebhookQueue { get; set; }

        private QueueClient _client;

        public void Start() {
            if( _client != null ) return;
            Log.Info( "Starting WebhookModule" );
            Log.Info( "Connection String: " + AzureServiceBusWebhookQueue );
            _client = QueueClient.CreateFromConnectionString( AzureServiceBusWebhookQueue );
            var opts = new OnMessageOptions() {
                AutoComplete = false,
            };
            _client.OnMessage( ProcessMessage, opts );
        }

        public void Stop() {
            if( _client == null ) return;
            Log.Info( "Stopping WebhookModule" );
            _client?.Close();
            _client = null;
        }

        private void ProcessMessage( BrokeredMessage bmess ) {
            try {
                using( bmess ) {
                    var message = bmess.GetBody<WebhookQueueMessage>();
                    var endmessage = WebhookParser.Parse( message );
                    var parsed = true;
                    if( endmessage == null ) {
                        var parser = WebhookParser.Get( message );
                        if( parser == null ) {
                            Log.Error( "Could not find parser for message\n" + message.Body );
                            endmessage = "Could not find parser for message";
                        } else {
                            Log.Error( parser.GetType().Name + " failed to parse\n" + message.Body );
                            if( parser.GetType() == typeof( GithubWebhookParser ) ) {
                                endmessage = parser.GetType().Name + " failed to parse message of event type: " +
                                             message.Headers["X-GitHub-Event"].First();
                            } else {
                                endmessage = parser.GetType().Name + " failed to parse message";
                            }
                        }
                        parsed = false;
                    }
                    if( endmessage != "IGNORE" ) {
                        var messageItem = new MessageItem( "gap", endmessage );
                        switch( message.Endpoint ) {
                            case WebhookEndpoint.Octgn:
                                Inputs["WebhookOctgn"].Push( this, messageItem );
                                break;
                            case WebhookEndpoint.OctgnDev:
                                Inputs["WebhookOctgnDev"].Push( this, messageItem );
                                break;
                            case WebhookEndpoint.OctgnLobby:
                                Inputs["WebhookOctgnLobby"].Push( this, messageItem );
                                break;
                            default:
                                throw new ArgumentOutOfRangeException( message.Endpoint.ToString() );
                        }
                    }

                    if( parsed ) {
                        bmess.Complete();
                    }
                }
            } catch( Exception e ) {
                Log.Error( "ProcessHooksTimerOnElapsed", e );
            }
        }

        protected override void Dispose( bool disposing ) {
            base.Dispose( disposing );
            if( !disposing ) return;

            Stop();
        }
    }
}
