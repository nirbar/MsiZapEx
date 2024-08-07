using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

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

        public bool MachineScope { get; private set; }
        public string UserSID => MachineScope ? ProductInfo.LocalSystemSID : ProductInfo.CurrentUserSID;
        public Guid ComponentCode { get; private set; }
        public StatusFlags Status { get; private set; } = StatusFlags.None;
        public List<ProductKeyPath> ProductsKeyPath { get; } = new List<ProductKeyPath>();
        private static List<ComponentInfo> _components = new List<ComponentInfo>();

        private static ManualResetEventSlim _componentsLock = new ManualResetEventSlim(false);
        internal static List<ComponentInfo> GetAllComponents()
        {
            if (_componentsLock.IsSet)
            {
                return _components;
            }

            try
            {
                _componentsLock.Set();
                if (_components.Count > 0)
                {
                    return _components;
                }

                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{ProductInfo.LocalSystemSID}\Components", false))
                    {
                        if (k == null)
                        {
                            throw new FileNotFoundException();
                        }

                        string[] componentCodes = k.GetSubKeyNames();
                        foreach (string c in componentCodes)
                        {
                            // Ignore default value
                            if (string.IsNullOrWhiteSpace(c) || c.Equals("@") || !Guid.TryParse(c, out Guid id) || id.Equals(Guid.Empty))
                            {
                                continue;
                            }

                            Guid componentCode = GuidEx.MsiObfuscate(c);
                            ComponentInfo ci = _components.FirstOrDefault(cc => cc.MachineScope && cc.ComponentCode.Equals(componentCode));
                            if (ci == null)
                            {
                                ci = new ComponentInfo(c, true);
                            }
                        }
                    }
                    using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{ProductInfo.CurrentUserSID}\Components", false))
                    {
                        if (k != null)
                        {
                            string[] componentCodes = k.GetSubKeyNames();
                            foreach (string c in componentCodes)
                            {
                                // Ignore default value
                                if (string.IsNullOrWhiteSpace(c) || c.Equals("@") || !Guid.TryParse(c, out Guid id) || id.Equals(Guid.Empty))
                                {
                                    continue;
                                }

                                Guid componentCode = GuidEx.MsiObfuscate(c);
                                ComponentInfo ci = _components.FirstOrDefault(cc => !cc.MachineScope && cc.ComponentCode.Equals(componentCode));
                                if (ci == null)
                                {
                                    ci = new ComponentInfo(c, false);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                _componentsLock.Reset();
            }
            return _components;
        }

        internal static List<ComponentInfo> GetComponents(Guid productCode)
        {
            GetAllComponents();
            List<ComponentInfo> components = new List<ComponentInfo>();
            components.AddRange(_components.FindAll(ci => ci.ProductsKeyPath.Any(p => p.ProductCode.Equals(productCode))));
            return components;
        }

        internal static List<ComponentInfo> GetByKeyPath(string keyPath)
        {
            GetAllComponents();
            keyPath = keyPath.Replace("?", "");
            List<ComponentInfo> components = new List<ComponentInfo>();
            try
            {
                if (Path.IsPathRooted(keyPath))
                {
                    keyPath = Path.GetFullPath(keyPath);
                }
            }
            catch { }

            components.AddRange(_components.FindAll(ci => ci.ProductsKeyPath.Any(p => p.KeyPath.Replace("?", "").Equals(keyPath, StringComparison.InvariantCultureIgnoreCase))));
            return components;
        }

        internal static bool ResolveScope(Guid componentCode)
        {
            string obfuscatedGuid = GuidEx.MsiObfuscate(componentCode);
            using (RegistryKey hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey k = hklm64.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{ProductInfo.LocalSystemSID}\Components\{obfuscatedGuid}", false))
                {
                    if (k != null)
                    {
                        // Machine scope exists for this component
                        return true;
                    }
                }
            }
            return false;
        }

        public ComponentInfo(Guid componentCode, bool? machineScope = null)
            : this(componentCode.MsiObfuscate(), machineScope)
        {
        }

        internal ComponentInfo(string obfuscatedGuid, bool? machineScope = null)
        {
            GetAllComponents();

            ComponentCode = GuidEx.MsiObfuscate(obfuscatedGuid);
            if (machineScope == null)
            {
                machineScope = ResolveScope(ComponentCode);
            }
            this.MachineScope = machineScope.Value;
            ComponentInfo ci = _components.FirstOrDefault(c => c.MachineScope == machineScope && c.ComponentCode.Equals(ComponentCode));
            if (ci != null)
            {
                this.Status = ci.Status;
                this.ProductsKeyPath.AddRange(ci.ProductsKeyPath);
                return;
            }

            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Components\{obfuscatedGuid}", false))
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
            _components.Add(this);
        }

        internal void PrintProducts()
        {
            if (ProductsKeyPath.Count == 0)
            {
                Console.WriteLine($"Component '{ComponentCode}' is not related to any product");
                return;
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
            keyPath = keyPath.Replace("?", "");

            Match regMatch = registryKeyPath_.Match(keyPath);
            if (regMatch.Success)
            {
                string root = regMatch.Groups["root"].Value;
                if (int.TryParse(root, out int kr))
                {
                    RegistryHive hive;
                    RegistryView view = (kr >= 20) ? RegistryView.Registry64 : RegistryView.Registry32;
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
                    if (path.EndsWith($"{Path.DirectorySeparatorChar}") || path.EndsWith($"{Path.AltDirectorySeparatorChar}"))
                    {
                        using (RegistryKey rk = RegistryKey.OpenBaseKey(hive, view))
                        {
                            using (RegistryKey hk = rk.OpenSubKey(path, false))
                            {
                                return (hk != null);
                            }
                        }
                    }

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

            string subkeyName = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{UserSID}\Components\{obfuscatedComponentCode}";
            modifier.DeferDeleteValue(RegistryHive.LocalMachine, RegistryView.Registry64, subkeyName, obfuscatedProductCode);
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, RegistryView.Registry64, $"{subkeyName}\\{obfuscatedProductCode}");
            modifier.DeferDeleteKey(RegistryHive.LocalMachine, RegistryView.Registry64, subkeyName, k => ((k.SubKeyCount == 0) && (k.GetValueNames()?.Any(v => !string.IsNullOrEmpty(v) && !v.Equals("@")) != true)));
        }
    }
}
