using CommandLine;
using System;
using System.Collections.Generic;
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
        // HKEY_CLASSES_ROOT\Installer\Features\<ProductSQUID>\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductSQUID>\ 
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductSQUID>\Features\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductSQUID>\InstallProperties\
        // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components\<ComponentSQUID>\

        // Patch registry keys
        // HKEY_CLASSES_ROOT\Installer\Products\<ProductSQUID>\Patches
        // HKEY_CLASSES_ROOT\Installer\Patches\<PatchSQUID>\
        // HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Patches\<PatchSQUID>\

        // Note: SQUID means obfuscated GUID

        static void Main(string[] args)
        {
            ParserResult<Settings> cmdLine = Parser.Default.ParseArguments<Settings>(args);
            if (cmdLine.Errors.Count() > 0)
            {
                foreach (Error e in cmdLine.Errors)
                {
                    Console.WriteLine(e.ToString());
                }
                Environment.Exit(1);
            }

            try
            {
                Settings.Instance = cmdLine.Value;
                if (!string.IsNullOrEmpty(Settings.Instance.BundleUpgradeCode))
                {
                    List<BundleInfo> bundles = BundleInfo.FindByUpgradeCode(new Guid(Settings.Instance.BundleUpgradeCode));
                    if (bundles.Count == 0)
                    {
                        Console.WriteLine($"No BundleUpgradeCode '{Settings.Instance.BundleUpgradeCode}' was found");
                    }
                    foreach (BundleInfo bi in bundles)
                    {
                        bi.PrintState();
                    }
                    if ((Settings.Instance.ForceClean && (bundles.Count == 1)) || Settings.Instance.ForceCleanAllRelated)
                    {
                        foreach (BundleInfo bi in bundles)
                        {
                            bi.Prune();
                        }
                    }
                }
                if (!string.IsNullOrEmpty(Settings.Instance.BundleProductCode))
                {
                    if (!Guid.TryParse(Settings.Instance.BundleProductCode, out Guid productCode))
                    {
                        Console.WriteLine($"BundleProductCode '{Settings.Instance.BundleUpgradeCode}' is not a UUID");
                        Environment.Exit(1);
                    }

                    BundleInfo bundle = new BundleInfo(productCode);
                    bundle.PrintState();
                    if (Settings.Instance.ForceClean)
                    {
                        bundle.Prune();
                    }
                }
                if (!string.IsNullOrEmpty(Settings.Instance.UpgradeCode))
                {
                    Guid upgradeCode = Settings.Instance.Obfuscated ? GuidEx.MsiObfuscate(Settings.Instance.UpgradeCode) : new Guid(Settings.Instance.UpgradeCode);

                    UpgradeInfo upgrade = new UpgradeInfo(upgradeCode);
                    if (upgrade != null)
                    {
                        upgrade.PrintState();
                        if ((Settings.Instance.ForceClean && (upgrade.RelatedProducts.Count == 1)) || Settings.Instance.ForceCleanAllRelated)
                        {
                            foreach (ProductInfo pi in upgrade.RelatedProducts)
                            {
                                upgrade.Prune(pi);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No UpgradeCode '{upgradeCode}' was found");
                    }
                }
                if (!string.IsNullOrEmpty(Settings.Instance.ProductCode))
                {
                    Guid productCode = Settings.Instance.Obfuscated ? GuidEx.MsiObfuscate(Settings.Instance.ProductCode) : new Guid(Settings.Instance.ProductCode);

                    UpgradeInfo upgrade = UpgradeInfo.FindByProductCode(productCode);
                    if (upgrade != null)
                    {
                        ProductInfo product = upgrade.RelatedProducts.First(p => p.ProductCode.Equals(productCode));
                        product.PrintState();
                        if (Settings.Instance.ForceClean && (product != null))
                        {
                            upgrade.Prune(product);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Product '{productCode}' is not related to any UpgradeCode");
                        ProductInfo product = new ProductInfo(productCode);
                        if (product != null)
                        {
                            product.PrintState();
                            if (Settings.Instance.ForceClean)
                            {
                                using (RegistryModifier modifier = new RegistryModifier())
                                {
                                    product.Prune(modifier);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"No ProductCode '{productCode}' was found");
                        }
                    }
                }
                if (!string.IsNullOrEmpty(Settings.Instance.ComponentCode))
                {
                    Guid componentCode = Settings.Instance.Obfuscated ? GuidEx.MsiObfuscate(Settings.Instance.ComponentCode) : new Guid(Settings.Instance.ComponentCode);

                    ComponentInfo component = new ComponentInfo(componentCode);
                    component.PrintProducts();
                }
                if (Settings.Instance.DetectOrphanProducts)
                {
                    List<ProductInfo> orphan = ProductInfo.GetOrphanProducts();
                    Console.WriteLine($"{orphan.Count} orphan product(s) detected");
                    foreach (ProductInfo pi in orphan)
                    {
                        pi.PrintState();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}