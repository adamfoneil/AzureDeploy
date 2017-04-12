using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzDeploy.Server.Installers
{
    public class BuildOutputZipFile : ZipFile
    {
        public override string[] GetFileList(string folder)
        {
            string[] masks = new string[] { "*.exe", "*.dll" };
            return masks.SelectMany(m =>
            {
                return Directory.GetFiles(folder, m, SearchOption.TopDirectoryOnly);
            }).ToArray();
        }
    }
}
