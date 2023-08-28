using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MsiZapEx
{
    public class UpgradeInfo
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

        public UpgradeInfo(Guid upgradeCode, bool shallow = false)
        {
            UpgradeCode = upgradeCode;
            Enumerate(shallow);
        }

        public void PrintState()
        {
            if (Status == StatusFlags.None)
            {
                Console.WriteLine($"UpgradeCode not found");
                return;
            }

            Console.WriteLine($"UpgradeCode '{UpgradeCode}', {RelatedProducts.Count} products");
            if (!Status.HasFlag(StatusFlags.HkcrUpgarde))
            {
                Console.WriteLine($@"{'\t'}Missing HKCR key under 'Installer\UpgradeCodes");
            }
            if (!Status.HasFlag(StatusFlags.HklmHkcrProductsMatch))
            {
                Console.WriteLine($@"{'\t'}HKCR key 'Installer\UpgradeCodes and HKLM key 'SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes' have mismatching products");
            }
            if (!Status.HasFlag(StatusFlags.HklmUpgarde))
            {
                Console.WriteLine($@"{'\t'}Missing HKLM key under 'SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes");
            }

            if (RelatedProducts.Count == 0)
            {
                Console.WriteLine($"\tNo related products detected");
            }
            foreach (ProductInfo product in RelatedProducts)
            {
                product.PrintState();
            }
        }

        private void Enumerate(bool shallow)
        {
            RelatedProducts = new List<ProductInfo>();
            GetRelatedProducts(shallow);
        }

        public static UpgradeInfo FindByProductCode(Guid productCode, bool shallow = false)
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
                                    UpgradeInfo upgrade = new UpgradeInfo(upgradeCode, shallow);
                                    return upgrade;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private void GetRelatedProducts(bool shallow)
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

                                        ProductInfo pi = new ProductInfo(p, !shallow);
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

                                            ProductInfo pi = new ProductInfo(p, !shallow);
                                            RelatedProducts.Add(pi);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (RelatedProducts.Count > 0)
            {
                Status |= StatusFlags.Products;
                if (RelatedProducts.TrueForAll(p => p.Status.HasFlag(ProductInfo.StatusFlags.Good)))
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
