using System;
using System.Diagnostics;

namespace CloudDeployLib.Installers
{
    public class Process : IInstaller
    {
        public void Run(Engine engine)
        {
            ProcessStartInfo psi = new ProcessStartInfo(engine.InstallerExecutable);            
            psi.Arguments = BuildArguments(engine.InstallerArguments);
            OnBeforeRun(engine);
            var process = System.Diagnostics.Process.Start(psi);
            process.WaitForExit();
            
            if (engine.InstallerSuccessCode != -1)
            {
                int code = process.ExitCode;
                if (code != engine.InstallerSuccessCode)
                {
                    throw new Exception($"Process {engine.InstallerExecutable} with arguments {psi.Arguments} failed with code {code}.");
                }
            }                            
        }

        /// <summary>
        /// Enables subclasses to execute a pre-build action on an installer, such as setting a version number on the output file
        /// </summary>
        protected virtual void OnBeforeRun(Engine engine)
        {
            // do nothing
        }

        /// <summary>
        /// Enables subclasses to alter the arguments passed to the process, such as by appending switches or surrounding a value in quotes
        /// </summary>
        protected virtual string BuildArguments(string arguments)
        {
            return arguments;
        }
    }
}
