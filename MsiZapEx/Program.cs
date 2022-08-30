using CommandLine;
using System;
using System.Linq;

namespace MsiZapEx
{
    class Program
    {
        // Product registry keys:
        // HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall\<ProductCode>\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\<UpgradeSQUID>\
        // HKEY_CLASSES_ROOT\Installer\UpgradeCodes\<UpgradeSQUID>\
        // HKEY_CLASSES_ROOT\Installer\Products\<ProductSQUID>\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductSQUID>\ 
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductSQUID>\Features\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductSQUID>\InstallProperties\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components\<ComponentSQUID>\

        // Patch registry keys
        // HKEY_CLASSES_ROOT\Installer\Products\<ProductSQUID>\Patches
        // HKEY_CLASSES_ROOT\Installer\Patches\<PatchSQUID>\
        // HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Patches\<PatchSQUID>\

        // Note: SQUID means obfuscated GUID

        public static CmdLineOptions Settings { get; private set; }

        static void Main(string[] args)
        {
            ParserResult<CmdLineOptions> cmdLine = Parser.Default.ParseArguments<CmdLineOptions>(args);
            if (cmdLine.Errors.Count() > 0)
            {
                foreach (Error e in cmdLine.Errors)
                {
                    Console.WriteLine(e.ToString());
                }
                Environment.Exit(1);
            }

            Settings = cmdLine.Value;
            if (!string.IsNullOrEmpty(Settings.UpgradeCode))
            {
                Guid upgradeCode = Settings.Obfuscated ? GuidEx.MsiObfuscate(Settings.UpgradeCode) : new Guid(Settings.UpgradeCode);
                Console.WriteLine($"Upgarde code is {upgradeCode}");

                UpgradeInfo upgrade = new UpgradeInfo(upgradeCode);
            }
            else if (!string.IsNullOrEmpty(Settings.ProductCode))
            {
                Guid productCode = Settings.Obfuscated ? GuidEx.MsiObfuscate(Settings.ProductCode) : new Guid(Settings.ProductCode);
                Console.WriteLine($"Product code is {productCode}");

                ProductInfo product = new ProductInfo(productCode);
            }
        }

        public class CmdLineOptions
        {
            [Option("upgrade-code", Required = false, HelpText = "Detect products by UpgradeCode", Group = "codes")]
            public string UpgradeCode { get; set; }

            [Option("product-code", Required = false, HelpText = "Detect products by ProductCode", Group = "codes")]
            public string ProductCode { get; set; }

            [Option("delete", Required = false, HelpText = "Forcibly remove product's Windows Installer entries from the registry")]
            public bool ForceClean { get; set; }

            [Option("dry-run", Required = false, HelpText = "Do not delete Windows Installer entries. Instead, print anything that would have been deleted")]
            public bool DryRun { get; set; }

            [Option("verbose", Required = false, HelpText = "Verbose logging")]
            public bool Verbose { get; set; }

            [Option("obfuscated", Required = false, HelpText = "The upgrade code or product code where supplied in their obfuscated form")]
            public bool Obfuscated { get; set; }
        }
    }
}