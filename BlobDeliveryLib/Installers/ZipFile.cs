using AdamOneilSoftware;
using System.IO;
using System.IO.Compression;

namespace AzDeploy.Server.Installers
{
    public class ZipFile : IInstaller
    {
        public void Run(Engine engine)
        {
            string outputFile = Path.GetFullPath(engine.InstallerOutput);
            if (File.Exists(outputFile)) File.Delete(outputFile);

            string[] files = GetFileList(engine.StagingFolder);

            string basePath = FileSystem.CommonBasePath(files);            
            
            // thanks for some help from accepted answer at http://stackoverflow.com/questions/33687425/central-directory-corrupt-error-in-ziparchive
            using (ZipArchive archive = System.IO.Compression.ZipFile.Open(outputFile, ZipArchiveMode.Create))
            {
                foreach (var fileName in files)
                {
                    archive.CreateEntryFromFile(fileName, fileName.Substring(basePath.Length + 1), CompressionLevel.Optimal);
                }                    
            }                                                   
        }

        public virtual string[] GetFileList(string folder)
        {
            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        }
    }
}
