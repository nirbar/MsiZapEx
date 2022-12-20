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
        public List<string> Dependants { get; private set; }
        public RegistryView View { get; private set; }
        public StatusFlags Status { get; private set; } = StatusFlags.None;

        public ProductInfo(Guid productCode)
        {
            string obfuscatedGuid = GuidEx.MsiObfuscate(productCode);
            Read(obfuscatedGuid);
        }

        public void PrintState()
        {
            if (Status == StatusFlags.None)
            {
                Console.WriteLine($"Product not found");
                return;
            }

            Console.WriteLine($"Product '{ProductCode}': '{DisplayName}' v{DisplayVersion} Contains {Components.Count} components");

            if (!Status.HasFlag(StatusFlags.HkcrProduct))
            {
                Console.WriteLine($@"{'\t'}Missing HKCR key under 'Installer\Products");
            }
            if (!Status.HasFlag(StatusFlags.HkcrFeatures))
            {
                Console.WriteLine($@"{'\t'}Missing HKCR key under 'Installer\Features");
            }
            if (!Status.HasFlag(StatusFlags.ARP))
            {
                Console.WriteLine($@"{'\t'}Missing Uninstall key under 'Software\Microsoft\Windows\CurrentVersion\Uninstall");
            }
            if (!Status.HasFlag(StatusFlags.HklmFeatures))
            {
                Console.WriteLine($@"{'\t'}Missing HKLM key under 'SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductCode SUID>\Features");
            }
            if (!Status.HasFlag(StatusFlags.HklmProduct))
            {
                Console.WriteLine($@"{'\t'}Missing HKLM key under 'SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\<ProductCode SUID>\InstallProperties");
            }

            if (!Status.HasFlag(StatusFlags.Components))
            {
                Console.WriteLine($"\tProduct has no components");
            }
            else if (!Status.HasFlag(StatusFlags.ComponentsGood))
            {
                foreach (ComponentInfo ci in Components)
                {
                    if (!ci.Status.Equals(ComponentInfo.StatusFlags.Good))
                    {
                        ci.PrintProductState(ProductCode);
                    }
                }
            }

            if (!Status.HasFlag(StatusFlags.PatchesGood))
            {
                foreach (PatchInfo pi in Patches)
                {
                    if (!pi.Status.Equals(PatchInfo.StatusFlags.Good))
                    {
                        pi.PrintState();
                    }
                }
            }
        }

        internal ProductInfo(string obfuscatedGuid)
        {
            Read(obfuscatedGuid);
        }

        private void Read(string obfuscatedGuid)
        {
            ProductCode = GuidEx.MsiObfuscate(obfuscatedGuid);
            using (RegistryKey hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey k = hklm64.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{obfuscatedGuid}\InstallProperties", false))
                {
                    if (k != null)
                    {
                        Status |= StatusFlags.HklmProduct;

                        LocalPackage = k.GetValue("LocalPackage")?.ToString();
                        DisplayName = k.GetValue("DisplayName")?.ToString();
                        DisplayVersion = k.GetValue("DisplayVersion")?.ToString();
                        InstallLocation = k.GetValue("InstallLocation")?.ToString();
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
                        Status |= StatusFlags.HkcrProduct;
                    }
                }
                using (RegistryKey k = hkcr.OpenSubKey($@"Installer\Features\{obfuscatedGuid}", false))
                {
                    if (k != null)
                    {
                        Status |= StatusFlags.HkcrFeatures;
                    }
                }

                Dependants = new List<string>();
                using (RegistryKey k = hkcr.OpenSubKey(@"Installer\Dependencies", false))
                {
                    if (k != null)
                    {
                        string[] subkeys = k.GetSubKeyNames();
                        if (subkeys != null)
                        {
                            foreach (string subkey in subkeys)
                            {
                                using (RegistryKey sk = k.OpenSubKey($@"{subkey}\Dependents\{ProductCode.ToString("B")}", false))
                                {
                                    if (sk != null)
                                    {
                                        Dependants.Add(subkey);
                                    }
                                }
                            }
                        }
                    }
                }
            }

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

            // Dependencies
            modifier.DeferDeleteKey(RegistryHive.ClassesRoot, RegistryView.Registry64, $@"Installer\Dependencies\{ProductCode.ToString("B")}");

            foreach (string d in Dependants)
            {
                modifier.DeferDeleteKey(RegistryHive.ClassesRoot, RegistryView.Registry64, $@"Installer\Dependencies\{d}\Dependents\{ProductCode.ToString("B")}");
            }

            //TODO Use FileSystemModifier
            if (!string.IsNullOrEmpty(LocalPackage))
            {
                try
                {
                    File.Delete(LocalPackage);
                }
                catch (Exception ex)
                {

                }
            }
        }
    }
}