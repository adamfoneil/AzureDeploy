using AdamOneilSoftware;
using AzDeploy.Server.Installers;
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

namespace AzDeploy.Server
{
    public enum InstallType
    {
        Process,
        ZipFile,
        DeployMasterProcess,
        BuildOutputZipFile
    }

    public class Engine : Document
    {
        private static Dictionary<InstallType, IInstaller> _installers = new Dictionary<InstallType, IInstaller>()
        {
            { InstallType.Process, new Installers.Process() },
            { InstallType.ZipFile, new ZipFile() },
            { InstallType.DeployMasterProcess, new DeployMaster() },
            { InstallType.BuildOutputZipFile, new BuildOutputZipFile() }
        };        

        [Category("Product")]
        [Description("Local folder, typically bin/Release (use the full path), where the installer gets component files")]
        public string StagingFolder { get { return Get<string>(); } set { Set(value); } }

        [Category("Product")]
        [Description("Name of the product used to indicate where component version info is stored online")]
        public string ProductName { get { return Get<string>(); } set { Set(value); } }

        [Category("Product")]
        [Description("EXE or DLL that defines the version number for the product as a whole (use only the file name without the path, must be within StagingFolder)")]
        public string ProductVersionFile { get { return Get<string>(); } set { Set(value); } }

        public string GetProductVersion()
        {
            return Common.Utilities.GetLocalVersion(Path.Combine(Path.GetFullPath(StagingFolder), ProductVersionFile)).ToString();
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
        [Description("Local filename where storage credentials can be found (use this to keep credentials out of source control)")]
        public string LocalCredentialSource {  get { return Get<string>(); } set { Set(value); } }

        [Category("Blob Storage")]
        [Description("Storage account name")]
        public string StorageAccountName { get { return Get<string>(); } set { Set(value); } }

        [Category("Blob Storage")]
        [Description("Storage account key")]
        public string StorageAccountKey { get { return Get<string>(); } set { Set(value); } }

        [Category("Blob Storage")]
        [Description("Container in which your installer executable is uploaded")]
        public string ContainerName { get { return Get<string>(); } set { Set(value); } }

        [Browsable(false)]
        public string ChangeLogUrl
        {
            get
            {
                var creds = GetAzureCredentials();
                return new BlobUri(creds.AccountName, ContainerName, GetChangeLogFilename()).ToString();
            }
        }

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
            var creds = GetAzureCredentials();

            var localFileInfo = Common.Utilities.GetLocalVersions(StagingFolder);
            var cloudFileInfo = Common.Utilities.GetCloudVersions(creds.AccountName, ContainerName, ProductName);
            IEnumerable<Common.FileVersion> versions = null;

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
                var versionInfoList = new Common.FileVersionList();
                versionInfoList.AddRange(localFileInfo);                
                AzureXmlSerializerHelper.Upload(versionInfoList, Common.Utilities.VersionInfoUri(creds.AccountName, ContainerName, ProductName), creds.AccountKey);

                Console.WriteLine("AzDeploy: uploading log entry...");
                UploadLogEntry entry = new UploadLogEntry();
                entry.LocalTime = DateTime.Now;
                entry.Files = new Common.FileVersionList(versions);
                entry.Version = version;
                AzureXmlSerializerHelper.Upload(entry, UploadLogUri(), creds.AccountKey);

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
                
                string htmlFile = BuildHtmlFromEntries(entries.Take(logCount));
                
                var container = GetContainer();
                var logBlob = container.GetBlockBlobReference(Path.GetFileName(htmlFile));
                logBlob.UploadFromFile(htmlFile);
                // thanks to http://stackoverflow.com/questions/24621664/uploading-blockblob-and-setting-contenttype
                logBlob.Properties.ContentType = "text/html";
                logBlob.SetProperties();
                File.Delete(htmlFile);
                
                var deleteEntries = entries.Skip(logCount);
                //todo: delete entries
            });
        }

        private string BuildHtmlFromEntries(IEnumerable<UploadLogEntry> entries)
        {
            XmlDocument doc = GetEmbeddedDocument("AzDeploy.Server.Resources.LogTemplate.html");

            XmlNode ndTitle = doc.SelectSingleNode("/html/head/title");
            ndTitle.InnerText = $"{ProductName} Change Log";

            XmlNode ndHeading = doc.SelectSingleNode("/html/body/h1");
            ndHeading.InnerText = ndTitle.InnerText;

            XmlNode ndTable = doc.SelectSingleNode("/html/body/table");

            foreach (var entry in entries)
            {
                ndTable.AppendChild(entry.ToXhtml(doc));
            }

            string fileName = Path.Combine(Path.GetTempPath(), GetChangeLogFilename());
            doc.Save(fileName);
            return fileName;
        }

        private string GetChangeLogFilename()
        {
            return ProductName.Trim() + ".ChangeLog.html";
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
            var logEntries = container.ListBlobs(prefix: ProductName.Trim() + "/", useFlatBlobListing: true, blobListingDetails: BlobListingDetails.None);
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
            var creds = GetAzureCredentials();
            CloudStorageAccount acct = new CloudStorageAccount(new StorageCredentials(creds.AccountName, creds.AccountKey), true);
            CloudBlobClient client = acct.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            container.CreateIfNotExists();
            return container;
        }

        private BlobUri UploadLogUri()
        {
            var creds = GetAzureCredentials();
            return Common.Utilities.BlobUriBase(
                creds.AccountName, ContainerName, ProductName, 
                DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm"), ProductName.Trim());
        }

        private AzureCredentials GetAzureCredentials()
        {
            AzureCredentials result = null;
            if (File.Exists(LocalCredentialSource))
            {
                result = XmlSerializerHelper.Load<AzureCredentials>(LocalCredentialSource);
            }
            else
            {
                result = new AzureCredentials() { AccountKey = StorageAccountKey, AccountName = StorageAccountName };
            }

            return result;            
        }

        public void SaveLocalCredentials()
        {
            var creds = new AzureCredentials() { AccountName = StorageAccountName, AccountKey = StorageAccountKey };
            XmlSerializerHelper.Save(creds, LocalCredentialSource);
        }

        public class AzureCredentials
        {
            public string AccountName { get; set; }
            public string AccountKey { get; set; }
        }
    }
}
