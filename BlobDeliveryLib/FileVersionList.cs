using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzDeployLib
{
    public class FileVersion
    {
        public string Filename { get; set; }
        public string Version { get; set; }

        public Version GetVersion()
        {
            return new Version(Version);
        }
    }

    public class FileVersionList : List<FileVersion>
    {
        public FileVersionList()
        {
        }

        public FileVersionList(IEnumerable<FileVersion> versions) : base(versions)
        {
        }
    }
}
