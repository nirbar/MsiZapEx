using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MsiZapEx
{
    [Serializable]
    public class OrphanComponent
    {
        public OrphanComponent()
        {
            NonExistingClients = new List<string>();
        }

        public OrphanComponent(string obfusctaedId) : this()
        {
            Guid = obfusctaedId.MsiObfuscate();
        }

        public Guid Guid { get; set; }

        public List<string> NonExistingClients { get; }

        public static List<OrphanComponent> DetectOrphanComponents()
        {
            List<OrphanComponent> orphans = new List<OrphanComponent>();

            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
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

                        using (RegistryKey k2 = k.OpenSubKey(c))
                        {
                            OrphanComponent orphan = OrphanComponent.IsOrphanComponent(k2);
                            if (orphan != null)
                            {
                                orphans.Add(orphan);
                            }
                        }
                    }
                }
            }

            return orphans;
        }

        public static OrphanComponent IsOrphanComponent(RegistryKey componentKey)
        {
            string[] productCodes = componentKey.GetValueNames();

            string compName = Path.GetFileName(componentKey.Name);
            OrphanComponent orphan = new OrphanComponent(compName);

            foreach (string p in productCodes)
            {
                // Ignore default value
                if (string.IsNullOrWhiteSpace(p) || p.Equals("@") || p.Equals("00000000000000000000000000000000"))
                {
                    continue;
                }

                Guid productCode = p.MsiObfuscate();
                Guid componentGuid = compName.MsiObfuscate();

                IEnumerable<ProductInstallation> pis = ProductInstallation.GetProducts(productCode.ToString("B"), null, UserContexts.All);
                if ((pis == null) || (pis.Count() <= 0))
                {
                    string keyPath = componentKey.GetValue(p) as string;
                    Console.WriteLine($"Component '{componentGuid.ToString("B")}' in registry key '{componentKey.Name}' is registered to product '{productCode.ToString("B")} which is not installed with KeyPath='{keyPath}'");
                    orphan.NonExistingClients.Add(productCode.ToString("B"));
                }
            }

            return orphan.NonExistingClients.Count > 0 ? orphan : null;
        }
    }
}