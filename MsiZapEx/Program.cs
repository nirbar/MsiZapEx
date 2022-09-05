﻿using CommandLine;
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
                if (!string.IsNullOrEmpty(Settings.Instance.UpgradeCode))
                {
                    Guid upgradeCode = Settings.Instance.Obfuscated ? GuidEx.MsiObfuscate(Settings.Instance.UpgradeCode) : new Guid(Settings.Instance.UpgradeCode);

                    UpgradeInfo upgrade = new UpgradeInfo(upgradeCode);
                    if (upgrade != null)
                    {
                        upgrade.PrintState();
                        if (Settings.Instance.ForceClean && (upgrade.RelatedProducts.Count == 1))
                        {
                            upgrade.Prune(upgrade.RelatedProducts[0]);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No UpgradeCode '{upgradeCode}' was found");
                    }
                }
                else if (!string.IsNullOrEmpty(Settings.Instance.ProductCode))
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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}