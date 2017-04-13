using AdamOneilSoftware;
using AzDeploy.Common;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AzDeploy.Client
{
    public class InstallManager
    {
        private readonly BlobUri _installerUri;
        private readonly BlobUri _versionInfoUri;
        private readonly string _productName;
        private readonly string _installerExe;

        public IEnumerable<FileVersion> NewComponents { get; set; }        

        public InstallManager(string storageAccount, string containerName, string installerExe, string productName)
        {
            _installerUri = new BlobUri(storageAccount, containerName, installerExe);
            _versionInfoUri = Utilities.VersionInfoUri(storageAccount, containerName, productName);
            _productName = productName;
            _installerExe = installerExe;
        }

        public async Task<IEnumerable<FileVersion>> GetNewComponentsAsync()
        {
            List<FileVersion> results = new List<FileVersion>();

            var localVersions = Utilities.GetLocalVersions(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            var cloudVersions = await Utilities.GetCloudVersionsAsync(_versionInfoUri);

            // any where the versions increased
            results.AddRange(
                from cloud in cloudVersions
                join local in localVersions on cloud.Filename equals local.Filename
                where cloud.GetVersion() > local.GetVersion()
                select cloud);

            // any new cloud files not in local
            results.AddRange(cloudVersions.Where(cv => !localVersions.Any(lv => lv.Filename.Equals(cv.Filename))));

            NewComponents = results;
            return results;
        }

        public async Task<bool> IsNewVersionAvailableAsync()
        {
            var result = await GetNewComponentsAsync();
            return result.Any();
        }

        public async Task<string> DownloadAsync(bool promptForLocation = false)
        {
            string localPath = (promptForLocation) ?
                GetSavePath() :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _installerExe);

            CloudBlob blob = new CloudBlob(_installerUri.ToUri());
            await blob.DownloadToFileAsync(localPath, FileMode.Create);

            return localPath;
        }

        public string GetSavePath()
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "EXE Files|*.exe|All Files|*.*";
            if (dlg.ShowDialog() == DialogResult.OK) return dlg.FileName;

            throw new OperationCanceledException("User canceled out of Save dialog.");
        }

        public async Task DownloadAndExecuteAsync()
        {
            if (await IsNewVersionAvailableAsync())
            {
                string localFile = await DownloadAsync();
                ProcessStartInfo psi = new ProcessStartInfo(localFile);
                Process.Start(psi);
                Application.Exit();
            }
        }
    }
}
