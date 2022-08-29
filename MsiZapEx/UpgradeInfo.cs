using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;

namespace MsiZapEx
{
    internal class UpgradeInfo
    {
        public Guid UpgradeCode { get; private set; }
        public List<ProductInfo> RelatedProducts { get; private set; }

        public UpgradeInfo(Guid upgradeCode)
        {
            UpgradeCode = upgradeCode;
            Enumerate();
            if (RelatedProducts.Count == 0)
            {
                throw new FileNotFoundException();
            }
        }

        private void Enumerate()
        {
            RelatedProducts = new List<ProductInfo>();
            GetRelatedProducts(RegistryView.Registry64);
            GetRelatedProducts(RegistryView.Registry32);
        }

        private void GetRelatedProducts(RegistryView view)
        {
            string obfuscatedGuid = GuidEx.MsiObfuscate(UpgradeCode);
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            {
                using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\{obfuscatedGuid}", false))
                {
                    if (k == null)
                    {
                        return;
                    }

                    string[] productCodes = k.GetValueNames();
                    foreach (string p in productCodes)
                    {
                        // Ignore default value
                        if (string.IsNullOrWhiteSpace(p) || p.Equals("@"))
                        {
                            continue;
                        }

                        if (Guid.TryParse(p, out Guid id))
                        {
                            ProductInfo pi = new ProductInfo(p, view);
                            RelatedProducts.Add(pi);

                            Console.WriteLine($"Detected product '{pi.ProductCode}': '{pi.DisplayName}' v{pi.DisplayVersion}, installed in '{pi.InstallLocation}'. It contains {pi.Components.Count} components");
                        }
                    }
                }
            }
        }
    }
}