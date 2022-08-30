using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MsiZapEx
{
    internal class UpgradeInfo
    {
        public Guid UpgradeCode { get; private set; }
        public List<ProductInfo> RelatedProducts { get; private set; }
        public RegistryView View { get; private set; }

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

        public static UpgradeInfo FindByProductCode(Guid productCode)
        {
            return FindByProductCode(productCode, RegistryView.Registry64)
                ?? FindByProductCode(productCode, RegistryView.Registry32);
        }

        private static UpgradeInfo FindByProductCode(Guid productCode, RegistryView view)
        {
            string obfuscatedProductCode = GuidEx.MsiObfuscate(productCode);
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            {
                using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\", false))
                {
                    if (k == null)
                    {
                        throw new FileNotFoundException();
                    }

                    string[] obfuscatedUpgradeCodes = k.GetSubKeyNames();
                    foreach (string u in obfuscatedUpgradeCodes)
                    {
                        if (Guid.TryParse(u, out Guid id))
                        {
                            using (RegistryKey uk = k.OpenSubKey(u, false))
                            {
                                string[] obfuscatedProductCodes = uk.GetValueNames();
                                if (obfuscatedProductCodes.Contains(obfuscatedProductCode))
                                {
                                    Guid upgradeCode = GuidEx.MsiObfuscate(u);
                                    Console.WriteLine($"Detected upgarde code '{upgradeCode}'");
                                    UpgradeInfo upgrade = new UpgradeInfo(upgradeCode);
                                    upgrade.View = view;
                                    return upgrade;
                                }
                            }
                        }
                    }
                }
            }
            return null;
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

                    View = view;
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

        public void Prune(ProductInfo product)
        {
            if (!RelatedProducts.Contains(product))
            {
                throw new FileNotFoundException();
            }

            using (RegistryModifier modifier = new RegistryModifier())
            {
                product.Prune(modifier);
                RelatedProducts.Remove(product);

                string obfuscatedUpgradeCode = GuidEx.MsiObfuscate(UpgradeCode);
                string obfuscatedProductCode = GuidEx.MsiObfuscate(product.ProductCode);

                if (RelatedProducts.Count > 0)
                {
                    modifier.DeferDeleteValue(RegistryHive.LocalMachine, View, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\{obfuscatedUpgradeCode}", obfuscatedProductCode);
                    modifier.DeferDeleteValue(RegistryHive.ClassesRoot, View, $@"Installer\UpgradeCodes\{obfuscatedUpgradeCode}", obfuscatedProductCode);
                }
                else
                {
                    modifier.DeferDeleteKey(RegistryHive.LocalMachine, View, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\{obfuscatedUpgradeCode}");
                    modifier.DeferDeleteKey(RegistryHive.ClassesRoot, View, $@"Installer\UpgradeCodes\{obfuscatedUpgradeCode}");
                }
            }
        }
    }
}