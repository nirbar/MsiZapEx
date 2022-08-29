using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace MsiZapEx
{
    public class ComponentInfo
    {
        public Guid ComponentCode { get; private set; }
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
                        return null;
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
                                productToKeyPath[GuidEx.MsiObfuscate(n)] = k.GetValue(n)?.ToString();
                            }
                        }
                    }
                }
            }
        }
    }
}
