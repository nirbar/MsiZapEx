using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using static MsiZapEx.ComponentInfo;
using System.Linq;
using System.Security.Principal;

namespace MsiZapEx
{
    public class ProductInfo
    {
        [Flags]
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

            /// <summary>
            /// Products that have enough data to be detected and uninstalled via "Add/Remove Programs" applet
            /// </summary>
            MinGood = ARP | HklmProduct,

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
        public List<string> Features { get; private set; }
        public RegistryView View { get; private set; }
        public StatusFlags Status { get; private set; } = StatusFlags.None;
        public bool IsShallow { get; private set; }

        public bool MachineScope { get; private set; }
        public bool UserScope => !MachineScope;
        public string UserSID => MachineScope ? LocalSystemSID : CurrentUserSID;

        internal readonly static string LocalSystemSID = "S-1-5-18";
        private static string _currentUserSID = null;
        internal static string CurrentUserSID
        {
            get
            {
                if (!string.IsNullOrEmpty(_currentUserSID))
                {
                    return _currentUserSID;
                }
                _currentUserSID = WindowsIdentity.GetCurrent(false)?.User?.ToString();
                if (string.IsNullOrEmpty(_currentUserSID))
                {
                    throw new Exception("Failed to get SID of current user");
                }
                return _currentUserSID;
            }
        }

        public ProductInfo(Guid productCode, bool includeComponents = true, bool? machineScope = null)
        {
            string obfuscatedGuid = GuidEx.MsiObfuscate(productCode);
            IsShallow = !includeComponents;
            Read(obfuscatedGuid, machineScope);
        }

        public void PrintState()
        {
            if (Status == StatusFlags.None)
            {
                Console.WriteLine($"Product not found");
                return;
            }

            Console.WriteLine($"Product '{ProductCode}': '{DisplayName}' v{DisplayVersion}, Contains {Components.Count} components" + (MachineScope ? " in machine scope" : " in user scope"));

            if (!Status.HasFlag(StatusFlags.HkcrProduct))
            {
                Console.WriteLine($@"{'\t'}Missing HKCR key under 'Installer\Products'");
            }
            if (!Status.HasFlag(StatusFlags.HkcrFeatures))
            {
                Console.WriteLine($@"{'\t'}Missing HKCR key under 'Installer\Features'");
            }
            if (!Status.HasFlag(StatusFlags.ARP))
            {
                Console.WriteLine($@"{'\t'}Missing Uninstall key under 'Software\Microsoft\Windows\CurrentVersion\Uninstall'");
            }
            if (!Status.HasFlag(StatusFlags.HklmFeatures))
            {
                Console.WriteLine($@"{'\t'}Missing HKLM key under 'SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Products\<ProductCode SUID>\Features'");
            }
            if (!Status.HasFlag(StatusFlags.HklmProduct))
            {
                Console.WriteLine($@"{'\t'}Missing HKLM key under 'SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Products\<ProductCode SUID>\InstallProperties'");
            }

            if (IsShallow)
            {
                Console.WriteLine("\tComponentes were not checked");
            }
            else
            {
                if (!Status.HasFlag(StatusFlags.Components))
                {
                    Console.WriteLine($"\tProduct has no components");
                }
                else if (!Status.HasFlag(StatusFlags.ComponentsGood))
                {
                    foreach (ComponentInfo ci in Components)
                    {
                        if (!ci.Status.HasFlag(ComponentInfo.StatusFlags.Good))
                        {
                            ci.PrintProductState(ProductCode);
                        }
                    }
                }
            }

            if (!Status.HasFlag(StatusFlags.PatchesGood))
            {
                foreach (PatchInfo pi in Patches)
                {
                    if (!pi.Status.HasFlag(PatchInfo.StatusFlags.Good))
                    {
                        pi.PrintState();
                    }
                }
            }

            if (Settings.Instance.Verbose)
            {
                Console.WriteLine($"\tFeatures: {Features.Aggregate((a, c) => $"{a}, {c}")}");
                Console.WriteLine($"\tLocalPackage: {LocalPackage}");
            }
        }

        internal ProductInfo(string obfuscatedGuid, bool includeComponents = true, bool? machineScope = null)
        {
            IsShallow = !includeComponents;
            Read(obfuscatedGuid, machineScope);
        }

