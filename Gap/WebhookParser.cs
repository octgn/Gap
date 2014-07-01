using System;
using System.Linq;
using System.Reflection;
using log4net;
using Newtonsoft.Json;
using Octgn.Site.Api.Models;

namespace Gap
{
    public abstract class WebhookParser
    {
        private static readonly WebhookParser[] Parsers; 

        static WebhookParser()
        {
            Parsers = new WebhookParser[]
            {
                new GithubWebhookParser()
            };
        }

        public static WebhookParser Get(WebhookQueueMessage message)
        {
            return Parsers.FirstOrDefault(parser => parser.IsMatch(message));
        }

        public static string Parse(WebhookQueueMessage message)
        {
            return Get(message).DoParse(message);
        }

        protected abstract bool IsMatch(WebhookQueueMessage message);
        protected abstract string DoParse(WebhookQueueMessage message);
    }

    public class GithubWebhookParser : WebhookParser
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected override bool IsMatch(WebhookQueueMessage message)
        {
            return message.Body.Contains("https://api.github.com/");
        }

        protected override string DoParse(WebhookQueueMessage message)
        {
            dynamic d = JsonConvert.DeserializeObject(message.Body);

            string ghmessage = null;
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
                    if (d.issue != null)
                    {
                        ghmessage = string.Format("[{0}] {1} opened issue #{2}: {3} - {4}",
                            d.repository.name, d.sender.login, d.issue.number, d.issue.title,
                            d.issue.html_url); ;
                    }
                    else if (d.pull_request != null)
                    {
                        ghmessage = string.Format("[{0}] {1} closed pull request #{2}: {3} - {4}",
                            d.repository.name, d.sender.login, d.pull_request.number, d.pull_request.title,
                            d.pull_request.html_url);
                    }
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
                    break;
                case "started":
                    ghmessage = string.Format("[{0}] {1} starred repository", d.repository.name, d.sender.login);
                    break;
            }
            return ghmessage;
        }
    }
}