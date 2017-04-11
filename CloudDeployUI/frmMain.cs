using AdamOneilSoftware;
using AzDeployLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace AzDeployUI
{
    public partial class frmMain : Form
    {
        private Engine _engine = null;
        private Options _options = null;

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
                tsbSave.Enabled = true;
                tsbRun.Enabled = true;
                tsbAddToProject.Enabled = true;
                tsbChangeLog.Enabled = true;
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
                    tsbAddToProject.Enabled = true;
                    tsbChangeLog.Enabled = true;
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
                this.Text = $"Cloud Deployer - {_engine.Filename}";
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

        private async void tsbRun_Click(object sender, EventArgs e)
        {
            try
            {
                Console.SetOut(new TextControlWriter(tbConsole));
                tabControl1.SelectedIndex = 1;
                await _engine.ExecuteAsync();                
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
            finally
            {
                Console.SetOut(Console.Out);
            }
        }

        private void tsbAddToProject_Click(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(_options.AzDeployPath))
                {
                    MessageBox.Show("The full path to AzDeploy.exe has not been set yet. You will select this next.");
                    SelectAzDeployPath();
                }

                if (MessageBox.Show("This will add a post-build event call to AzDeploy.exe using this script. You will select a .csproj file next.", "Add To Project", MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    OpenFileDialog dlg = new OpenFileDialog();
                    dlg.Filter = "Visual Studio Project Files|*.csproj|All Files|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        AddBuildEvent(dlg.FileName, _engine.Filename);
                    }
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void AddBuildEvent(string projectFile, string deployScript)
        {
            if (IsXmlFile(projectFile))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(projectFile);

                // xpath help from http://stackoverflow.com/questions/4313520/is-it-possible-to-ignore-namespaces-in-c-sharp-when-using-xpath, answer by Martin Honnen
                XmlNode ndPostBuild = doc.SelectSingleNode("/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='PostBuildEvent']");
                if (ndPostBuild != null)
                {
                    if (MessageBox.Show($"The project already has this post build event:\r\n{ndPostBuild.InnerText}\r\n\r\nDo you want to replace it?", "Replace Post Build Event", MessageBoxButtons.YesNo) == DialogResult.No) return;
                }
                else
                {
                    // removing namespace thanks to http://stackoverflow.com/questions/135000/how-to-prevent-blank-xmlns-attributes-in-output-from-nets-xmldocument, CJohnson
                    XmlElement elPropertyGroup = doc.CreateElement("PropertyGroup", doc.DocumentElement.NamespaceURI);
                    ndPostBuild = doc.CreateElement("PostBuildEvent", doc.DocumentElement.NamespaceURI);
                    doc.DocumentElement.AppendChild(elPropertyGroup);
                    elPropertyGroup.AppendChild(ndPostBuild);
                }

                ndPostBuild.InnerText = $"\"{_options.AzDeployPath}\" \"{FileSystem.GetRelativePath(Path.Combine(_engine.StagingFolder, _engine.ProductVersionFile), deployScript)}\"";

                doc.Save(projectFile);
            }
            else
            {
                throw new NotImplementedException("Visual Studio 2017 not supported yet.");
            }
        }

        private bool IsXmlFile(string fileName)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(fileName);
                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            try
            {
                _options = UserOptionsBase.Load<Options>("Options.xml", this);
                _options.RestoreFormPosition(_options.MainFormPosition, this);
                _options.TrackFormPosition(this, (fp) => _options.MainFormPosition = fp);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void btnAzDeployPath_Click(object sender, EventArgs e)
        {
            try
            {
                SelectAzDeployPath();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void SelectAzDeployPath()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Exe files|*.exe|All Files|*.*";
            dlg.FileName = _options.AzDeployPath;
            dlg.Title = "Select Path of AzDeploy.exe";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _options.AzDeployPath = dlg.FileName;
            }
        }

        private void tsbChangeLog_Click(object sender, EventArgs e)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(_engine.ChangeLogUrl);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }
    }
}