        /// <summary>
        /// Return all products that fails <see cref="ProductInfo.StatusFlags.MinGood"/> check.
        /// Products are enumerated from Components' registry key. Thus a product will not be returned if it has no components at all
        /// </summary>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException">Components root registry key is missing</exception>
        public static List<ProductInfo> GetOrphanProducts()
        {
            List<ProductInfo> products = new List<ProductInfo>();
            List<ComponentInfo> allComponents = ComponentInfo.GetAllComponents();

            foreach (ComponentInfo ci in allComponents)
            {
                // Ignore default value
                if (ci.ComponentCode.Equals(Guid.Empty))
                {
                    continue;
                }

                foreach (ProductKeyPath keyPath in ci.ProductsKeyPath)
                {
                    if (keyPath.ProductCode.Equals(Guid.Empty))
                    {
                        continue;
                    }

                    ProductInfo pi = products.FirstOrDefault(p => p.ProductCode.Equals(keyPath.ProductCode));
                    if (pi == null)
                    {
                        pi = new ProductInfo(keyPath.ProductCode, ci.MachineScope);
                        products.Add(pi);
                    }
                }
            }
            products.RemoveAll(p => p.Status.HasFlag(StatusFlags.MinGood));
            return products;
        }

        internal static bool ResolveScope(Guid productCode)
        {
            string obfuscatedGuid = GuidEx.MsiObfuscate(productCode);
            using (RegistryKey hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey k = hklm64.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{LocalSystemSID}\Products\{obfuscatedGuid}\InstallProperties", false))
                {
                    if (k != null)
                    {
                        // Machine scope exists for this product
                        return true;
                    }
                }
            }
            return false;
        }

        private void Read(string obfuscatedGuid, bool? machineScope)
        {
            ProductCode = GuidEx.MsiObfuscate(obfuscatedGuid);
            MachineScope = machineScope ?? ResolveScope(ProductCode);

            using (RegistryKey hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey k = hklm64.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Products\{obfuscatedGuid}\InstallProperties", false))
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
                using (RegistryKey k = hklm64.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Products\{obfuscatedGuid}\Features", false))
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

            string keyBase = MachineScope ? @"SOFTWARE\Classes" : @"Software\Microsoft";
            using (RegistryKey hkmu = RegistryKey.OpenBaseKey(MachineScope ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                using (RegistryKey k = hkmu.OpenSubKey($@"{keyBase}\Installer\Products\{obfuscatedGuid}", false))
                {
                    if (k != null)
                    {
                        Status |= StatusFlags.HkcrProduct;
                    }
                }

                Features = new List<string>();
                using (RegistryKey k = hkmu.OpenSubKey($@"{keyBase}\Installer\Features\{obfuscatedGuid}", false))
                {
                    if (k != null)
                    {
                        Status |= StatusFlags.HkcrFeatures;

                        string[] features = k.GetValueNames();
                        if (features != null)
                        {
                            foreach (string f in features)
                            {
                                if (!string.IsNullOrEmpty(f) && !f.Equals("@"))
                                {
                                    Features.Add(f);
                                }
                            }
                        }
                    }
                }

                Dependants = new List<string>();
                using (RegistryKey k = hkmu.OpenSubKey(@"Software\Classes\Installer\Dependencies", false))
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

            if (IsShallow)
            {
                Components = new List<ComponentInfo>();
            }
            else
            {
                Components = ComponentInfo.GetComponents(ProductCode);
                if (Components.Count > 0)
                {
                    Status |= StatusFlags.Components;
                    if (Components.TrueForAll(c => c.Status.HasFlag(ComponentInfo.StatusFlags.Good)))
                    {
                        Status |= StatusFlags.ComponentsGood;
                    }
                }
            }

            Patches = PatchInfo.GetPatches(ProductCode);
            if ((Patches.Count == 0) || Patches.TrueForAll(p => p.Status.HasFlag(PatchInfo.StatusFlags.Good)))
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
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, RegistryView.Registry64, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Products\{obfuscatedProductCode}");
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, View, $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProductCode.ToString("B")}");

            string keyBase = MachineScope ? @"SOFTWARE\Classes" : @"Software\Microsoft";
            RegistryHive hiveBase = MachineScope ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            modifier.DeferDeleteKey(hiveBase, RegistryView.Registry64, $@"{keyBase}\Installer\Products\{obfuscatedProductCode}");
            modifier.DeferDeleteKey(hiveBase, RegistryView.Registry64, $@"{keyBase}\Installer\Features\{obfuscatedProductCode}");

            // Dependencies
            modifier.DeferDeleteKey(hiveBase, RegistryView.Registry64, $@"SOFTWARE\Classes\Installer\Dependencies\{ProductCode.ToString("B")}");

            foreach (string d in Dependants)
            {
                modifier.DeferDeleteKey(hiveBase, RegistryView.Registry64, $@"SOFTWARE\Classes\Installer\Dependencies\{d}\Dependents\{ProductCode.ToString("B")}");
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
