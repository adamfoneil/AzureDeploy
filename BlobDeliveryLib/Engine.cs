using AdamOneilSoftware;
using AzDeployLib.Installers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace AzDeployLib
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
        
        public static Engine Load(string fileName)
        {
            return Load<Engine>(fileName);
        }

        public async Task ExecuteAsync()
        {            
            var localFileInfo = GetLocalVersions();            
            var cloudFileInfo = GetCloudVersions();
            IEnumerable<FileVersion> versions = null;

            bool newVersionAvailable = false;
            string newVersionInfo = null;

            if (!cloudFileInfo.Any())
            {
                // no cloud version info present, so we'll assume new version available
                newVersionAvailable = true;
                newVersionInfo = "No cloud version info found, installer will be uploaded.";
                versions = localFileInfo;
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
                    versions = newVersions;
                }

                if (!newVersionAvailable)
                {
                    // have any new files been added?
                    var newFiles = localFileInfo.Where(local => !cloudFileInfo.Any(cloud => local.Filename.Equals(cloud.Filename)));
                    versions = newFiles;
                    newVersionInfo = $"New files added to project: {string.Join(", ", newFiles.Select(fv => fv.Filename))}";
                    newVersionAvailable = newFiles.Any();
                }
            }

            if (newVersionAvailable)
            {
                Console.WriteLine(newVersionInfo);
                Console.WriteLine("AzDeploy: building installer...");
                _installers[Type].Run(this);

                string version = GetProductVersion();
                Console.WriteLine($"AzDeploy: uploading {InstallerOutput}, version {version}...");
                await UploadInstallerAsync(version);

                Console.WriteLine("AzDeploy: uploading new version info...");
                var versionInfoList = new FileVersionList();
                versionInfoList.AddRange(localFileInfo);                
                AzureXmlSerializerHelper.Upload(versionInfoList, VersionInfoUri(), StorageAccountKey);

                Console.WriteLine("AzDeploy: uploading log entry...");
                UploadLogEntry entry = new UploadLogEntry();
                entry.LocalTime = DateTime.Now;
                entry.Files = new FileVersionList(versions);
                entry.Version = version;
                AzureXmlSerializerHelper.Upload(entry, UploadLogUri(), StorageAccountKey);

                Console.WriteLine("AzDeploy: generating log html");
                await BuildUpdateLogAsync();

                Console.WriteLine("AzDeploy: upload completed successfully.");
            }
            else
            {
                Console.WriteLine("AzDeploy: no new version to upload.");
            }
        }

        private async Task BuildUpdateLogAsync()
        {
            await Task.Run(() =>
            {
                const int logCount = 50;
                
                IEnumerable<UploadLogEntry> entries = GetChangeLogEntries();

                // generate html output from the last 50
                string htmlFile = BuildHtmlFromEntries(entries.Take(logCount));

                // upload html output


                // delete log entries after the last 50
                var deleteEntries = entries.Skip(logCount);
            });
        }

        private string BuildHtmlFromEntries(IEnumerable<UploadLogEntry> entries)
        {
            XmlDocument doc = GetEmbeddedDocument("AzDeployLib.Resources.LogTemplate.html");

            XmlNode ndTitle = doc.SelectSingleNode("/html/head/title");
            ndTitle.InnerText = $"{ProductName} Change Log";

            XmlNode ndHeading = doc.SelectSingleNode("/html/body/h1");
            ndHeading.InnerText = ndTitle.InnerText;

            XmlNode ndTable = doc.SelectSingleNode("/html/body/table");

            foreach (var entry in entries)
            {
                ndTitle.AppendChild(entry.ToXhtml(doc));
            }

            string fileName = Path.Combine(Path.GetTempPath(), ProductName + ".ChangeLog.html");
            doc.Save(fileName);
            return fileName;
        }

        private XmlDocument GetEmbeddedDocument(string resourceName)
        {
            XmlDocument result = new XmlDocument();

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                result.Load(stream);
            }

            return result;
        }

        private IEnumerable<UploadLogEntry> GetChangeLogEntries()
        {
            List<UploadLogEntry> results = new List<UploadLogEntry>();

            var container = GetContainer();
            var logEntries = container.ListBlobs(prefix: ProductName.Trim(), useFlatBlobListing: true, blobListingDetails: BlobListingDetails.None);
            foreach (var entry in logEntries)
            {
                results.Add(AzureXmlSerializerHelper.Download<UploadLogEntry>(entry.Uri));
            }
            return results;
        }

        private async Task UploadInstallerAsync(string version)
        {
            CloudBlobContainer container = GetContainer();
            string fileName = Path.GetFileName(InstallerOutput);
            CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
            blob.Metadata.Add("version", version);
            await blob.UploadFromFileAsync(InstallerOutput);
        }

        private CloudBlobContainer GetContainer()
        {
            CloudStorageAccount acct = new CloudStorageAccount(new StorageCredentials(StorageAccountName, StorageAccountKey), true);
            CloudBlobClient client = acct.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            container.CreateIfNotExists();
            return container;
        }

        private BlobUri ChangeLogUri()
        {
            return BlobUriBase("ChangeLog");
        }

        private BlobUri VersionInfoUri()
        {
            return BlobUriBase("VersionInfo");            
        }

        private BlobUri UploadLogUri()
        {
            return BlobUriBase(DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm"), ProductName.Trim());
        }

        private BlobUri BlobUriBase(string suffix, string folder = null)
        {
            string baseName = ProductName.Trim();
            if (!string.IsNullOrEmpty(folder)) baseName = folder + "/" + baseName;
            return new BlobUri(StorageAccountName, ContainerName, baseName + $".{suffix}.xml");
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
                .Select(f => new FileVersion() { Filename = Path.GetFileName(f), Version = LocalVersion(f).ToString() }));
        }

        private static Version LocalVersion(string fileName)
        {
            var fv = FileVersionInfo.GetVersionInfo(fileName);
            return new Version(fv.FileVersion);
        }        
    }
}
