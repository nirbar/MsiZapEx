using CommandLine;
using System;
using System.Collections.Generic;

namespace MsiZapEx
{
    public class Settings
    {
        internal static Settings Instance { get; set; }

        [Option("obfuscate-guid", Required = false, HelpText = "Print the obfuscated form of the UUID", Group = "codes")]
        public string ObfuscateGuid { get; set; }

        [Option("bundle-upgrade-code", Required = false, HelpText = "Detect bundles by UpgradeCode", Group = "codes")]
        public string BundleUpgradeCode { get; set; }

        [Option("bundle-product-code", Required = false, HelpText = "Detect bundles by ProductCode", Group = "codes")]
        public string BundleProductCode { get; set; }

        [Option("upgrade-code", Required = false, HelpText = "Detect products by UpgradeCode", Group = "codes")]
        public string UpgradeCode { get; set; }

        [Option("product-code", Required = false, HelpText = "Detect products by ProductCode", Group = "codes")]
        public string ProductCode { get; set; }

        [Option("component-code", Required = false, HelpText = "Detect products by ComponentCode", Group = "codes")]
        public string ComponentCode { get; set; }

        [Option("detect-orphan-products", Required = false, HelpText = "List all products that can't be normally uninstalled due to missing registration data", Group = "codes")]
        public bool DetectOrphanProducts { get; set; }

        [Option("delete", Required = false, HelpText = "Forcibly remove product's Windows Installer entries from the registry", SetName = "delete")]
        public bool ForceClean { get; set; }

        [Option("delete-all-related", Required = false, HelpText = "When used with --upgrade-code or --bundle-upgrade-code, deletes Windows Installer and WiX entries for all related products. When used with --detect-orphan-products, deletes registration for all orphan products", SetName = "delete-all-related")]
        public bool ForceCleanAllRelated { get; set; }

        [Option("dry-run", Required = false, HelpText = "Do not delete Windows Installer entries. Instead, print anything that would have been deleted")]
        public bool DryRun { get; set; }

        [Option("obfuscated", Required = false, HelpText = "The upgrade code or product code or component code were supplied in their obfuscated form")]
        public bool Obfuscated { get; set; }

        [Option("verbose", Required = false, HelpText = "Verbose logging")]
        public bool Verbose { get; set; }

        [Option("shallow", Required = false, HelpText = "Whether or not to enumerate products' components. Specifying this may significally reduce run time", SetName = "shallow")]
        public bool Shallow { get; set; }
    }
}
