using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace CloudDeployLib.Installers
{
    public class Process : IInstaller
    {
        public void Run(Engine engine)
        {
            ProcessStartInfo psi = new ProcessStartInfo(engine.InstallerExecutable);
            psi.Arguments = engine.InstallerArguments;
            OnRun(engine);
            var process = System.Diagnostics.Process.Start(psi);
            process.WaitForExit();
            
            if (engine.InstallerSuccessCode != -1)
            {
                int code = process.ExitCode;
                if (code != engine.InstallerSuccessCode)
                {
                    throw new Exception($"Process {engine.InstallerExecutable} with arguments {engine.InstallerArguments} failed with code {code}.");
                }
            }                            
        }

        /// <summary>
        /// Enables subclasses to execute a pre-build action on an installer, such as setting a version number on the output file
        /// </summary>
        protected virtual void OnRun(Engine engine)
        {
            // do nothing
        }
    }
}
