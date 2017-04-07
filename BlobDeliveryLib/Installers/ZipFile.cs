using System.IO;

namespace AzDeployLib.Installers
{
    public class ZipFile : IInstaller
    {
        public void Run(Engine engine)
        {
            string outputFile = Path.GetFullPath(engine.InstallerOutput);
            if (File.Exists(outputFile)) File.Delete(outputFile);
            System.IO.Compression.ZipFile.CreateFromDirectory(engine.StagingFolder, engine.InstallerOutput, System.IO.Compression.CompressionLevel.Optimal, false);
        }
    }
}
