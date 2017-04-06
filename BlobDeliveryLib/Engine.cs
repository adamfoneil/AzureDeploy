using AdamOneilSoftware;
using CloudDeployLib.Installers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        
        public async Task ExecuteAsync()
        {            
            var localFileInfo = GetLocalVersions();            
            var cloudFileInfo = GetCloudVersions();

            bool newVersionAvailable = false;
            string newVersionInfo = null;

            if (!cloudFileInfo.Any())
            {
                // no cloud version info present, so we'll assume new version available
                newVersionAvailable = true;
                newVersionInfo = "No cloud version info found, installer will be uploaded.";
            }
            else
            {
                // have any local version numbers increased?
                var newVersions = from local in localFileInfo
                                  join cloud in cloudFileInfo on local.Filename equals cloud.Filename
                                  where local.GetVersion() > cloud.GetVersion()
                                  select local;

                if (newVersions.Any())
                {
                    newVersionAvailable = true;
                    newVersionInfo = $"New file versions found: {string.Join(", ", newVersions.Select(fv => $"{fv.Filename} = {fv.Version.ToString()}"))}";
                }

                if (!newVersionAvailable)
                {
                    // have any new files been added?
                    var newFiles = localFileInfo.Where(local => !cloudFileInfo.Any(cloud => local.Filename.Equals(cloud.Filename)));
                    newVersionInfo = $"New files added to project: {string.Join(", ", newFiles.Select(fv => fv.Filename))}";
                    newVersionAvailable = newFiles.Any();
                }
            }

            if (newVersionAvailable)
            {
                Console.WriteLine(newVersionInfo);
                Console.WriteLine("Building installer...");
                _installers[Type].Run(this);

                string version = GetProductVersion();
                Console.WriteLine($"Uploading {InstallerOutput}, version {version}...");
                await Upload(version);

                Console.WriteLine("Uploading new version info...");
                var versionInfoList = new FileVersionList();
                versionInfoList.AddRange(localFileInfo);                
                AzureXmlSerializerHelper.Upload(versionInfoList, VersionInfoUri(), StorageAccountKey);

                Console.WriteLine("Upload completed successfully.");
            }
            else
            {
                Console.WriteLine("No new version to upload.");
            }
        }

        private async Task Upload(string version)
        {
            CloudStorageAccount acct = new CloudStorageAccount(new StorageCredentials(StorageAccountName, StorageAccountKey), true);
            CloudBlobClient client = acct.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            container.CreateIfNotExists();

            string fileName = Path.GetFileName(InstallerOutput);
            CloudBlockBlob blob = container.GetBlockBlobReference(fileName);                
            blob.Metadata.Add("version", version);
            await blob.UploadFromFileAsync(InstallerOutput);
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
                result = new FileVersionList();
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

        private static Version LocalVersion(string fileName)
        {
            var fv = FileVersionInfo.GetVersionInfo(fileName);
            return new Version(fv.FileVersion);
        }        
    }
}
