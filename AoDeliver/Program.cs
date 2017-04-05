using AdamOneilSoftware;
using CloudDeployLib;
using Microsoft.WindowsAzure.Storage.Auth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AoDeliver
{
    class Program
    {
        static void Main(string[] args)
        {
            Engine e = new Engine();
            e.StorageAccountName = "adamosoftware";
            e.StorageAccountKey = "gSJlhScBOAskz9tIM+iUbxYwNozSjR26D+PFLULOmWSE1OoRKwwUO1kyS4kzuwuRjrTwMIori/9lG2+jRZMRRQ==";
            e.ContainerName = "install";
            e.ProductName = "Blobak";
            e.ProductVersionFile = "BlobakUI.exe";
            e.StagingFolder = @"C:\Users\Adam\Dropbox\Visual Studio 2015\Projects\BlobBackupLib\BlobakUIWinForm\bin\Release";
            e.InstallerExecutable = @"C:\Program Files\Just Great Software\DeployMaster\DeployMaster.exe";
            e.InstallerArguments = @"""C:\Users\Adam\Dropbox\Visual Studio 2015\Projects\BlobBackupLib\Setup.deploy"" /b /q";
            //e.Execute();

            e.SaveAs(@"C:\Users\Adam\Dropbox\Visual Studio 2015\Projects\BlobBackupLib\BlobakUIWinForm\Blobak.deploy.xml");
        }
    }
}
