using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Deployment.WindowsInstaller;
using System.IO;

namespace MsiZapEx
{
    class Program
    {
        static void Main(string[] args)
        {
            OrphanComponent.DetectOrphanComponents();
        }
    }
}