﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MsiZapEx
{
    internal class UpgradeInfo
    {
        [Flags]
        public enum StatusFlags
        {
            None = 0,

            HklmUpgarde = 1,
            HkcrUpgarde = HklmUpgarde * 2,

            HklmHkcrProductsMatch = HkcrUpgarde * 2,

            Products = HklmHkcrProductsMatch * 2,
            ProductsGood = Products * 2,

            Good = ProductsGood | Products | HklmHkcrProductsMatch | HkcrUpgarde | HklmUpgarde
        }

        public Guid UpgradeCode { get; private set; }
        public List<ProductInfo> RelatedProducts { get; private set; }
        public StatusFlags Status { get; private set; } = StatusFlags.None;

        public UpgradeInfo(Guid upgradeCode)
        {
            UpgradeCode = upgradeCode;
            Enumerate();
            if (RelatedProducts.Count == 0)
            {
                throw new FileNotFoundException();
            }
            Console.WriteLine($"UpgradeCode '{UpgradeCode}', {RelatedProducts.Count} products, status=0x{Status:X}");
        }

        private void Enumerate()
        {
            RelatedProducts = new List<ProductInfo>();
            GetRelatedProducts();
        }

        public static UpgradeInfo FindByProductCode(Guid productCode)
        {
            string obfuscatedProductCode = GuidEx.MsiObfuscate(productCode);
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
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
                                    UpgradeInfo upgrade = new UpgradeInfo(upgradeCode);
                                    return upgrade;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private void GetRelatedProducts()
        {
            string obfuscatedUpgradeCode = GuidEx.MsiObfuscate(UpgradeCode);
            bool hkcrHklmMatch = true;
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey hkcr = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64))
                {
                    using (RegistryKey ck = hkcr.OpenSubKey($@"Installer\UpgradeCodes\{obfuscatedUpgradeCode}", false))
                    {
                        using (RegistryKey mk = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\{obfuscatedUpgradeCode}", false))
                        {
                            if (ck != null)
                            {
                                Status |= StatusFlags.HkcrUpgarde;

                                string[] obfuscatedProductCodes = ck.GetValueNames();
                                foreach (string p in obfuscatedProductCodes)
                                {
                                    // Ignore default value
                                    if (string.IsNullOrWhiteSpace(p) || p.Equals("@"))
                                    {
                                        continue;
                                    }

                                    if (Guid.TryParse(p, out Guid id))
                                    {
                                        if ((mk == null) || !mk.GetValueNames().Contains(p))
                                        {
                                            hkcrHklmMatch = false;
                                        }

                                        ProductInfo pi = new ProductInfo(p);
                                        RelatedProducts.Add(pi);
                                    }
                                }
                            }

                            if (mk != null)
                            {
                                Status |= StatusFlags.HklmUpgarde;

                                string[] obfuscatedProductCodes = mk.GetValueNames();
                                foreach (string p in obfuscatedProductCodes)
                                {
                                    // Ignore default value
                                    if (string.IsNullOrWhiteSpace(p) || p.Equals("@"))
                                    {
                                        continue;
                                    }

                                    if (Guid.TryParse(p, out Guid id))
                                    {
                                        if (!RelatedProducts.Any(p1 => p.Equals(GuidEx.MsiObfuscate(p1.ProductCode))))
                                        {
                                            hkcrHklmMatch = false;

                                            ProductInfo pi = new ProductInfo(p);
                                            RelatedProducts.Add(pi);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            foreach (ProductInfo pi in RelatedProducts)
            {
                Console.WriteLine($"Detected product '{pi.ProductCode}': '{pi.DisplayName}' v{pi.DisplayVersion}, installed in '{pi.InstallLocation}'. It contains {pi.Components.Count} components. Status=0x{pi.Status:X}");
            }
            if (RelatedProducts.Count > 0)
            {
                Status |= StatusFlags.Products;
                if (RelatedProducts.TrueForAll(p => p.Status == ProductInfo.StatusFlags.Good))
                {
                    Status |= StatusFlags.ProductsGood;
                }
            }
            if (hkcrHklmMatch)
            {
                Status |= StatusFlags.HklmHkcrProductsMatch;
            }
        }

        public void Prune(Guid productCode)
        {
            ProductInfo product = RelatedProducts.First(p => p.ProductCode.Equals(productCode));
            Prune(product);
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

                string obfuscatedUpgradeCode = GuidEx.MsiObfuscate(UpgradeCode);
                string obfuscatedProductCode = GuidEx.MsiObfuscate(product.ProductCode);

                if (RelatedProducts.Count > 1)
                {
                    modifier.DeferDeleteValue(RegistryHive.LocalMachine, RegistryView.Registry64, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\{obfuscatedUpgradeCode}", obfuscatedProductCode);
                    modifier.DeferDeleteValue(RegistryHive.ClassesRoot, RegistryView.Registry64, $@"Installer\UpgradeCodes\{obfuscatedUpgradeCode}", obfuscatedProductCode);
                }
                else
                {
                    modifier.DeferDeleteKey(RegistryHive.LocalMachine, RegistryView.Registry64, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\{obfuscatedUpgradeCode}");
                    modifier.DeferDeleteKey(RegistryHive.ClassesRoot, RegistryView.Registry64, $@"Installer\UpgradeCodes\{obfuscatedUpgradeCode}");
                }
            }
        }
    }
}