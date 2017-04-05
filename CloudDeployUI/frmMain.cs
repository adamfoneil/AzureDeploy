using CloudDeployLib;
using System;
using System.Windows.Forms;

namespace CloudDeployUI
{
    public partial class frmMain : Form
    {
        private Engine _engine = null;

        public frmMain()
        {
            InitializeComponent();
        }

        private void tsbNew_Click(object sender, EventArgs e)
        {
            try
            {
                if (_engine?.IsModified ?? false)
                {
                    if (!_engine.Save()) return;
                }

                _engine = new Engine();
                _engine.FilenamePrompt = PromptFilename;
                propertyGrid1.SelectedObject = _engine;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void tsbOpen_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog dlg = new OpenFileDialog();
                dlg.Filter = "Deployment Files|*.deploy.xml|All Files|*.*";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _engine = Engine.Load<Engine>(dlg.FileName);                    
                    propertyGrid1.SelectedObject = _engine;
                    this.Text = $"Cloud Deployer - {_engine.Filename}";
                    tsbSave.Enabled = true;
                    tsbRun.Enabled = true;
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void tsbSave_Click(object sender, EventArgs e)
        {
            try
            {
                _engine.Save();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private bool PromptFilename(out string fileName)
        {
            fileName = null;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.DefaultExt = "deploy.xml";
            dlg.Filter = "Deployment Files|*.deploy.xml|All Files|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                fileName = dlg.FileName;
                return true;
            }
            return false;
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (_engine?.IsModified ?? false)
                {
                    if (!_engine.Save()) e.Cancel = true;
                }                
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }
    }
}
