using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Scripting;

namespace Gap
{
    using IronPython.Hosting;

    using Microsoft.Scripting.Hosting;

    public class PythonEngine
    {
        #region PythonEngineSingleton

        internal static PythonEngine Context { get; set; }

        private static readonly object PythonEngineSingletonLocker = new object();

        public static PythonEngine Get()
        {
            lock (PythonEngineSingletonLocker) return Context ?? (Context = new PythonEngine());
        }

        internal PythonEngine()
        {
            Engine = Python.CreateEngine();
            Scope = Engine.CreateScope();
            OutputStream = new MemoryStream();
            OutputWriter = new StreamWriter(OutputStream);
            Engine.Runtime.IO.SetOutput(OutputStream, OutputWriter);
            Scope.SetVariable("GapAss",Assembly.GetExecutingAssembly());
            string res;
            RunString(
@"import clr
clr.AddReference(GapAss)
import Gap
from Gap import *
def lobby(fromuser, message):
  MessageQueue.Get().Add(MessageItem(fromuser,message,Destination.Xmpp))", out res);
        }

        #endregion Singleton

        internal ScriptEngine Engine { get; set; }
        internal ScriptScope Scope { get; set; }
        internal readonly MemoryStream OutputStream = new MemoryStream();
        internal readonly StreamWriter OutputWriter;

        public string RunString(string code, out string output)
        {
            ScriptSource source = Engine.CreateScriptSourceFromString(code, SourceCodeKind.AutoDetect);
            var res = source.Execute(Scope);
            output = Encoding.UTF8.GetString(OutputStream.ToArray(), 0, (int)OutputStream.Length);
            output = output.Replace("\r\r", "\r");
            OutputStream.SetLength(0);
            if (res == null) return null;
            return res.ToString();
        }
    }
}
