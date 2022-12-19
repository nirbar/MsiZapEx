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
        List<ReducePendingFileRenameOperations> reducePendingFileRenameOperations_ = new List<ReducePendingFileRenameOperations>();

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

        public void DeferRemoveFromPendingOperations(string filePath)
        {
            ReducePendingFileRenameOperations reduce = new ReducePendingFileRenameOperations();
            reduce.path = filePath;
            reducePendingFileRenameOperations_.Add(reduce);
        }

        public void Dispose()
        {
            DoReduceFilePendingOperations();
            DoSetValues();
            DoDeleteValues();
            DoDeleteKeys();
        }

        // Must be called before DoSetValues() and DoDeleteValues()
        private void DoReduceFilePendingOperations()
        {
            using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
            {
                using (RegistryKey k = root.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Session Manager", false))
                {
                    if (k != null)
                    {
                        string[] paths = k.GetValue("PendingFileRenameOperations") as string[];
                        if (paths != null)
                        {
                            List<string> newPaths = new List<string>(paths);
                            for (int i = 0; i < paths.Length; i += 2)
                            {
                                string p1 = paths[i];
                                if (p1.StartsWith(@"\??\"))
                                {
                                    p1 = p1.Substring(4);
                                }
                                if (reducePendingFileRenameOperations_.Any(r => r.path.Equals(p1, StringComparison.OrdinalIgnoreCase)))
                                {
                                    newPaths.RemoveAt(i + 1);
                                    newPaths.RemoveAt(i);
                                }
                            }

                            if (newPaths.Count != paths.Length)
                            {
                                if (newPaths.Count == 0)
                                {
                                    DeferDeleteValue(RegistryHive.LocalMachine, RegistryView.Default, "SYSTEM\\CurrentControlSet\\Control\\Session Manager", "PendingFileRenameOperations");
                                }
                                else
                                {
                                    DeferSetValue(RegistryHive.LocalMachine, RegistryView.Default, "SYSTEM\\CurrentControlSet\\Control\\Session Manager", "PendingFileRenameOperations", RegistryValueKind.MultiString, newPaths.ToArray());
                                }
                            }
                        }
                    }
                }
            }
        }

        // Must be called after DoReduceFilePendingOperations()
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

        // Must be called after DoReduceFilePendingOperations()
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
        struct ReducePendingFileRenameOperations
        {
            public string path;
        }
    }
}
