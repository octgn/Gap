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
            Engine.CreateScope();
        }

        #endregion Singleton

        internal ScriptEngine Engine { get; set; }
        internal ScriptScope Scope { get; set; }

        
    }
}
