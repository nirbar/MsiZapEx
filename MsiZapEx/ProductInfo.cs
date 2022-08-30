using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;

namespace MsiZapEx
{
    public class ProductInfo
    {
        public Guid ProductCode { get; private set; } = Guid.Empty;
        public string LocalPackage { get; private set; }
        public string DisplayName { get; private set; }
        public string DisplayVersion { get; private set; }
        public string InstallLocation { get; private set; }
        public List<ComponentInfo> Components { get; private set; }
        public List<PatchInfo> Patches { get; private set; }
        public RegistryView View { get; private set; }

        public ProductInfo(Guid productCode)
        {
            string obfuscatedGuid = GuidEx.MsiObfuscate(productCode);
            Read(obfuscatedGuid, RegistryView.Registry64);
            if (ProductCode == Guid.Empty)
            {
                Read(obfuscatedGuid, RegistryView.Registry32);
                if (ProductCode == Guid.Empty)
                {
                    throw new FileNotFoundException();
                }
            }
        }

        internal ProductInfo(string obfuscatedGuid, RegistryView view)
        {
            Read(obfuscatedGuid, view);
        }

        private void Read(string obfuscatedGuid, RegistryView view)
        {
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            {
                using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{obfuscatedGuid}\InstallProperties", false))
                {
                    if (k != null)
                    {
                        View = view;
                        ProductCode = GuidEx.MsiObfuscate(obfuscatedGuid);
                        LocalPackage = k.GetValue("LocalPackage")?.ToString();
                        DisplayName = k.GetValue("DisplayName")?.ToString();
                        DisplayVersion = k.GetValue("DisplayVersion")?.ToString();
                        InstallLocation = k.GetValue("InstallLocation")?.ToString();
                        Components = ComponentInfo.GetComponents(ProductCode, view);
                        Patches = PatchInfo.GetPatches(ProductCode, view);
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
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, View, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{obfuscatedProductCode}");
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, View, $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProductCode.ToString("B")}");
            modifier.DeferDeleteKey(RegistryHive.ClassesRoot, View, $@"Installer\Products\{obfuscatedProductCode}");
            modifier.DeferDeleteKey(RegistryHive.ClassesRoot, View, $@"Installer\Features\{obfuscatedProductCode}");
        }
    }
}