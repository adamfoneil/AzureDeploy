using AdamOneilSoftware;
using AzDeploy.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AzDeployUI
{
    public partial class frmAzureCreds : Form
    {
        public frmAzureCreds()
        {
            InitializeComponent();
        }

        public string Filename { get; set; }

        private void frmAzureCreds_Load(object sender, EventArgs e)
        {

        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.DefaultExt = "xml";
                dlg.Filter = "XML Files|*.xml|All Files|*.*";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Engine.AzureCredentials creds = new Engine.AzureCredentials() { AccountName = textBox1.Text, AccountKey = textBox2.Text };
                    XmlSerializerHelper.Save(creds, dlg.FileName);
                    this.Filename = dlg.FileName;
                    DialogResult = DialogResult.OK;
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }
    }
}
