using System;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                new GithubWebhookParser(),
                new HelpDeskWebhookParser()
            };
        }

        public static WebhookParser Get(WebhookQueueMessage message)
        {
            return Parsers.FirstOrDefault(parser => parser.IsMatch(message));
        }

        public static string Parse(WebhookQueueMessage message)
        {
            var parser = Get(message);
            return parser == null ? null : parser.DoParse(message);
        }

        protected abstract bool IsMatch(WebhookQueueMessage message);
        protected abstract string DoParse(WebhookQueueMessage message);
    }

    public class GithubWebhookParser : WebhookParser
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected override bool IsMatch(WebhookQueueMessage message)
        {
            var ret = message.Body.Contains("https://api.github.com/");
            if (ret == false)
            {
                ret = message.Body.Contains("repository") && message.Body.Contains("3222538");
            }
            return ret;
        }

        protected override string DoParse(WebhookQueueMessage message)
        {
            dynamic d = JsonConvert.DeserializeObject(message.Body);

            string ghmessage = null;
            var action = (string)d.action;
            if (action != null)
            {
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
                                d.issue.html_url);
                            ;
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
            }
            else
            {
                // So far this is just commits
                if (d.commits != null)
                {
                    ghmessage = "";
                    var ctype = d.commits.GetType();
                    if ((d.commits as JArray).Count == 0)
                    {
                        d.commits = new JArray(d.head_commit);
                    }
                    foreach (var com in d.commits)
                    {
                        ghmessage += string.Format("[{0}] {1} made a commit {2}\n", d.repository.name, com.author.name, com.message);
                    }
                }
            }
            return ghmessage;
        }
    }

    public class HelpDeskWebhookParser : WebhookParser
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected override bool IsMatch(WebhookQueueMessage message)
        {
            return message.Body.StartsWith("Ticket:");
        }

        protected override string DoParse(WebhookQueueMessage message)
        {
            try
            {
                var temp = "[{{ticket}} {{requester}}] {{subject}} - {{description}}";
                //Ticket: 582
                //Source: Email
                //Requester: Benjamin Gittus
                //Subject: E-Mail Verification
                //Description: Description

                using (var sr = new StringReader(message.Body))
                {
                    var line = sr.ReadLine();
                    while (line != null)
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        var tag = parts[0].Trim().ToLower();
                        var val = parts[1].Trim();

                        temp = temp.Replace("{{" + tag + "}}", val);

                        line = sr.ReadLine();
                    }
                }
                if (string.IsNullOrEmpty(temp))
                    return null;
                return temp;
            }
            catch (Exception e)
            {
                Log.Error("DoParse",e);
            }
            return null;
        }
    }
}