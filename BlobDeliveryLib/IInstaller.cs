using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzDeployLib
{
    public interface IInstaller
    {
        void Run(Engine engine);   
    }
}
