using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AzDeployUI
{
    // thanks to http://stackoverflow.com/questions/18726852/redirecting-console-writeline-to-textbox, answer by Servy

    internal class TextControlWriter : TextWriter
    {
        private readonly Control _control;

        public TextControlWriter(Control control)
        {
            _control = control;
        }

        public override void WriteLine(string value)
        {
            _control.Text += value + "\r\n";            
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }
}
