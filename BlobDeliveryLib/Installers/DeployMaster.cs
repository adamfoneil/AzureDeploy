using System.IO;

namespace AzDeploy.Server.Installers
{
    public class DeployMaster : Process
    {
        private string[] _lines;

        private const int _versionLine = 6; // line number in script where the main version number is

        protected override string BuildArguments(string arguments)
        {
            return "\"" + arguments + "\" /b /q";
        }

        protected override void OnBeforeRun(Engine engine)
        {
            // read all lines of the script
            _lines = File.ReadAllLines(engine.InstallerArguments);

            // set the new version number
            _lines[_versionLine] = engine.GetProductVersion();

            // save the new script
            using (StreamWriter writer = File.CreateText(engine.InstallerArguments))
            {                
                foreach (string line in _lines) writer.WriteLine(line);
            }
        }
    }
}
