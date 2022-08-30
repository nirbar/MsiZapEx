using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace MsiZapEx
{
    public class ComponentInfo
    {
        public Guid ComponentCode { get; private set; }
        public RegistryView View { get; private set; }

        Dictionary<Guid, string> productToKeyPath = new Dictionary<Guid, string>();

        internal static List<ComponentInfo> GetComponents(Guid productCode, RegistryView view)
        {
            List<ComponentInfo> components = new List<ComponentInfo>();

            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            {
                using (RegistryKey k = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components", false))
                {
                    if (k == null)
                    {
                        Console.WriteLine(@"Registry key doesn't exist: SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components");
                        return components;
                    }

                    string[] componentCodes = k.GetSubKeyNames();
                    foreach (string c in componentCodes)
                    {
                        // Ignore default value
                        if (string.IsNullOrWhiteSpace(c) || c.Equals("@"))
                        {
                            continue;
                        }

                        ComponentInfo ci = new ComponentInfo(c, view);
                        if (ci.productToKeyPath.ContainsKey(productCode))
                        {
                            components.Add(ci);
                        }
                    }
                }
            }
            return components;
        }

        internal ComponentInfo(string obfuscatedGuid, RegistryView view)
        {
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            {
                using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components\{obfuscatedGuid}", false))
                {
                    if (k != null)
                    {
                        ComponentCode = GuidEx.MsiObfuscate(obfuscatedGuid);

                        foreach (string n in k.GetValueNames())
                        {
                            if (string.IsNullOrWhiteSpace(n) || n.Equals("@"))
                            {
                                continue;
                            }

                            if (Guid.TryParse(n, out Guid id))
                            {
                                View = view;
                                productToKeyPath[GuidEx.MsiObfuscate(n)] = k.GetValue(n)?.ToString();
                            }
                        }
                    }
                }
            }
        }

        internal void Prune(Guid productCode)
        {
            string obfuscatedComponentCode = GuidEx.MsiObfuscate(ComponentCode);
            string obfuscatedProductCode = GuidEx.MsiObfuscate(productCode);
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, View))
            {
                string subkeyName = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components\{obfuscatedComponentCode}";
                bool deleteSybkey = false;
                using (RegistryKey k = hklm.OpenSubKey(subkeyName, true))
                {
                    if (k != null)
                    {

                        int prodNum = 0;
                        foreach (string n in k.GetValueNames())
                        {
                            if (string.IsNullOrWhiteSpace(n) || n.Equals("@"))
                            {
                                continue;
                            }
                            if (n.Equals(obfuscatedProductCode))
                            {
                                k.DeleteValue(obfuscatedProductCode, false);
                                k.DeleteSubKeyTree(obfuscatedProductCode, false);
                                continue;
                            }
                            ++prodNum;
                        }

                        deleteSybkey = ((prodNum == 0) && (k.SubKeyCount == 0));
                    }
                }
                if (deleteSybkey)
                {
                    using (RegistryKey k = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components", true))
                    {
                        hklm.DeleteSubKeyTree(obfuscatedComponentCode, true);
                    }
                }
            }
        }
    }
}
