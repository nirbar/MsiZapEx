using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MsiZapEx
{
    public class PatchInfo
    {
        public Guid PatchCode { get; private set; }
        public RegistryView View { get; private set; }
        public string DisplayName { get; private set; }
        public string LocalPackage { get; private set; }

        internal static List<PatchInfo> GetPatches(Guid productCode, RegistryView view)
        {
            List<PatchInfo> patches = new List<PatchInfo>();

            string obfuscatedProductCode = GuidEx.MsiObfuscate(productCode);

            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            {
                using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{obfuscatedProductCode}\Patches", false))
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
                            pi.View = view;
                            pi.PatchCode = GuidEx.MsiObfuscate(opc);

                            using (RegistryKey pk = k.OpenSubKey(opc, false))
                            {
                                pi.DisplayName = pk.GetValue("DisplayName")?.ToString();
                            }
                            using (RegistryKey pk = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Patches\{opc}", false))
                            {
                                pi.LocalPackage = pk.GetValue("LocalPackage")?.ToString();
                            }
                            patches.Add(pi);
                        }
                    }
                }
            }
            return patches;
        }

        internal void Prune(Guid productCode, RegistryModifier modifier)
        {
            string obfuscatedPatchCode = GuidEx.MsiObfuscate(PatchCode);
            string obfuscatedProductCode = GuidEx.MsiObfuscate(productCode);

            modifier.DeferDeleteKey(RegistryHive.LocalMachine, View, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Patches\{obfuscatedPatchCode}");
            modifier.DeferDeleteKey(RegistryHive.ClassesRoot, View, $@"Installer\Patches\{obfuscatedPatchCode}");

            string subKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{obfuscatedProductCode}\Patches";
            modifier.DeferDeleteValue(RegistryHive.LocalMachine, View, subKey, obfuscatedPatchCode);

            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, View))
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
                                modifier.DeferSetValue(RegistryHive.LocalMachine, View, subKey, "Patches", RegistryValueKind.MultiString, reduced);
                            }
                            else
                            {
                                modifier.DeferDeleteValue(RegistryHive.LocalMachine, View, subKey, "Patches");
                            }
                        }
                    }
                }
            }
        }
    }
}