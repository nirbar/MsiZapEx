using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;

namespace MsiZapEx
{
    public class ProductInfo
    {
        public enum StatusFlags
        {
            None = 0,
            HklmProduct = 1,
            HkcrProduct = HklmProduct * 2,
            HkcrFeatures = HkcrProduct * 2,
            HklmFeatures = HkcrFeatures * 2,
            ARP = HklmFeatures * 2,

            Components = ARP * 2,
            ComponentsGood = Components * 2,

            PatchesGood = ComponentsGood * 2,

            Good = PatchesGood | ComponentsGood | Components | ARP | HklmFeatures | HkcrFeatures | HkcrProduct | HklmProduct
        }

        public Guid ProductCode { get; private set; } = Guid.Empty;
        public string LocalPackage { get; private set; }
        public string DisplayName { get; private set; }
        public string DisplayVersion { get; private set; }
        public string InstallLocation { get; private set; }
        public List<ComponentInfo> Components { get; private set; }
        public List<PatchInfo> Patches { get; private set; }
        public RegistryView View { get; private set; }
        public StatusFlags Status { get; private set; } = StatusFlags.None;

        public ProductInfo(Guid productCode)
        {
            string obfuscatedGuid = GuidEx.MsiObfuscate(productCode);
            Read(obfuscatedGuid);
            if (Status == StatusFlags.None)
            {
                throw new FileNotFoundException();
            }
        }

        internal ProductInfo(string obfuscatedGuid)
        {
            Read(obfuscatedGuid);
        }

        private void Read(string obfuscatedGuid)
        {
            using (RegistryKey hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey k = hklm64.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{obfuscatedGuid}\InstallProperties", false))
                {
                    if (k != null)
                    {
                        Status |= StatusFlags.HklmProduct;

                        ProductCode = GuidEx.MsiObfuscate(obfuscatedGuid);
                        LocalPackage = k.GetValue("LocalPackage")?.ToString();
                        DisplayName = k.GetValue("DisplayName")?.ToString();
                        DisplayVersion = k.GetValue("DisplayVersion")?.ToString();
                        InstallLocation = k.GetValue("InstallLocation")?.ToString();

                        Components = ComponentInfo.GetComponents(ProductCode);
                        if (Components.Count > 0)
                        {
                            Status |= StatusFlags.Components;
                            if (Components.TrueForAll(c => c.Status == ComponentInfo.StatusFlags.Good))
                            {
                                Status |= StatusFlags.ComponentsGood;
                            }
                        }

                        Patches = PatchInfo.GetPatches(ProductCode);
                        if ((Patches.Count == 0) || Patches.TrueForAll(p => p.Status == PatchInfo.StatusFlags.Good))
                        {
                            Status |= StatusFlags.PatchesGood;
                        }
                    }
                }
                using (RegistryKey k = hklm64.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{obfuscatedGuid}\Features", false))
                {
                    if (k != null)
                    {
                        Status |= StatusFlags.HklmFeatures;
                    }
                }
                using (RegistryKey arp = hklm64.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProductCode.ToString("B")}", false))
                {
                    if (arp != null)
                    {
                        View = RegistryView.Registry64;
                        Status |= StatusFlags.ARP;
                    }
                }
                if (!Status.HasFlag(StatusFlags.ARP))
                {
                    using (RegistryKey hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    {
                        using (RegistryKey arp = hklm32.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{ProductCode.ToString("B")}", false))
                        {
                            if (arp != null)
                            {
                                View = RegistryView.Registry32;
                                Status |= StatusFlags.ARP;
                            }
                        }
                    }
                }
            }

            using (RegistryKey hkcr = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64))
            {
                using (RegistryKey k = hkcr.OpenSubKey($@"Installer\Products\{obfuscatedGuid}", false))
                {
                    if (k != null)
                    {
                        Status |= StatusFlags.HkcrFeatures;
                    }
                }
                using (RegistryKey k = hkcr.OpenSubKey($@"Installer\Features\{obfuscatedGuid}", false))
                {
                    if (k != null)
                    {
                        Status |= StatusFlags.HkcrProduct;
                    }
                }
            }
        }

        internal void Prune(RegistryModifier modifier)
        {
            foreach (ComponentInfo c in Components)
            {
                c.Prune(ProductCode, modifier);
            }
            foreach (PatchInfo p in Patches)
            {
                p.Prune(ProductCode, modifier);
            }

            string obfuscatedProductCode = GuidEx.MsiObfuscate(ProductCode);
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, RegistryView.Registry64, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{obfuscatedProductCode}");
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, View, $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProductCode.ToString("B")}");
            modifier.DeferDeleteKey(RegistryHive.ClassesRoot, RegistryView.Registry64, $@"Installer\Products\{obfuscatedProductCode}");
            modifier.DeferDeleteKey(RegistryHive.ClassesRoot, RegistryView.Registry64, $@"Installer\Features\{obfuscatedProductCode}");
        }
    }
}