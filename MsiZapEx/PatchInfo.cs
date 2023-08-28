using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MsiZapEx
{
    public class PatchInfo
    {
        [Flags]
        public enum StatusFlags
        {
            None = 0,
            HklmProduct = 1,
            HklmPatch = 2 * HklmProduct,
            HkcrPatch = 2 * HklmPatch,

            Good = HkcrPatch | HklmPatch | HklmProduct
        }

        public Guid PatchCode { get; private set; }
        public string DisplayName { get; private set; }
        public bool MachineScope { get; private set; }
        public string UserSID => MachineScope ? ProductInfo.LocalSystemSID : ProductInfo.CurrentUserSID;
        public string LocalPackage { get; private set; }
        public StatusFlags Status { get; private set; } = StatusFlags.None;

        internal static List<PatchInfo> GetPatches(Guid productCode, bool? machineScope = null)
        {
            List<PatchInfo> patches = new List<PatchInfo>();

            string obfuscatedProductCode = GuidEx.MsiObfuscate(productCode);
            if (machineScope == null)
            {
                machineScope = ProductInfo.ResolveScope(productCode);
            }
            string userSID = (machineScope == true) ? ProductInfo.LocalSystemSID : ProductInfo.CurrentUserSID;

            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey hkmu = RegistryKey.OpenBaseKey((machineScope == true) ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64))
                {
                    using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{userSID}\Products\{obfuscatedProductCode}\Patches", false))
                    {
                        if (k == null)
                        {
                            return patches;
                        }

                        string[] obfuscatedPatchCodes = k.GetSubKeyNames();
                        foreach (string opc in obfuscatedPatchCodes)
                        {
                            if (Guid.TryParse(opc, out Guid guid))
                            {
                                PatchInfo pi = new PatchInfo();
                                pi.PatchCode = GuidEx.MsiObfuscate(opc);
                                pi.MachineScope = (machineScope == true);

                                using (RegistryKey pk = k.OpenSubKey(opc, false))
                                {
                                    if (pk != null)
                                    {
                                        pi.Status |= StatusFlags.HklmProduct;
                                        pi.DisplayName = pk.GetValue("DisplayName")?.ToString();
                                    }
                                }
                                using (RegistryKey pk = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{pi.UserSID}\Patches\{opc}", false))
                                {
                                    if (pk != null)
                                    {
                                        pi.Status |= StatusFlags.HklmPatch;
                                        pi.LocalPackage = pk.GetValue("LocalPackage")?.ToString();
                                    }
                                }

                                string keyBase = (machineScope == true) ? @"SOFTWARE\Classes" : @"Software\Microsoft";
                                using (RegistryKey pk = hkmu.OpenSubKey($@"{keyBase}\Installer\Patches\{opc}", false))
                                {
                                    if (pk != null)
                                    {
                                        pi.Status |= StatusFlags.HkcrPatch;
                                        pi.LocalPackage = pk.GetValue("LocalPackage")?.ToString();
                                    }
                                }
                                patches.Add(pi);
                            }
                        }
                    }
                }
            }
            return patches;
        }

        internal void PrintState()
        {
            Console.WriteLine($"Patch '{PatchCode}'");

            if (!Status.HasFlag(StatusFlags.HkcrPatch))
            {
                Console.WriteLine($@"{'\t'}Missing HKCR key under 'Installer\Patches");
            }
            if (!Status.HasFlag(StatusFlags.HklmPatch))
            {
                Console.WriteLine($@"{'\t'}Missing HKLM key under 'SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Patches");
            }
            if (!Status.HasFlag(StatusFlags.HklmProduct))
            {
                Console.WriteLine($@"{'\t'}Missing HKLM key under 'SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Products\<ProductCode SUID>\Patches");
            }
        }

        internal void Prune(Guid productCode, RegistryModifier modifier)
        {
            string obfuscatedPatchCode = GuidEx.MsiObfuscate(PatchCode);
            string obfuscatedProductCode = GuidEx.MsiObfuscate(productCode);

            modifier.DeferDeleteKey(RegistryHive.LocalMachine, RegistryView.Registry64, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Patches\{obfuscatedPatchCode}");

            string keyBase = MachineScope ? @"SOFTWARE\Classes" : @"Software\Microsoft";
            RegistryHive hiveBase = MachineScope ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            modifier.DeferDeleteKey(hiveBase, RegistryView.Registry64, $@"{keyBase}\Installer\Patches\{obfuscatedPatchCode}");

            string subKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Products\{obfuscatedProductCode}\Patches";
            modifier.DeferDeleteValue(RegistryHive.LocalMachine, RegistryView.Registry64, subKey, obfuscatedPatchCode);

            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey k = hklm.OpenSubKey(subKey, false))
                {
                    if (k != null)
                    {
                        IEnumerable<string> patches = k.GetValue("Patches") as IEnumerable<string>;
                        if ((patches != null) && patches.Contains(obfuscatedPatchCode))
                        {
                            List<string> reduced = new List<string>(patches);
                            reduced.Remove(obfuscatedPatchCode);
                            if (reduced.Count > 0)
                            {
                                modifier.DeferSetValue(RegistryHive.LocalMachine, RegistryView.Registry64, subKey, "Patches", RegistryValueKind.MultiString, reduced);
                            }
                            else
                            {
                                modifier.DeferDeleteValue(RegistryHive.LocalMachine, RegistryView.Registry64, subKey, "Patches");
                            }
                        }
                    }
                }
            }
        }
    }
}
