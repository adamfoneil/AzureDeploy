using AzDeploy.Client;
using System;
using System.Windows.Forms;
using System.Linq;

namespace Test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            InstallManager im = new InstallManager("adamosoftware", "install", "BlobakSetup.exe", "Blobak");
            await im.AutoInstallAsync(true);            
        }
    }
}
