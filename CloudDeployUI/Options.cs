using AdamOneilSoftware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzDeployUI
{
    public class Options : UserOptionsBase
    {
        public FormPosition MainFormPosition { get; set; }
        public string AzDeployPath { get; set; }
    }
}
