using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MsiZapEx
{
    class RegistryModifier : IDisposable
    {
        List<DeleteKey> deleteKeys_ = new List<DeleteKey>();
        List<DeleteValue> deleteValues_ = new List<DeleteValue>();
        List<SetValue> setValues_ = new List<SetValue>();

        public void DeferDeleteKey(RegistryHive hive, RegistryView view, string key, Func<RegistryKey, bool> predicate = null)
        {
            DeleteKey deleteKey = new DeleteKey();
            deleteKey.hive = hive;
            deleteKey.view = view;
            deleteKey.key = key;
            deleteKey.predicate = predicate;

            deleteKeys_.Add(deleteKey);
        }

        public void DeferSetValue(RegistryHive hive, RegistryView view, string key, string name, RegistryValueKind kind, object value, Func<RegistryKey, string, bool> predicate = null)
        {
            SetValue setValue = new SetValue();
            setValue.hive = hive;
            setValue.view = view;
            setValue.key = key;
            setValue.name = name;
            setValue.value = value;
            setValue.kind = kind;
            setValue.predicate = predicate;

            setValues_.Add(setValue);
        }

        public void DeferDeleteValue(RegistryHive hive, RegistryView view, string key, string name, Func<RegistryKey, string, bool> predicate = null)
        {
            DeleteValue deleteValue = new DeleteValue();
            deleteValue.hive = hive;
            deleteValue.view = view;
            deleteValue.key = key;
            deleteValue.name = name;
            deleteValue.predicate = predicate;

            deleteValues_.Add(deleteValue);
        }

        public void Dispose()
        {
            DoSetValues();
            DoDeleteValues();
            DoDeleteKeys();
        }

        private void DoSetValues()
        {
            foreach (SetValue setValue in setValues_)
            {
                bool doSet = false;
                using (RegistryKey root = RegistryKey.OpenBaseKey(setValue.hive, setValue.view))
                {
                    using (RegistryKey k = root.OpenSubKey(setValue.key, false))
                    {
                        if ((k != null) && k.GetValueNames().Contains(setValue.name) && ((setValue.predicate == null) || setValue.predicate.Invoke(k, setValue.name)))
                        {
                            doSet = true;
                        }
                    }

                    if (doSet)
                    {
                        if ((Settings.Instance?.Verbose == true) || (Settings.Instance?.DryRun == true))
                        {
                            Console.WriteLine($"Setting value: '{setValue.hive}\\{setValue.key}@{setValue.name}'");
                        }

                        if (Settings.Instance?.DryRun != true)
                        {
                            using (RegistryKey k = root.OpenSubKey(setValue.key, true))
                            {
                                k.SetValue(setValue.name, setValue.value, setValue.kind);
                            }
                        }
                    }
                }
            }
        }

        private void DoDeleteValues()
        {
            foreach (DeleteValue delValue in deleteValues_)
            {
                bool doDelete = false;
                using (RegistryKey root = RegistryKey.OpenBaseKey(delValue.hive, delValue.view))
                {
                    using (RegistryKey k = root.OpenSubKey(delValue.key, false))
                    {
                        if ((k != null) && k.GetValueNames().Contains(delValue.name) && ((delValue.predicate == null) || delValue.predicate.Invoke(k, delValue.name)))
                        {
                            doDelete = true;
                        }
                    }

                    if (doDelete)
                    {
                        if ((Settings.Instance?.Verbose == true) || (Settings.Instance?.DryRun == true))
                        {
                            Console.WriteLine($"Deleting value: '{delValue.hive}\\{delValue.key}@{delValue.name}'");
                        }

                        if (Settings.Instance?.DryRun != true)
                        {
                            using (RegistryKey k = root.OpenSubKey(delValue.key, true))
                            {
                                k.DeleteValue(delValue.name);
                            }
                        }
                    }
                }
            }
        }

        private void DoDeleteKeys()
        {
            foreach (DeleteKey delKey in deleteKeys_)
            {
                bool doDelete = false;
                using (RegistryKey root = RegistryKey.OpenBaseKey(delKey.hive, delKey.view))
                {
                    using (RegistryKey k = root.OpenSubKey(delKey.key, false))
                    {
                        if ((k != null) && ((delKey.predicate == null) || delKey.predicate.Invoke(k)))
                        {
                            doDelete = true;
                        }
                    }

                    if (doDelete)
                    {
                        if ((Settings.Instance?.Verbose == true) || (Settings.Instance?.DryRun == true))
                        {
                            Console.WriteLine($"Deleting key: '{delKey.hive}\\{delKey.key}'");
                        }

                        if (Settings.Instance?.DryRun != true)
                        {
                            root.DeleteSubKeyTree(delKey.key);
                        }
                    }
                }
            }
        }

        struct SetValue
        {
            public RegistryHive hive;
            public RegistryView view;
            public string key;
            public string name;
            public RegistryValueKind kind;
            public object value;
            public Func<RegistryKey, string, bool> predicate;
        }
        struct DeleteValue
        {
            public RegistryHive hive;
            public RegistryView view;
            public string key;
            public string name;
            public Func<RegistryKey, string, bool> predicate;
        }
        struct DeleteKey
        {
            public RegistryHive hive;
            public RegistryView view;
            public string key;
            public Func<RegistryKey, bool> predicate;
        }
    }
}
