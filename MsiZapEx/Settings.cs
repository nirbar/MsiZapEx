using CommandLine;

namespace MsiZapEx
{
    public class Settings
    {
        internal static Settings Instance { get; set; }

        [Option("upgrade-code", Required = false, HelpText = "Detect products by UpgradeCode", Group = "codes")]
        public string UpgradeCode { get; set; }

        [Option("product-code", Required = false, HelpText = "Detect products by ProductCode", Group = "codes")]
        public string ProductCode { get; set; }

        [Option("delete", Required = false, HelpText = "Forcibly remove product's Windows Installer entries from the registry")]
        public bool ForceClean { get; set; }

        [Option("dry-run", Required = false, HelpText = "Do not delete Windows Installer entries. Instead, print anything that would have been deleted")]
        public bool DryRun { get; set; }

        [Option("obfuscated", Required = false, HelpText = "The upgrade code or product code were supplied in their obfuscated form")]
        public bool Obfuscated { get; set; }
    }
}
