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
        public Dictionary<string, Module> Modules { get; set; } = new Dictionary<string, Module>();
        public List<MessageRoute> MessageRoutes { get; set; } = new List<MessageRoute>();

        public Configuration() { }

        public static Configuration FromFile( string path ) {
            using( FileStream fileStream = File.OpenRead( path ) ) {
                var yourObj = (Configuration)System.Windows.Markup.XamlReader.Load( fileStream );
                return yourObj;
            }
        }
    }

    public class Component : IDisposable
    {

        #region IDisposable Support
        protected bool IsDisposed => disposedValue;
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Component() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose( true );
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class Module : Component
    {
        public virtual Dictionary<string, Input> Inputs { get; set; } = new Dictionary<string, Input>();
        public virtual Dictionary<string, Output> Outputs { get; set; } = new Dictionary<string, Output>();

        public virtual void Configure() {

        }
    }

    public interface IRunnableModule
    {
        void Start();
        void Stop();
    }

    public class Input
    {
        public event EventHandler<MessageEventArgs> OnMessage;

        public void Push( Module from, object message ) {
            Push( this, new MessageEventArgs( from, message ) );
        }

        public void Push( object sender, MessageEventArgs args ) {
            OnMessage?.Invoke( sender, args );
        }
    }

    public class MessageEventArgs : EventArgs
    {
        public Module Origin { get; set; }
        public object Message { get; set; }

        public MessageEventArgs(Module origin, object message ) {
            Origin = origin;
            Message = message;
        }

    }

    public class Output
    {
        public event EventHandler<MessageEventArgs> OnMessage;

        public void Push( Module from, object message ) {
            Push( this, new MessageEventArgs( from, message ) );
        }

        public void Push( object sender, MessageEventArgs args ) {
            OnMessage?.Invoke( sender, args );
        }
    }

    public class MessageRoute : Module
    {
        public Input From { get; set; }
        public Output To { get; set; }

        public override void Configure() {
            base.Configure();
            From.OnMessage += From_OnMessage;
        }

        private void From_OnMessage( object sender, MessageEventArgs e ) {
            To.Push( sender, e );
        }

        protected override void Dispose( bool disposing ) {
            base.Dispose( disposing );
            From.OnMessage -= From_OnMessage;
        }
    }

    public class ModuleExtension : MarkupExtension
    {
        public string Module { get; set; }

        public string Input { get; set; }

        public string Output { get; set; }

        public ModuleExtension( string module ) {
            Module = module;
        }

        public override object ProvideValue( IServiceProvider serviceProvider ) {
            if( string.IsNullOrWhiteSpace( Module ) )
                throw new ArgumentNullException(nameof(Module), "Module must be set" );
            if( !String.IsNullOrWhiteSpace( Input ) && !String.IsNullOrWhiteSpace( Output ) )
                throw new InvalidOperationException( "Can not set both Input and Output" );

            var valueService = (IProvideValueTarget)serviceProvider.GetService( typeof( IProvideValueTarget ) );
            if( valueService == null ) return null;
            var rop = (IRootObjectProvider)serviceProvider.GetService( typeof( IRootObjectProvider ) );
            if( rop == null ) return null;
            var rootObject = (Configuration)rop.RootObject;
            var property = valueService.TargetProperty as PropertyInfo;

            bool mappingToModule = string.IsNullOrWhiteSpace( Input + Output );
            bool mappingToInput = !string.IsNullOrWhiteSpace( Input );
            bool mappingToOutput = !string.IsNullOrWhiteSpace( Output );

            var mod = rootObject.Modules[Module];

            if( mappingToModule ) {
                return mod;
            } else if( mappingToInput ) {
                return mod.Inputs[Input];
            } else if( mappingToOutput ) {
                return mod.Outputs[Output];
            } else throw new InvalidOperationException( "Module reference invalid" );
        }
    }
}
