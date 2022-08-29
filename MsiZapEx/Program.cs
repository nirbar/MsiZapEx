using CommandLine;
using CommandLine.Text;
using System;
using System.Linq;

namespace MsiZapEx
{
    class Program
    {
        // Product registry keys:
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\<UpgradeSQUID>\
        // HKEY_CLASSES_ROOT\Installer\UpgradeCodes\<UpgradeSQUID>\
        // HKEY_CLASSES_ROOT\Installer\Products\<ProductSQUID>\
        // HKEY_LOCAL_MACHINE\Software\Classes\Installer\Features\<ProductSQUID>\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductSQUID>\ 
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductSQUID>\Features\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductSQUID>\InstallProperties\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components\<ComponentSQUID>\

        // Patch registry keys
        // HKEY_CLASSES_ROOT\Installer\Patches\<ProductSQUID>\
        // HKEY_LOCAL_MACHINE\Software\Classes\Installer\Products\<ProductSQUID>\Patches\
        // HKEY_LOCAL_MACHINE\Software\Classes\Installer\Patches\<PatchSQUID>\
        // HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Patches\<PatchSQUID>\

        // Note: SQUID means obfuscated GUID

        static void Main(string[] args)
        {
            ParserResult<CmdLineOptions> cmdLine = Parser.Default.ParseArguments<CmdLineOptions>(args);
            if (cmdLine.Errors.Count() > 0)
            {
                Console.WriteLine(cmdLine.Errors);
                Environment.Exit(1);
            }
            if (!string.IsNullOrEmpty(cmdLine.Value.UpgradeCode))
            {
                Guid upgradeCode = new Guid(cmdLine.Value.UpgradeCode);
                Console.WriteLine($"Upgarde code is {upgradeCode}");

                UpgradeInfo byUpgradeCode = new UpgradeInfo(upgradeCode);
            }
            else if (!string.IsNullOrEmpty(cmdLine.Value.ProductCode))
            {
                Guid productCode = new Guid(cmdLine.Value.ProductCode);
                Console.WriteLine($"Product code is {productCode}");

                ProductInfo product = new ProductInfo(productCode);
            }
        }

        public class CmdLineOptions
        {            
            [Option("upgrade-code", Required = false, HelpText = "Detect products by UpgradeCode", SetName = "codes")]
            public string UpgradeCode { get; set; }

            [Option("product-code", Required = false, HelpText = "Detect products by ProductCode", SetName = "codes")]
            public string ProductCode { get; set; }

            [Option('d', "delete", Required = false, HelpText = "Forcibly remove product(s)")]
            public bool ForceClean { get; set; }
        }
    }
}