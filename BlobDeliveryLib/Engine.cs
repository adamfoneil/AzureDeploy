using AdamOneilSoftware;
using CloudDeployLib.Installers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    public class Engine : Document
    {
        private static Dictionary<InstallType, IInstaller> _installers = new Dictionary<InstallType, IInstaller>()
        {
            { InstallType.Process, new Installers.Process() },
            { InstallType.ZipFile, new ZipFile() },
            { InstallType.DeployMasterProcess, new DeployMaster() }
        };        

        [Category("Product")]
        [Description("Local folder, typically bin/Release, where the installer gets component files")]
        public string StagingFolder { get { return Get<string>(); } set { Set(value); } }

        [Category("Product")]
        [Description("Name of the product used to indicate where component version info is stored online")]
        public string ProductName { get { return Get<string>(); } set { Set(value); } }

        [Category("Product")]
        [Description("EXE or DLL that defines the version number for the product as a whole")]
        public string ProductVersionFile { get { return Get<string>(); } set { Set(value); } }

        public string GetProductVersion()
        {
            return LocalVersion(Path.Combine(Path.GetFullPath(StagingFolder), ProductVersionFile)).ToString();
        }

        [Category("Installer")]
        [Description("Type of installer")]
        public InstallType Type { get { return Get<InstallType>(); } set { Set(value); } }

        [Category("Installer")]
        [Description("EXE file that builds your installer")]
        public string InstallerExecutable { get { return Get<string>(); } set { Set(value); } }

        [Category("Installer")]
        [Description("Installer process return value that indicates success. Use -1 to ignore")]
        public int InstallerSuccessCode { get { return Get<int>(); } set { Set(value); } }

        [Category("Installer")]
        [Description("Any arguments passed to the installer executable")]
        public string InstallerArguments { get { return Get<string>(); } set { Set(value); } }

        [Category("Installer")]
        [Description("Installer run by the end user")]
        public string InstallerOutput { get { return Get<string>(); } set { Set(value); } }

        [Category("Blob Storage")]
        [Description("Storage account name")]
        public string StorageAccountName { get { return Get<string>(); } set { Set(value); } }

        [Category("Blob Storage")]
        [Description("Storage account key")]
        public string StorageAccountKey { get { return Get<string>(); } set { Set(value); } }

        [Category("Blob Storage")]
        [Description("Container in which your installer executable is uploaded")]
        public string ContainerName { get { return Get<string>(); } set { Set(value); } }

        public Engine()
        {
            InstallerSuccessCode = -1;
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
