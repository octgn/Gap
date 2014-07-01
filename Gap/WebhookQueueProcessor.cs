using System;
using System.Reflection;
using System.Timers;
using Amazon.SQS;
using Amazon.SQS.Model;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

                        if(mess.Body.Contains("https://api.github.com/"))
                        {
                            // Then it's a github whatever
                            dynamic d = JsonConvert.DeserializeObject(mess.Body);

                            var ghmessage = "Could not parse github message";
                            var action = (string)d.action;
                            switch (action)
                            {
                                case "created":
                                    ghmessage = string.Format("[{0}] {1} commented on the issue #{2}: {3} - {4}",
                                        d.repository.name, d.sender.login, d.issue.number, d.issue.title,
                                        d.issue.html_url);
                                    break;
                                case "reopened":
                                    ghmessage = string.Format("[{0}] {1} reopened issue #{2}: {3} - {4}",
                                        d.repository.name, d.sender.login, d.issue.number, d.issue.title,
                                        d.issue.html_url);
                                    break;
                                case "opened":
                                    ghmessage = string.Format("[{0}] {1} opened issue #{2}: {3} - {4}",
                                        d.repository.name, d.sender.login, d.issue.number, d.issue.title,
                                        d.issue.html_url);
                                    if (d.issue != null)
                                    {
                                        ghmessage = string.Format("[{0}] {1} opened issue #{2}: {3} - {4}",
                                            d.repository.name, d.sender.login, d.issue.number, d.issue.title,
                                            d.issue.html_url); ;
                                    }
                                    else if (d.pull_request != null)
                                    {
                                        //ghmessage = string.Format("[{0}] {1} opened issue #{2}: {3} - {4}",
                                        //    d.repository.name, d.sender.login, d.pull_request.number, d.pull_request.title,
                                        //    d.pull_request.html_url);
                                        ghmessage = string.Format("[{0}] {1} closed pull request #{2}: {3} - {4}",
                                            d.repository.name, d.sender.login, d.pull_request.number, d.pull_request.title,
                                            d.pull_request.html_url);
                                    }
                                    else
                                        Log.Error("Github hook failed to find proper action case\n" + mess.Body);
                                    break;
                                case "closed":
                                    if (d.issue != null)
                                    {
                                        ghmessage = string.Format("[{0}] {1} closed issue #{2}: {3} - {4}",
                                            d.repository.name, d.sender.login, d.issue.number, d.issue.title,
                                            d.issue.html_url);
                                    }
                                    else if (d.pull_request != null)
                                    {
                                        ghmessage = string.Format("[{0}] {1} closed pull request #{2}: {3} - {4}",
                                            d.repository.name, d.sender.login, d.pull_request.number, d.pull_request.title,
                                            d.pull_request.html_url);
                                    }
                                    else
                                        Log.Error("Github hook failed to find proper action case\n" + mess.Body);
                                    break;
                                case "started":
                                    ghmessage = string.Format("[{0}] {1} starred repository",d.repository.name,d.sender.login);
                                    break;
                                default:
                                    Log.Error("Github hook failed to find proper action case\n" + mess.Body);
                                    break;
                            }
                            mess.Body = ghmessage;
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
                        //var req2 = new DeleteMessageRequest();
                        //req2.QueueUrl = req.QueueUrl;
                        //req2.ReceiptHandle = m.ReceiptHandle;
                        //client.DeleteMessage(req2);
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