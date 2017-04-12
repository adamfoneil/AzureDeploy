using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzDeploy.Client
{
    public class InstallManager
    {
        private readonly string _installerUri;

        public InstallManager(string installerUri)
        {
            _installerUri = installerUri;
        }

        public static InstallManager FromAppSetting(string name)
        {
            return new InstallManager(ConfigurationManager.AppSettings[name]);
        }        

        public bool NewVersionAvailable()
        {
            throw new NotImplementedException();
        }

        public string Download(bool promptForLocation = false)
        {
            throw new NotImplementedException();
        }

        public void Execute()
        {

        }
    }
}
