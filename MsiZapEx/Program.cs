using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsiZapEx
{
    class Program
    {
        static void Main(string[] args)
        {
            Guid g = new Guid("c301f105-d0a2-4ba9-9de4-498eea078900");
            Console.WriteLine($"From {g.ToString("B")} To {g.MsiObfuscate()} And back to {g.MsiObfuscate().MsiObfuscate().ToString("B")}");

            g = new Guid("B19ED5D0-2262-43F7-AA89-48B43CDEA161");
            Console.WriteLine($"From {g.ToString("B")} To {g.MsiObfuscate()} And back to {g.MsiObfuscate().MsiObfuscate().ToString("B")}");

            g = new Guid("8EEF8D7F-0EFF-5DB7-BEA6-BC2E776A4AAE");
            Console.WriteLine($"From {g.ToString("B")} To {g.MsiObfuscate()} And back to {g.MsiObfuscate().MsiObfuscate().ToString("B")}");
        }
    }
}
