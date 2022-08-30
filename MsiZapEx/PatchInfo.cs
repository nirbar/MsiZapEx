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

        internal void Prune(Guid productCode)
        {
            string obfuscatedPatchCode = GuidEx.MsiObfuscate(PatchCode);
            string obfuscatedProductCode = GuidEx.MsiObfuscate(productCode);
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, View))
            {
                using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{obfuscatedProductCode}\Patches", true))
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
                                k.SetValue("Patches", reduced, RegistryValueKind.MultiString);
                            }
                            else
                            {
                                k.DeleteValue("Patches");
                            }
                        }

                        k.DeleteValue(obfuscatedPatchCode);
                    }
                }
                using (RegistryKey k = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Patches", true))
                {
                    if (k != null)
                    {
                        k.DeleteSubKeyTree(obfuscatedPatchCode);
                    }
                }
            }

            using (RegistryKey hkcr = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, View))
            {
                using (RegistryKey k = hkcr.OpenSubKey(@"Installer\Patches", true))
                {
                    k.DeleteSubKeyTree(obfuscatedPatchCode, false);
                }
            }
        }
    }
}
