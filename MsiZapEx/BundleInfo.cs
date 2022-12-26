using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MsiZapEx
{

    public class BundleInfo
    {
        // HKLM32 Software\Microsoft\Windows\CurrentVersion\Uninstall\<BundleProductCode>
        // HKCR HKEY_CLASSES_ROOT\Installer\Dependencies\<MsiProductCode or BundleBundleProviderKey>\@ = <MsiProductCode Or BundleProductCode>
        // HKCR HKEY_CLASSES_ROOT\Installer\Dependencies\*\Dependents\<BundleProductCode>
        [Flags]
        public enum StatusFlags
        {
            None = 0,
            ARP = 1,
            ArpUpgradeCodes = ARP * 2,
            ArpPorviderKey = ArpUpgradeCodes * 2,
            HkcrDependencies = ArpPorviderKey * 2,
            ProviderKeyProductCodeMatch = HkcrDependencies * 2,

            Good = ProviderKeyProductCodeMatch | HkcrDependencies | ArpPorviderKey | ArpUpgradeCodes | ARP
        }

        public Guid BundleProductCode { get; private set; } = Guid.Empty;
        public List<Guid> BundleUpgradeCodes { get; private set; } = new List<Guid>();
        public string BundleProviderKey { get; private set; }
        public string BundleCachePath { get; private set; }
        public string DisplayName { get; private set; }
        public string DisplayVersion { get; private set; }
        public List<string> Dependencies { get; private set; } = new List<string>();
        public List<string> Dependents { get; private set; } = new List<string>();
        public StatusFlags Status { get; private set; } = StatusFlags.None;

        public static List<BundleInfo> FindByUpgradeCode(Guid bundleUpgradeCode)
        {
            List<BundleInfo> bundles = new List<BundleInfo>();

            using (RegistryKey hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (RegistryKey hkUninstall = hklm32.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false))
                {
                    if (hkUninstall == null)
                    {
                        throw new FileNotFoundException();
                    }

                    string[] subKeyNames = hkUninstall.GetSubKeyNames();
                    if ((subKeyNames == null) || (subKeyNames.Length == 0))
                    {
                        return bundles;
                    }

                    foreach (string sk in subKeyNames)
                    {
                        if (!Guid.TryParse(sk, out Guid bundleProductCode))
                        {
                            continue;
                        }

                        using (RegistryKey hkSubkey = hkUninstall.OpenSubKey(sk, false))
                        {
                            string[] valNames = hkSubkey.GetValueNames();
                            if ((valNames == null) || !valNames.Contains("BundleUpgradeCode"))
                            {
                                continue;
                            }

                            RegistryValueKind valueKind = hkSubkey.GetValueKind("BundleUpgradeCode");
                            switch (valueKind)
                            {
                                case RegistryValueKind.String:
                                    if (Guid.TryParse(hkSubkey.GetValue("BundleUpgradeCode")?.ToString(), out Guid bug) && bug.Equals(bundleUpgradeCode))
                                    {
                                        BundleInfo bundle = new BundleInfo(bundleProductCode);
                                        bundles.Add(bundle);
                                    }
                                    continue;

                                case RegistryValueKind.MultiString:
                                    if ((hkSubkey.GetValue("BundleUpgradeCode") is IEnumerable<string> bugs) && bugs.Any(uc => Guid.TryParse(uc, out Guid bug1) && bug1.Equals(bundleUpgradeCode)))
                                    {
                                        BundleInfo bundle = new BundleInfo(bundleProductCode);
                                        bundles.Add(bundle);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            return bundles;
        }

        public void Prune()
        {
            using (RegistryModifier modifier = new RegistryModifier())
            {
                if (!BundleProductCode.Equals(Guid.Empty))
                {
                    modifier.DeferDeleteKey(RegistryHive.LocalMachine, RegistryView.Registry32, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{BundleProductCode.ToString("B")}");

                    foreach (string d in Dependents)
                    {
                        modifier.DeferDeleteKey(RegistryHive.ClassesRoot, RegistryView.Registry64, $@"Installer\Dependencies\{d}\Dependents\{BundleProductCode.ToString("B")}");
                    }
                }
                if (!string.IsNullOrEmpty(BundleProviderKey))
                {
                    modifier.DeferDeleteKey(RegistryHive.ClassesRoot, RegistryView.Registry64, $@"Installer\Dependencies\{BundleProviderKey}");
                }

                // Remove bundle from PendingFileRenameOperations
                if (!string.IsNullOrEmpty(BundleCachePath))
                {
                    //TODO Use FileSystemModifier
                    try
                    {
                        File.Delete(BundleCachePath);
                    }
                    catch (Exception ex)
                    {

                    }
                    modifier.DeferRemoveFromPendingOperations(BundleCachePath);
                }
            }
        }

        internal void PrintState()
        {
            if (Status == StatusFlags.None)
            {
                Console.WriteLine($"BundleUpgradeCode not found");
                return;
            }

            Console.WriteLine($"Bundle '{DisplayName}' v{DisplayVersion}");
            if (!Status.HasFlag(StatusFlags.ARP))
            {
                Console.WriteLine($"\tMissing HKLM Uninstall key");
            }
            if (!BundleProductCode.Equals(Guid.Empty))
            {
                Console.WriteLine($"\tBundleProductCode '{BundleProductCode}'");
            }

            if (!Status.HasFlag(StatusFlags.ArpUpgradeCodes))
            {
                Console.WriteLine($"\tMissing 'BundleUpgradeCode'");
            }
            foreach (Guid buc in BundleUpgradeCodes)
            {
                Console.WriteLine($"\tBundleUpgradeCode '{buc}'");
            }

            if (!Status.HasFlag(StatusFlags.ArpPorviderKey))
            {
                Console.WriteLine($"\tMissing 'BundlePorviderKey'");
            }
            else
            {
                Console.WriteLine($"\tBundlePorviderKey '{BundleProviderKey}'");
            }

            if (!Status.HasFlag(StatusFlags.HkcrDependencies))
            {
                Console.WriteLine($@"{'\t'}Missing HKCR Installer\Dependencies key");
            }
            if (!Status.HasFlag(StatusFlags.ProviderKeyProductCodeMatch))
            {
                Console.WriteLine($"\tHKLM 'BundlePorviderKey' does not match a HKCR entry");
            }

            if (Dependencies.Count == 0)
            {
                Console.WriteLine($"\tNo dependencies detected");
            }
            foreach (string d in Dependencies)
            {
                Console.WriteLine($"\tDependency: '{d}'");
            }

            if (Dependents.Count == 0)
            {
                Console.WriteLine($"\tNo dependents detected");
            }
            foreach (string d in Dependents)
            {
                Console.WriteLine($"\tDependent: '{d}'");
            }
        }

        public BundleInfo(Guid bundleProductCode)
        {
            ReadARP(bundleProductCode);
        }

        private void ReadDependencies(string bundleProviderKey)
        {
            BundleProviderKey = bundleProviderKey;
            using (RegistryKey hkcr = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64))
            {
                using (RegistryKey hkDependencies = hkcr.OpenSubKey($@"Installer\Dependencies", false))
                {
                    if (hkDependencies == null)
                    {
                        return;
                    }

                    using (RegistryKey hkProviderKey = hkDependencies.OpenSubKey(bundleProviderKey))
                    {
                        if (hkProviderKey != null)
                        {
                            Status |= StatusFlags.HkcrDependencies;

                            if (Guid.TryParse(hkProviderKey.GetValue("")?.ToString(), out Guid bpc))
                            {
                                if (BundleProductCode.Equals(Guid.Empty))
                                {
                                    ReadARP(bpc);
                                }
                                if (BundleProductCode.Equals(bpc))
                                {
                                    Status |= StatusFlags.ProviderKeyProductCodeMatch;
                                }
                            }

                            using (RegistryKey hkDependents = hkProviderKey.OpenSubKey("Dependents", false))
                            {
                                if (hkDependents != null)
                                {
                                    foreach (string sk in hkDependents.GetSubKeyNames())
                                    {
                                        if (!string.IsNullOrEmpty(sk) && Guid.TryParse(sk, out Guid dg) && !dg.Equals(BundleProductCode))
                                        {
                                            Dependencies.Add(sk);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    foreach (string sk in hkDependencies.GetSubKeyNames())
                    {
                        if (!string.IsNullOrEmpty(sk) && !sk.Equals(BundleProviderKey))
                        {
                            using (RegistryKey hkDependents = hkDependencies.OpenSubKey($@"{sk}\Dependents\{BundleProductCode.ToString("B")}"))
                            {
                                if (hkDependents != null)
                                {
                                    Dependents.Add(sk);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private void ReadARP(Guid bundleProductCode)
        {
            BundleProductCode = bundleProductCode;
            using (RegistryKey hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (RegistryKey hkUninstall = hklm32.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{bundleProductCode.ToString("B")}", false))
                {
                    if (hkUninstall == null)
                    {
                        return;
                    }
                    Status |= StatusFlags.ARP;

                    RegistryValueKind valueKind = hkUninstall.GetValueKind("BundleUpgradeCode");
                    switch (valueKind)
                    {
                        case RegistryValueKind.String:
                            if (Guid.TryParse(hkUninstall.GetValue("BundleUpgradeCode")?.ToString(), out Guid buc))
                            {
                                BundleUpgradeCodes.Add(buc);
                            }
                            break;

                        case RegistryValueKind.MultiString:
                            if (hkUninstall.GetValue("BundleUpgradeCode") is IEnumerable<string> bucs)
                            {
                                foreach (string s in bucs)
                                {
                                    if (Guid.TryParse(s, out Guid buc1))
                                    {
                                        BundleUpgradeCodes.Add(buc1);
                                    }
                                }
                            }
                            break;
                    }
                    if (BundleUpgradeCodes.Count > 0)
                    {
                        Status |= StatusFlags.ArpUpgradeCodes;
                    }

                    DisplayName = hkUninstall.GetValue("DisplayName")?.ToString();
                    DisplayVersion = hkUninstall.GetValue("DisplayVersion")?.ToString();
                    BundleCachePath = hkUninstall.GetValue("BundleCachePath")?.ToString();

                    string bpk = hkUninstall.GetValue("BundleProviderKey")?.ToString();
                    if (!string.IsNullOrEmpty(bpk))
                    {
                        Status |= StatusFlags.ArpPorviderKey;
                        if (string.IsNullOrEmpty(BundleProviderKey))
                        {
                            ReadDependencies(bpk);
                        }
                    }
                }
            }
        }
    }
}
