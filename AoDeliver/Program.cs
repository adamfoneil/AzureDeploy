using CloudDeployLib;
using System;
using System.IO;

namespace CloudDeploy
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length != 1) throw new InvalidOperationException("CloudDeploy.exe: script file argument is required.");
                string script = args[0];
                if (File.Exists(script)) throw new FileNotFoundException($"CloudDeploy.exe: script file {script} not found.");

                Engine e = Engine.Load(args[0]);
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
