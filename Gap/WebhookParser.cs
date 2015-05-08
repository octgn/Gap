using System;
using System.Collections.Generic;
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
                new HelpDeskWebhookParser(),
                new JenkinsWebhookParser(), 
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
                            ghmessage = string.Format("[{0}] {1} opened pull request #{2}: {3} - {4}",
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
                    case "labeled":
                        ghmessage = string.Format("[{0}] {1} added label {2} to issue #{3} - {4}", d.repository.name, d.sender.login,d.label.name,d.issue.number,d.issue.html_url);
                        break;
                    case "unlabeled":
                        ghmessage = string.Format("[{0}] {1} removed label {2} from issue #{3} - {4}", d.repository.name, d.sender.login,d.label.name,d.issue.number,d.issue.html_url);
                        break;
                    case "assigned":
                        ghmessage = string.Format("[{0}] {1} assigned {2} to issue #{3} - {4}", d.repository.name, d.sender.login, d.assignee.login, d.issue.number, d.issue.html_url);
                        break;
                    case "unassigned":
                        ghmessage = string.Format("[{0}] {1} removed {2} from issue #{3} - {4}", d.repository.name, d.sender.login, d.assignee.login, d.issue.number, d.issue.html_url);
                        break;
                    case "synchronize":
                    {
                        ghmessage = string.Format("[{0}] {1} updated pull request #{2}: {3} - {4}",
                                d.repository.name, d.sender.login, d.pull_request.number, d.pull_request.title,
                                d.pull_request.html_url);
                        break;
                    }
                }
            }
            else if (d.@ref != null && d.deleted != null && (bool)d.deleted)
            {
                ghmessage = string.Format("[{0}] {1} deleted the branch {2}", d.repository.name, d.pusher.name, d.@ref);
            }
            else if (d.commits != null)
            {
                if ((d.commits as JArray).Count == 0)
                {
                    if (d.head_commit != null)
                        d.commits = new JArray(d.head_commit);
                    else
                        return null;
                }
                var messages = new List<string>();
                foreach (var com in d.commits)
                {
                    var title = "";
                    var desc = "";
                    using (var sr = new StringReader((string)com.message))
                    {
                        title += sr.ReadLine();
                        sr.ReadLine();
                        var line = sr.ReadLine();
                        while (line != null)
                        {
                            desc += line;
                            line = sr.ReadLine();
                        }
                    }
                    messages.Add(string.Format("[{0}] {1} made a commit {2} -> {3} {4}", d.repository.name, com.author.name, title, desc, com.url));
                }
                ghmessage = string.Join("\n", messages);
            }
            else if (d.pages != null)
            {
                var messages = new List<string>();
                foreach (var p in d.pages)
                {
                    var sum = " ";
                    if (p.summary != null)
                    {
                        sum = " \"" + p.summary + "\" ";
                    }
                    //                       [repo] user action page_name summary url
                    var mess = string.Format("[{0}] {1} {2} {3}{4}{5}",
                        d.repository.name, d.sender.login, p.action, p.page_name, sum,
                        p.html_url);
                    messages.Add(mess);
                }
                ghmessage = string.Join("\n", messages);
            }
            else if (d.context != null)
            {
                var context = (string)d.context;
                if (context.StartsWith("continuous-integration"))
                {
                    ghmessage = string.Format("[{0}] {1} {2}", d.context, d.description, d.target_url);
                }
                else if(context.StartsWith("clahub"))
                {
                    if(d.state != null)
                    {
                        if(d.state == "failure")
                        {
                            if (d.description == "Not all contributors have signed the Contributor License Agreement.")
                            {
                                ghmessage = string.Format("[OCTGN] {0} {1}", d.description, d.target_url);
                            }
                            else if (d.description == "One or more of this commit's parents has contributors who have not signed the Contributor License Agreement.")
                            {
                                ghmessage = string.Format("[OCTGN] {0} commited {1}, but still hasn't signed the CLA {2}", d.commit.commit.author.name, d.commit.html_url, d.target_url);
                            }
                        }
                        else if (d.state == "success")
                        {
                            if (d.description == "All contributors have signed the Contributor License Agreement.")
                            {
                                ghmessage = "IGNORE";
                            }
                        }
                    }
                }
                else if (context.StartsWith("default"))
                {
                    if (d.description.ToString().Trim().StartsWith("Build started"))
                    {
                        ghmessage = string.Format("[OCTGN] Build started for {0}", d.commit.html_url);
                    }
                    else if (d.description == "Build finished.")
                    {
                        ghmessage = string.Format("[OCTGN] Build {0} for {1}", d.state, d.commit.html_url);
                    }
                    else if (d.description.ToString().Trim().StartsWith("Build triggered"))
                    {
                        ghmessage = string.Format("[OCTGN] Build triggered for {0}", d.commit.html_url);
                    }
                }
            }
			else if(d.ref_type != null && d.@ref != null)
            {
                if (d.ref_type == "tag")
                {
                    ghmessage = string.Format("[{0}] {1} created the tag {2}", d.repository.name, d.sender.login, d.@ref);
                }
                else if (d.ref_type == "branch")
                {
                    ghmessage = string.Format("[{0}] {1} created the branch {2}", d.repository.name, d.sender.login, d.@ref);
                }
            }
			else if (d.forkee != null)
			{
			    ghmessage = string.Format("[{0}] {1} forked repository {2}", d.repository.name, d.forkee.owner.login,d.forkee.html_url);
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
                Log.Error("DoParse", e);
            }
            return null;
        }
    }

    public class JenkinsWebhookParser : WebhookParser
    {
        internal static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected override bool IsMatch(WebhookQueueMessage message)
        {
            return message.Body.Contains("\"full_url\":\"http://build.octgn.net/");
        }

        protected override string DoParse(WebhookQueueMessage message)
        {
            try
            {
                //var temp = "[{{name}} {{number}}] {{phase}}{{state}}{{url}}";
                //{
                //  "name": "Octgn.Gap",
                //  "url": "job\/Octgn.Gap\/",
                //  "build": {
                //    "full_url": "http:\/\/build.octgn.net\/job\/Octgn.Gap\/31\/",
                //    "number": 31,
                //    "phase": "STARTED",
                //    "url": "job\/Octgn.Gap\/31\/",
                //    "scm": {
                //      "url": "git@bitbucket.org:kellyelton\/octgn-gap.git",
                //      "branch": "origin\/master",
                //      "commit": "6811c2eba250e603f31723da19636afd7bf40647"
                //    },
                //    "artifacts": {
                      
                //    }
                //  }
                //}

                dynamic obj = JsonConvert.DeserializeObject(message.Body);

                var phase = ((string) obj.build.phase).ToLower();

                var status = " ";
                if (obj.build.status != null)
                {
                    status = " marked as " + ((string)obj.build.status).ToLower() + " " ;
                }

                var ret = string.Format("[BUILD {0} {1}] {2}{3}{4}", obj.name, obj.build.number, phase, status,
                    obj.build.full_url);

                if (string.IsNullOrEmpty(ret))
                    return null;
                return ret;
            }
            catch (Exception e)
            {
                Log.Error("DoParse", e);
            }
            return null;
        }
    }
}