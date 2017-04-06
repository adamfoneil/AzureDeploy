using CloudDeployLib;
using System;
using System.IO;

namespace AzDeploy
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length != 1) throw new InvalidOperationException("CloudDeploy.exe: script file argument is required.");
                string script = Path.GetFullPath(args[0]);
                if (File.Exists(script)) throw new FileNotFoundException($"CloudDeploy.exe: script file {script} not found.");

                Engine e = Engine.Load(script);
                // thanks to http://stackoverflow.com/questions/13002507/how-can-i-call-async-go-method-in-for-example-main, answer by Tim S
                e.ExecuteAsync().Wait();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
            }
        }
    }
}
