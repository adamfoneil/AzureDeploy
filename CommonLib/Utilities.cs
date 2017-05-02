using AdamOneilSoftware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AzTrace;

namespace AzDeploy.Common
{
    public static class Utilities
    {
        public static IEnumerable<FileVersion> GetLocalVersions(string folder)
        {
            string[] masks = new string[] { "*.exe", "*.dll" };
            return masks.SelectMany(mask =>
                Directory.GetFiles(folder, mask)
                .Where(f => !f.EndsWith("vshost.exe") && HasVersionInfo(f))
                .Select(f => new FileVersion()
                {
                    Filename = Path.GetFileName(f),
                    Version = GetLocalVersion(f).ToString()
                }));
        }

        private static bool HasVersionInfo(string fileName)
        {
            try
            {
                var fv = FileVersionInfo.GetVersionInfo(fileName);
                var version = new Version(fv.FileVersion);
                return true;
            }
            catch 
            {
                return false;
            }
        }

        public static Version GetLocalVersion(string fileName)
        {
            try
            {
                var fv = FileVersionInfo.GetVersionInfo(fileName);
                return new Version(fv.FileVersion);
            }
            catch (Exception exc)
            {
                Trace.WriteLine(new CallInfo(exc, new { fileName = fileName }));
                throw new Exception($"Failed to get version info from {fileName}: {exc.Message}");
            }
        }

        public static BlobUri VersionInfoUri(string storageAccount, string containerName, string productName)
        {
            return BlobUriBase(storageAccount, containerName, productName, "VersionInfo");
        }

        public static BlobUri BlobUriBase(string storageAccount, string containerName, string productName, string suffix, string folder = null)
        {
            string baseName = productName.Trim();
            if (!string.IsNullOrEmpty(folder)) baseName = folder + "/" + baseName;
            return new BlobUri(storageAccount, containerName, baseName + $".{suffix}.xml");
        }

        public static IEnumerable<FileVersion> GetCloudVersions(string storageAccount, string containerName, string productName)
        {
            var uri = VersionInfoUri(storageAccount, containerName, productName);
            return GetCloudVersions(uri);
        }

        public static async Task<IEnumerable<FileVersion>> GetCloudVersionsAsync(BlobUri uri)
        {
            FileVersionList result = null;
            if (uri.Exists())
            {
                result = await AzureXmlSerializerHelper.DownloadAsync<FileVersionList>(uri);
            }
            else
            {
                result = new FileVersionList();
            }
            return result;
        }

        public static IEnumerable<FileVersion> GetCloudVersions(BlobUri uri)
        {
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
    }
}
