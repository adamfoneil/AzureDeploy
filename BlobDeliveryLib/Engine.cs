using AdamOneilSoftware;
using CloudDeployLib.Installers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CloudDeployLib
{
    public enum InstallType
    {
        Process,
        ZipFile,
        DeployMasterProcess
    }

    public class Engine
    {
        private static Dictionary<InstallType, IInstaller> _installers = new Dictionary<InstallType, IInstaller>()
        {
            { InstallType.Process, new Installers.Process() },
            { InstallType.ZipFile, new ZipFile() },
            { InstallType.DeployMasterProcess, new DeployMaster() }
        };

        public string StagingFolder { get; set; }
        public string ProductName { get; set; }
        public string ProductVersionFile { get; set; }

        public string GetProductVersion()
        {
            return LocalVersion(Path.Combine(StagingFolder, ProductVersionFile)).ToString();
        }

        public InstallType Type { get; set; }
        public string InstallerExecutable { get; set; }
        public int InstallerSuccessCode { get; set; }
        public string InstallerArguments { get; set; }
        public string InstallerOutput { get; set; }

        public string StorageAccountName { get; set; }
        public string StorageAccountKey { get; set; }
        public string ContainerName { get; set; }
        
        public Engine()
        {            
        }

        public static Engine Load(string fileName)
        {
            return XmlSerializerHelper.Load<Engine>(fileName);
        }

        public void Save(string fileName)
        {
            XmlSerializerHelper.Save(this, fileName);
        }

        public void Execute()
        {            
            var localFileInfo = GetLocalVersions();            
            var cloudFileInfo = GetCloudVersions();

            bool newVersionAvailable = false;

            if (!cloudFileInfo.Any())
            {
                // no cloud version info present, so we'll assume new version available
                newVersionAvailable = true;
            }
            else
            {
                // have any local version numbers increased?
                newVersionAvailable = 
                    (from local in localFileInfo
                    join cloud in cloudFileInfo on local.Filename equals cloud.Filename
                    where local.GetVersion() > cloud.GetVersion()
                    select local).Any();

                if (!newVersionAvailable)
                {
                    // have any new files been added?
                    newVersionAvailable = localFileInfo.Any(local => !cloudFileInfo.Any(cloud => local.Filename.Equals(cloud.Filename)));
                }
            }

            if (newVersionAvailable)
            {                
                _installers[Type].Run(this);
                
                Upload(InstallerOutput, LocalVersion(ProductVersionFile).ToString());
                
                var versionInfoList = new FileVersionList();
                versionInfoList.AddRange(localFileInfo);                
                AzureXmlSerializerHelper.Upload(versionInfoList, VersionInfoUri(), StorageAccountKey);
            }
        }

        private void Upload(string installerOutput, string version)
        {
            throw new NotImplementedException();
        }

        private BlobUri VersionInfoUri()
        {
            return new BlobUri(StorageAccountName, ContainerName, ProductName.Trim() + ".VersionInfo.xml");
        }

        private IEnumerable<FileVersion> GetCloudVersions()
        {
            var uri = VersionInfoUri();            
            FileVersionList result = null;
            if (uri.Exists())
            {
                result = AzureXmlSerializerHelper.Download<FileVersionList>(uri);
            }
            else
            {
                return new FileVersionList();
            }
            return result;
        }

        private IEnumerable<FileVersion> GetLocalVersions()
        {
            string[] masks = new string[] { "*.exe", "*.dll" };
            return masks.SelectMany(mask =>
                Directory.GetFiles(StagingFolder, mask)
                .Where(f => !f.EndsWith("vshost.exe"))
                .Select(f => new FileVersion() { Filename = Path.GetFileName(f).ToLower(), Version = LocalVersion(f).ToString() }));
        }

        private static Version OnlineVersion(Dictionary<string, string> versionInfo, string fileName)
        {
            string filenameOnly = Path.GetFileName(fileName);
            if (versionInfo.ContainsKey(filenameOnly))
            {
                return new Version(versionInfo[filenameOnly]);
            }
            else
            {
                return new Version("0.0.0");
            }
        }

        private static Version LocalVersion(string fileName)
        {
            var fv = FileVersionInfo.GetVersionInfo(fileName);
            return new Version(fv.FileVersion);
        }
    }
}
