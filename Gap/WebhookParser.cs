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
                new RawWebhookParser(), 
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

    public class RawWebhookParser : WebhookParser
    {
        protected override bool IsMatch(WebhookQueueMessage message)
        {
            return message.Body.StartsWith("RAW:");
        }

        protected override string DoParse(WebhookQueueMessage message)
        {
            var mess = message.Body.Substring(4);
            return mess;
        }
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
            string ghMessage = null;
            string eventType = null;
            try
            {
                eventType = message.Headers["X-GitHub-Event"].First();
            }
            catch (Exception ex)
            {

            }
            if (eventType == null)
            {
                return (ghMessage);
            }

            dynamic d = JsonConvert.DeserializeObject(message.Body);

            switch (eventType)
            {
                case "commit_comment":
                    ghMessage = string.Format("[{0}] {1} commented on the commit {2}: {3}",
                            d.repository.name, d.sender.login, d.comment.commit_id, d.comment.html_url);
                    break;
                case "create":
                    ghMessage = string.Format("[{0}] {1} created the {2} {3}", d.repository.name, d.sender.login, d.ref_type, d.@ref);
                    break;
                case "delete":
                    ghMessage = string.Format("[{0}] {1} deleted the {2} {3}", d.repository.name, d.sender.login, d.ref_type, d.@ref);
                    break;
                case "fork":
                    ghMessage = string.Format("[{0}] {1} forked repository {2}", d.repository.name, d.forkee.owner.login, d.forkee.html_url);
                    break;
                case "gollum":
                    var messages = new List<string>();
                    foreach (var p in d.pages)
                    {
                        messages.Add(string.Format("[{0}] {1} {2} wiki page {3} {4}", d.repository.name, d.sender.login, p.action, p.page_name, p.html_url));
                    }
                    ghMessage = string.Join("\n", messages);
                    break;
                case "issue_comment":
                    ghMessage = string.Format("[{0}] {1} commented on the issue #{2}: {3} - {4}",
                            d.repository.name, d.sender.login, d.issue.number, d.issue.title,
                            d.issue.html_url);
                    break;
                case "issues":
                    if (d.label != null)
                    {
                        ghMessage = string.Format("[{0}] {1} {2} {3} to issue #{4} - {5}", d.repository.name, d.sender.login, d.action, d.label.name, d.issue.number, d.issue.html_url);
                        break;
                    }
                    if (d.assignee != null)
                    {
                        ghMessage = string.Format("[{0}] {1} {2} {3} to issue #{4} - {5}", d.repository.name, d.sender.login, d.action, d.assignee.login, d.issue.number, d.issue.html_url);
                        break;
                    }
                    ghMessage = string.Format("[{0}] {1} {2} issue #{3}: {4} - {5}",
                               d.repository.name, d.sender.login, d.action, d.issue.number, d.issue.title,
                               d.issue.html_url);
                    break;
                case "member":
                    ghMessage = string.Format("[{0}] {1} was {2} as collaborator on repo: {3}", d.repository.name, d.member.login, d.action, d.repository.name);
                    break;
                case "membership":
                    ghMessage = String.Format("[TEAM {0}] {1} was {2} from team: {3} by {4}", d.team.name, d.member.login, d.action, d.team.name, d.sender.login);
                    break;
                case "pull_request_review_comment":
                    ghMessage = string.Format("[{0}] {1} commented on pull request {2} {3}", d.repository.name, d.sender.login, d.pull_request.number, d.pull_request.html_url);
                    break;
                case "ping":
                    ghMessage = "IGNORE";
                    break;
                case "pull_request":
                    if (d.action == "closed")
                    {
                        var v = (bool) d.pull_request.merged;
                        if ((bool)d.pull_request.merged)
                        {
                            ghMessage = string.Format("[{0}] {1} merged pull request #{2}: {3} - {4}",
                                d.repository.name, d.merged_by, d.pull_request.number, d.pull_request.title,
                                d.pull_request.html_url);
                        }
                        else
                        {
                            ghMessage = string.Format("[{0}] {1} closed pull request #{2}: {3} - {4}",
                                d.repository.name, d.merged_by, d.pull_request.number, d.pull_request.title,
                                d.pull_request.html_url);
                        }
                    }
                    else
                    {
                        ghMessage = string.Format("[{0}] {1} {2} pull request #{3}: {4} - {5}",
                                d.repository.name, d.sender.login, d.action, d.pull_request.number, d.pull_request.title,
                                d.pull_request.html_url);
                    }
                    break;
                case "push":
                    if ((d.commits as JArray).Count == 0)
                    {
                        if (d.head_commit != null)
                            d.commits = new JArray(d.head_commit);
                        else
                            return null;
                    }
                    var commitMessages = new List<string>();
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
                        commitMessages.Add(string.Format("[{0}] {1} made a commit {2} -> {3} {4}", d.repository.name, com.author.name, title, desc, com.url));
                    }
                    ghMessage = string.Join("\n", commitMessages);
                    break;
                case "release":
                    ghMessage = string.Format("[{0}] {1} added release: {2} - {3}", d.repository.name, d.sender.login, d.release.name, d.release.html_url);
                    break;
                case "status":
                    ghMessage = "IGNORE";
                    break;
                case "team_add":
                    ghMessage = string.Format("[{0}] add repo {1}", d.team.name, d.repository.name);
                    break;
                case "watch":
                    ghMessage = string.Format("[{0}] {1} starred repository", d.repository.name, d.sender.login);
                    break;
                default:
                    if (d.context != null)
                    {
                        var context = (string)d.context;
                        if (context.StartsWith("continuous-integration"))
                        {
                            ghMessage = string.Format("[{0}] {1} {2}", d.context, d.description, d.target_url);
                        }
                        else if (context.StartsWith("clahub"))
                        {
                            if (d.state != null)
                            {
                                if (d.state == "failure")
                                {
                                    if (d.description == "Not all contributors have signed the Contributor License Agreement.")
                                    {
                                        ghMessage = string.Format("[OCTGN] {0} {1}", d.description, d.target_url);
                                    }
                                    else if (d.description == "One or more of this commit's parents has contributors who have not signed the Contributor License Agreement.")
                                    {
                                        ghMessage = string.Format("[OCTGN] {0} commited {1}, but still hasn't signed the CLA {2}", d.commit.commit.author.name, d.commit.html_url, d.target_url);
                                    }
                                }
                                else if (d.state == "success")
                                {
                                    if (d.description == "All contributors have signed the Contributor License Agreement.")
                                    {
                                        ghMessage = "IGNORE";
                                    }
                                }
                            }
                        }
                        else if (context.StartsWith("Build"))
                        {
                            if (d.description.ToString().Trim().StartsWith("Build started"))
                            {
                                ghMessage = string.Format("[OCTGN] Build started for {0}", d.commit.html_url);
                            }
                            else if (d.description == "Build finished.")
                            {
                                ghMessage = string.Format("[OCTGN] Build {0} for {1}", d.state, d.commit.html_url);
                            }
                            else if (d.description.ToString().Trim().StartsWith("Build triggered"))
                            {
                                ghMessage = string.Format("[OCTGN] Build triggered for {0}", d.commit.html_url);
                            }
                        }
                        else if (context == "OCTGN-PRTester")
                        {
                            ghMessage = string.Format("[OCTGN] " + d.description);
                        }
                    }
                    break;
            }

            return (ghMessage);
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

                var phase = ((string)obj.build.phase).ToLower();

                var status = " ";
                if (obj.build.status != null)
                {
                    status = " marked as " + ((string)obj.build.status).ToLower() + " ";
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