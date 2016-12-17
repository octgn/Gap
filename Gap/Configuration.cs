using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Markup;
using System.Xaml;

namespace Gap
{
    [ContentProperty( nameof( Modules ) )]
    public class Configuration : Module
    {
        public List<Module> Modules { get; set; } = new List<Module>();
        public List<MessageRoute> MessageRoutes { get; set; } = new List<MessageRoute>();

        public Configuration() { }

        public static Configuration FromFile(string path) {
            using(FileStream fileStream = File.OpenRead( path ) ) {
                var yourObj = (Configuration)System.Windows.Markup.XamlReader.Load( fileStream );
                return yourObj;
            }
        }
    }

    public class Module
    {
        public string Id { get; set; }

        public virtual MessageItem 
    }

    public class ChatClient : Module
    {

    }

    public class RoutedMessage
    {
        public string From { get; set; }
        public string Message { get; set; }
    }

    public class MessageRoute : Module
    {
        public Module From { get; set; }
        public Module To { get; set; }
    }

    public class Binding : MarkupExtension
    {
        public string Module { get; set; }
        public Binding() { }

        public Binding( string module ) {
            Module = module;
        }

        public override object ProvideValue( IServiceProvider serviceProvider ) {
            var valueService = (IProvideValueTarget)serviceProvider.GetService( typeof( IProvideValueTarget ) );
            if( valueService == null )
                return null;

            var ro = (IRootObjectProvider)serviceProvider.GetService( typeof( IRootObjectProvider ) );
            if( ro == null )
                return null;

            var rootObject = (Configuration)ro.RootObject;


            var property = valueService.TargetProperty as PropertyInfo;
            if( property == null )
                return null;

            return rootObject.Modules.FirstOrDefault( x => x.Id == Module );
        }
    }
}
