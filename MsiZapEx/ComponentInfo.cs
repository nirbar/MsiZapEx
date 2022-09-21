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
            Products = 1,
            KeyPath = 2 * Products,

            Good = Products | KeyPath
        }

        public struct ProductKeyPath
        {
            internal ProductKeyPath(Guid productCode, string keyPath, bool exists)
            {
                ProductCode = productCode;
                KeyPath = keyPath;
                KeyPathExists = exists;
            }

            public Guid ProductCode { get; private set; }
            public string KeyPath { get; private set; }
            public bool KeyPathExists { get; private set; }
        }

        public Guid ComponentCode { get; private set; }
        public StatusFlags Status { get; private set; } = StatusFlags.None;
        public List<ProductKeyPath> ProductsKeyPath { get; } = new List<ProductKeyPath>();

        internal static List<ComponentInfo> GetComponents(Guid productCode)
        {
            List<ComponentInfo> components = new List<ComponentInfo>();

            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey k = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Components", false))
                {
                    if (k == null)
                    {
                        throw new FileNotFoundException();
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
                        if (ci.ProductsKeyPath.Any(p => p.ProductCode.Equals(productCode)))
                        {
                            components.Add(ci);
                        }
                    }
                }
            }
            return components;
        }

        public ComponentInfo(Guid componentCode)
            : this(componentCode.MsiObfuscate())
        {
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

                        foreach (string obfuscatedProductCode in k.GetValueNames())
                        {
                            if (string.IsNullOrWhiteSpace(obfuscatedProductCode) || obfuscatedProductCode.Equals("@"))
                            {
                                continue;
                            }

                            if (Guid.TryParse(obfuscatedProductCode, out Guid id))
                            {
                                Guid productCode = GuidEx.MsiObfuscate(obfuscatedProductCode);
                                string keyPath = k.GetValue(obfuscatedProductCode)?.ToString();
                                bool exists = ValidateKeyPath(keyPath);

                                ProductsKeyPath.Add(new ProductKeyPath(productCode, keyPath, exists));
                                Status |= StatusFlags.Products;
                            }

                            if (!Status.HasFlag(StatusFlags.Products) || ProductsKeyPath.TrueForAll(p => p.KeyPathExists))
                            {
                                Status |= StatusFlags.KeyPath;
                            }
                        }
                    }
                }
            }
        }

        internal void PrintProducts()
        {
            if (ProductsKeyPath.Count == 0)
            {
                Console.WriteLine($"Component '{ComponentCode}' is not related to any product");
            }

            Console.WriteLine($"Component '{ComponentCode}' belongs to {ProductsKeyPath.Count} products");
            foreach (ProductKeyPath product in ProductsKeyPath)
            {
                Console.WriteLine($"\tBelongs to product '{product.ProductCode}' with key path '{product.KeyPath}'");
                if (!product.KeyPathExists)
                {
                    Console.WriteLine($"\t\tKeyPath is missing");
                }

                //TODO How does the registry reflect a permanent component with different key paths?
                if (product.ProductCode.Equals(Guid.Empty))
                {
                    Console.WriteLine($"\t\tThis KeyPath is permanent for this component");
                }
            }
        }

        internal void PrintProductState(Guid productCode)
        {
            ProductKeyPath keyPath = ProductsKeyPath.FirstOrDefault(p => p.ProductCode.Equals(productCode));
            if (!keyPath.ProductCode.Equals(productCode))
            {
                Console.WriteLine($"\tComponent '{ComponentCode}' is not related to product {productCode}");
                return;
            }
            if (!keyPath.KeyPathExists)
            {
                Console.WriteLine($"\tKeyPath '{keyPath.KeyPath}' not found for component '{ComponentCode}'");
            }
        }

        private static readonly Regex registryKeyPath_ = new Regex(@"^(?<root>[0-9]+):\\?(?<path>.+)$", RegexOptions.Compiled);
        private static bool ValidateKeyPath(string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
            {
                return true;
            }

            Match regMatch = registryKeyPath_.Match(keyPath);
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
                            return false;
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
                                return true;
                            }
                        }
                    }
                }
            }
            else if (keyPath.EndsWith($"{Path.DirectorySeparatorChar}") || keyPath.EndsWith($"{Path.VolumeSeparatorChar}"))
            {
                if (Directory.Exists(keyPath))
                {
                    return true;
                }
            }
            else if (File.Exists(keyPath))
            {
                return true;
            }

            return false;
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
