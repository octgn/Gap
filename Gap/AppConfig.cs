using System;
using System.Configuration;

namespace Gap
{
    public class AppConfig
    {
        public static string IrcUsername { get; } = ConfigurationManager.AppSettings[nameof( IrcUsername )];
        public static string IrcPassword { get; } = ConfigurationManager.AppSettings[nameof( IrcPassword )];
        public static string IrcEndpoint { get; } = ConfigurationManager.AppSettings[nameof( IrcEndpoint )];

        public static string SlackBotName { get; } = ConfigurationManager.AppSettings[nameof( SlackBotName )];
        public static string SlackAuthToken { get; } = ConfigurationManager.AppSettings[nameof( SlackAuthToken )];

        public static string XmppUsername { get; } = ConfigurationManager.AppSettings[nameof( XmppUsername )];
        public static string XmppPassword { get; } = ConfigurationManager.AppSettings[nameof( XmppPassword )];
        public static string XmppResource { get; } = ConfigurationManager.AppSettings[nameof( XmppResource )];
        public static string XmppServer { get; } = ConfigurationManager.AppSettings[nameof( XmppServer )];

        public static string AzureServiceBusWebhookQueue { get; } = ConfigurationManager.AppSettings[nameof( AzureServiceBusWebhookQueue )];
    }
}
