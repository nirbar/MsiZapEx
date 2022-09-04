using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MsiZapEx
{
    public class ComponentInfo
    {
        [Flags]
        public enum StatusFlags
        {
            None = 0,
            KeyPath = 1,

            Good = KeyPath
        }

        public Guid ComponentCode { get; private set; }
        public string KeyPath { get; private set; }
        public StatusFlags Status { get; private set; } = StatusFlags.None;

        Dictionary<Guid, string> productToKeyPath = new Dictionary<Guid, string>();

        internal static List<ComponentInfo> GetComponents(Guid productCode)
        {
            List<ComponentInfo> components = new List<ComponentInfo>();

            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
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
                        if (string.IsNullOrWhiteSpace(c) || c.Equals("@") || !Guid.TryParse(c, out Guid id))
                        {
                            continue;
                        }

                        ComponentInfo ci = new ComponentInfo(c);
                        if (ci.productToKeyPath.ContainsKey(productCode))
                        {
                            components.Add(ci);
                            if (!ci.Status.HasFlag(StatusFlags.KeyPath))
                            {
                                Console.WriteLine($"KeyPath '{ci.KeyPath}' not found for component '{ci.ComponentCode}'");
                            }
                        }
                    }
                }
            }
            return components;
        }

        internal ComponentInfo(string obfuscatedGuid)
        {
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
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
                                KeyPath = k.GetValue(n)?.ToString();
                                productToKeyPath[GuidEx.MsiObfuscate(n)] = KeyPath;

                                if (!string.IsNullOrWhiteSpace(KeyPath))
                                {
                                    ValidateKeyPath();
                                }
                            }
                        }
                    }
                }
            }
        }

        private Regex registryKeyPath_ = new Regex(@"^(?<root>[0-9]+):\\?(?<path>.+)$", RegexOptions.Compiled);
        private void ValidateKeyPath()
        {
            Match regMatch = registryKeyPath_.Match(KeyPath);
            if (regMatch.Success)
            {
                string root = regMatch.Groups["root"].Value;
                if (int.TryParse(root, out int kr))
                {
                    RegistryHive hive;
                    RegistryView view = (kr > 20) ? RegistryView.Registry64 : RegistryView.Registry32;
                    switch (kr % 10)
                    {
                        case 0:
                            hive = RegistryHive.ClassesRoot;
                            break;
                        case 1:
                            hive = RegistryHive.CurrentUser;
                            break;
                        case 2:
                            hive = RegistryHive.LocalMachine;
                            break;
                        case 3:
                            hive = RegistryHive.Users;
                            break;
                        default:
                            return;
                    }

                    string path = regMatch.Groups["path"].Value;
                    string key = Path.GetDirectoryName(path);
                    string name = Path.GetFileName(path);
                    using (RegistryKey rk = RegistryKey.OpenBaseKey(hive, view))
                    {
                        using (RegistryKey hk = rk.OpenSubKey(key, false))
                        {
                            if ((hk != null) && hk.GetValueNames().Contains(name))
                            {
                                Status |= StatusFlags.KeyPath;
                            }
                        }
                    }
                }
            }
            else if (KeyPath.EndsWith($"{Path.DirectorySeparatorChar}") || KeyPath.EndsWith($"{Path.VolumeSeparatorChar}"))
            {
                if (Directory.Exists(KeyPath))
                {
                    Status |= StatusFlags.KeyPath;
                }
            }
            else if (File.Exists(KeyPath))
            {
                Status |= StatusFlags.KeyPath;
            }
        }

        internal void Prune(Guid productCode, RegistryModifier modifier)
        {
            string obfuscatedComponentCode = GuidEx.MsiObfuscate(ComponentCode);
            string obfuscatedProductCode = GuidEx.MsiObfuscate(productCode);

            string subkeyName = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components\{obfuscatedComponentCode}";
            modifier.DeferDeleteValue(RegistryHive.LocalMachine, RegistryView.Registry64, subkeyName, obfuscatedProductCode);
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, RegistryView.Registry64, $"{subkeyName}\\{obfuscatedProductCode}");
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, RegistryView.Registry64, subkeyName, k => ((k.SubKeyCount == 0) && (k.GetValueNames()?.Any(v => !string.IsNullOrEmpty(v) && !v.Equals("@")) != true)));
        }
    }
}
